using WindowsActivityLogger;
using WindowsActivityLogger.Services;
using Xunit;

namespace WindowsActivityLogger.Tests;

public class ActivitySummaryServiceTests : IDisposable
{
	private readonly string _tempDir;
	private readonly string _logDir;
	private readonly string _outputDir;
	private readonly AppConfiguration _config;
	private readonly NoopLogger _logger = new();

	public ActivitySummaryServiceTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "ActivitySummaryTests_" + Guid.NewGuid());
		_logDir = Path.Combine(_tempDir, "logs");
		_outputDir = Path.Combine(_tempDir, "summary");
		Directory.CreateDirectory(_logDir);

		_config = new AppConfiguration
		{
			CustomSavePath = _logDir,
			ActivitySummaryOutputDir = _outputDir,
			ActivitySampleIntervalSeconds = 5,
		};
	}

	public void Dispose()
	{
		try { Directory.Delete(_tempDir, recursive: true); } catch { }
	}

	[Fact]
	public void GetSummaryOutputPath_UsesLogFileName()
	{
		var service = CreateService();
		var outputPath = service.GetSummaryOutputPath(Path.Combine(_logDir, "2026-03-26.log"), _outputDir);

		Assert.Equal(Path.Combine(_outputDir, "2026-03-26.md"), outputPath);
	}

	[Fact]
	public void GetPreviousDayLogPath_ReturnsExactPreviousDayFile()
	{
		var previousDay = new DateTime(2026, 3, 27, 8, 0, 0);
		var expectedPath = Path.Combine(_logDir, "2026-03-26.log");
		File.WriteAllText(expectedPath, "sample");

		var service = CreateService();

		Assert.Equal(expectedPath, service.GetPreviousDayLogPath(previousDay));
	}

	[Fact]
	public void GetPreviousDayLogPath_FallsBackToLatestAvailableLogBeforeToday()
	{
		var now = new DateTime(2026, 3, 27, 8, 0, 0);
		var olderPath = Path.Combine(_logDir, "2026-03-20.log");
		var latestPath = Path.Combine(_logDir, "2026-03-25.log");
		File.WriteAllText(olderPath, "older");
		File.WriteAllText(latestPath, "latest available before today");

		var service = CreateService();

		Assert.Equal(latestPath, service.GetPreviousDayLogPath(now));
	}

	[Fact]
	public void GetPreviousDayLogPath_DoesNotUseTodayLogWhenYesterdayIsMissing()
	{
		var now = new DateTime(2026, 3, 27, 8, 0, 0);
		var lastWeekPath = Path.Combine(_logDir, "2026-03-19.log");
		var todayPath = Path.Combine(_logDir, "2026-03-27.log");
		File.WriteAllText(lastWeekPath, "last week");
		File.WriteAllText(todayPath, "today");

		var service = CreateService();

		Assert.Equal(lastWeekPath, service.GetPreviousDayLogPath(now));
	}

	[Fact]
	public void GetPreviousDayLogPath_IgnoresNonActivityLogFiles()
	{
		var now = new DateTime(2026, 3, 27, 8, 0, 0);
		var validLogPath = Path.Combine(_logDir, "2026-03-25.log");
		var otherLogPath = Path.Combine(_logDir, "application.log");
		File.WriteAllText(validLogPath, "activity log");
		File.WriteAllText(otherLogPath, "not an activity log");

		var service = CreateService();

		Assert.Equal(validLogPath, service.GetPreviousDayLogPath(now));
	}

	[Fact]
	public async Task GeneratePreviousDaySummaryIfMissingAsync_SkipsWhenSummaryExists()
	{
		var now = new DateTime(2026, 3, 27, 8, 0, 0);
		var logPath = Path.Combine(_logDir, "2026-03-26.log");
		var outputPath = Path.Combine(_outputDir, "2026-03-26.md");
		Directory.CreateDirectory(_outputDir);
		File.WriteAllText(logPath, "sample");
		File.WriteAllText(outputPath, "existing summary");

		var service = CreateService();
		var result = await service.GeneratePreviousDaySummaryIfMissingAsync(now);

		Assert.Equal(ActivitySummaryService.SummaryGenerationStatus.AlreadyExists, result.Status);
		Assert.Equal(outputPath, result.OutputPath);
	}

	[Fact]
	public async Task GenerateSummaryAsync_InvokesProcessorWithConfiguredPaths()
	{
		var logPath = Path.Combine(_logDir, "2026-03-26.log");
		File.WriteAllText(logPath, "sample");
		string? runnerFileName = null;
		IReadOnlyList<string>? runnerArgs = null;

		var service = CreateService(
			processorPathResolver: () => "fake-processor.exe",
			processRunner: (fileName, args, _) =>
			{
				runnerFileName = fileName;
				runnerArgs = args;
				Directory.CreateDirectory(_outputDir);
				File.WriteAllText(Path.Combine(_outputDir, "2026-03-26.md"), "generated summary");
				return Task.FromResult(new ActivitySummaryService.ProcessExecutionResult(0, string.Empty, string.Empty));
			});

		var result = await service.GenerateSummaryAsync(logPath);

		Assert.Equal(ActivitySummaryService.SummaryGenerationStatus.Success, result.Status);
		Assert.Equal("fake-processor.exe", runnerFileName);
		Assert.NotNull(runnerArgs);
		Assert.Equal(new[] { "--logpath", logPath, "--interval", "5", "--out-dir", _outputDir }, runnerArgs!.ToArray());
	}

	[Fact]
	public async Task GenerateSummaryAsync_ReturnsMissingOutputDirectory_WhenNotConfigured()
	{
		var logPath = Path.Combine(_logDir, "2026-03-26.log");
		File.WriteAllText(logPath, "sample");
		_config.ActivitySummaryOutputDir = null;

		var service = CreateService();
		var result = await service.GenerateSummaryAsync(logPath);

		Assert.Equal(ActivitySummaryService.SummaryGenerationStatus.MissingOutputDirectory, result.Status);
	}

	[Fact]
	public async Task GenerateSummaryAsync_WithOverwriteExisting_ReplacesExistingSummary()
	{
		var logPath = Path.Combine(_logDir, "2026-03-26.log");
		var outputPath = Path.Combine(_outputDir, "2026-03-26.md");
		File.WriteAllText(logPath, "sample");
		Directory.CreateDirectory(_outputDir);
		File.WriteAllText(outputPath, "existing summary");
		var processCallCount = 0;

		var service = CreateService(
			processorPathResolver: () => "fake-processor.exe",
			processRunner: (_, _, _) =>
			{
				processCallCount++;
				File.WriteAllText(outputPath, "replacement summary");
				return Task.FromResult(new ActivitySummaryService.ProcessExecutionResult(0, string.Empty, string.Empty));
			});

		var result = await service.GenerateSummaryAsync(logPath, overwriteExisting: true);

		Assert.Equal(ActivitySummaryService.SummaryGenerationStatus.Success, result.Status);
		Assert.Equal(1, processCallCount);
		Assert.Equal("replacement summary", File.ReadAllText(outputPath));
	}

	[Fact]
	public async Task GenerateSummaryAsync_WithOverwriteExisting_RestoresExistingSummaryOnFailure()
	{
		var logPath = Path.Combine(_logDir, "2026-03-26.log");
		var outputPath = Path.Combine(_outputDir, "2026-03-26.md");
		File.WriteAllText(logPath, "sample");
		Directory.CreateDirectory(_outputDir);
		File.WriteAllText(outputPath, "existing summary");

		var service = CreateService(
			processorPathResolver: () => "fake-processor.exe",
			processRunner: (_, _, _) => Task.FromResult(new ActivitySummaryService.ProcessExecutionResult(1, string.Empty, "processor failed")));

		var result = await service.GenerateSummaryAsync(logPath, overwriteExisting: true);

		Assert.Equal(ActivitySummaryService.SummaryGenerationStatus.Failed, result.Status);
		Assert.Equal("existing summary", File.ReadAllText(outputPath));
	}

	private ActivitySummaryService CreateService(
		Func<string>? processorPathResolver = null,
		Func<string, IReadOnlyList<string>, CancellationToken, Task<ActivitySummaryService.ProcessExecutionResult>>? processRunner = null)
	{
		return new ActivitySummaryService(_config, _logger, processorPathResolver, processRunner);
	}

	private sealed class NoopLogger : ILogger
	{
		public void Initialize(bool enableLogging = false, string logLevel = "Information") { }
		public void LogTrace(string message) { }
		public void LogDebug(string message) { }
		public void LogInformation(string message) { }
		public void LogWarning(string message) { }
		public void LogError(string message) { }
		public void LogCritical(string message) { }
		public void LogException(Exception exception, string? context = null) { }
		public void LogCommandLineArgs(string[] args) { }
		public void LogUninstallOperation(string operation, bool success, string? details = null) { }
		public void LogRegistryOperation(string operation, string key, bool success, string? details = null) { }
		public string? GetLogFilePath() => null;
		public void CleanupOldLogs(int daysToKeep = 7) { }
		public void LogStartup() { }
		public void LogShutdown() { }
	}
}