using System.Diagnostics;

namespace WindowsActivityLogger.Services;

public sealed class ActivitySummaryService
{
	private readonly AppConfiguration _config;
	private readonly ILogger _logger;
	private readonly Func<string>? _processorPathResolver;
	private readonly Func<string, IReadOnlyList<string>, CancellationToken, Task<ProcessExecutionResult>>? _processRunner;

	public ActivitySummaryService(
		AppConfiguration config,
		ILogger logger,
		Func<string>? processorPathResolver = null,
		Func<string, IReadOnlyList<string>, CancellationToken, Task<ProcessExecutionResult>>? processRunner = null)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_processorPathResolver = processorPathResolver;
		_processRunner = processRunner;
	}

	public string GetLogDirectory() => _config.GetEffectiveSavePath();

	public async Task<SummaryGenerationResult> GenerateSummaryAsync(string logPath, bool overwriteExisting = false, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(logPath))
			return SummaryGenerationResult.Failed("A log file path is required.");

		if (!File.Exists(logPath))
			return SummaryGenerationResult.MissingLog(logPath);

		var outputDirectory = GetConfiguredOutputDirectory();
		if (outputDirectory is null)
			return SummaryGenerationResult.MissingOutputDirectory();

		var outputPath = GetSummaryOutputPath(logPath, outputDirectory);
		if (File.Exists(outputPath) && !overwriteExisting)
			return SummaryGenerationResult.AlreadyExists(logPath, outputPath);

		try
		{
			Directory.CreateDirectory(outputDirectory);
			if (overwriteExisting && File.Exists(outputPath))
				File.Delete(outputPath);

			var processorPath = ResolveProcessorPath();
			var args = new[]
			{
				"--logpath",
				logPath,
				"--interval",
				_config.ActivitySampleIntervalSeconds.ToString(),
				"--out-dir",
				outputDirectory,
			};

			_logger.LogInformation($"Generating activity summary for '{logPath}' → '{outputPath}'");
			var execution = await (_processRunner ?? RunProcessAsync)(processorPath, args, cancellationToken);

			if (execution.ExitCode != 0)
			{
				var error = string.IsNullOrWhiteSpace(execution.StandardError)
					? $"ActivityLogProcessor exited with code {execution.ExitCode}."
					: execution.StandardError.Trim();
				_logger.LogError($"Activity summary generation failed: {error}");
				return SummaryGenerationResult.Failed(error, logPath, outputPath);
			}

			if (!File.Exists(outputPath))
			{
				const string missingOutputMessage = "Summary generation completed but no output file was created.";
				_logger.LogError(missingOutputMessage);
				return SummaryGenerationResult.Failed(missingOutputMessage, logPath, outputPath);
			}

			_logger.LogInformation($"Activity summary written to '{outputPath}'");
			return SummaryGenerationResult.Success(logPath, outputPath);
		}
		catch (Exception ex)
		{
			_logger.LogException(ex, "Activity summary generation");
			return SummaryGenerationResult.Failed(ex.Message, logPath, outputPath);
		}
	}

	public async Task<SummaryGenerationResult> GeneratePreviousDaySummaryIfMissingAsync(DateTime? now = null, CancellationToken cancellationToken = default)
	{
		var previousDayLogPath = GetPreviousDayLogPath(now ?? DateTime.Now);
		if (previousDayLogPath is null)
			return SummaryGenerationResult.NoPreviousDayLog();

		var outputDirectory = GetConfiguredOutputDirectory();
		if (outputDirectory is null)
			return SummaryGenerationResult.MissingOutputDirectory();

		var outputPath = GetSummaryOutputPath(previousDayLogPath, outputDirectory);
		if (File.Exists(outputPath))
			return SummaryGenerationResult.AlreadyExists(previousDayLogPath, outputPath);

		return await GenerateSummaryAsync(previousDayLogPath, cancellationToken: cancellationToken);
	}

	internal string? GetConfiguredOutputDirectory()
	{
		return string.IsNullOrWhiteSpace(_config.ActivitySummaryOutputDir)
			? null
			: _config.ActivitySummaryOutputDir.Trim();
	}

	internal string GetSummaryOutputPath(string logPath, string outputDirectory)
	{
		return Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(logPath) + ".md");
	}

	internal string? GetPreviousDayLogPath(DateTime now)
	{
		var logDirectory = GetLogDirectory();
		if (!Directory.Exists(logDirectory))
			return null;

		var yesterday = now.Date.AddDays(-1);
		var expectedName = yesterday.ToString("yyyy-MM-dd") + ".log";
		var expectedPath = Path.Combine(logDirectory, expectedName);
		if (File.Exists(expectedPath))
			return expectedPath;

		return Directory.EnumerateFiles(logDirectory, "*.log")
			.Select(path => new
			{
				Path = path,
				Date = TryGetLogDate(path) ?? new FileInfo(path).LastWriteTime.Date,
			})
			.Where(file => file.Date < now.Date)
			.OrderByDescending(file => file.Date)
			.ThenByDescending(file => new FileInfo(file.Path).LastWriteTimeUtc)
			.Select(file => file.Path)
			.FirstOrDefault();
	}

	private static DateTime? TryGetLogDate(string path)
	{
		var fileName = Path.GetFileNameWithoutExtension(path);
		return DateTime.TryParseExact(fileName, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsed)
			? parsed.Date
			: null;
	}

	private string ResolveProcessorPath()
	{
		if (_processorPathResolver is not null)
			return _processorPathResolver();

		var processorPath = Path.Combine(AppContext.BaseDirectory, "ActivityLogProcessor.exe");
		if (!File.Exists(processorPath))
			throw new FileNotFoundException($"ActivityLogProcessor.exe was not found in the running executable directory: {AppContext.BaseDirectory}");

		return processorPath;
	}

	private static async Task<ProcessExecutionResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
	{
		using var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = fileName,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			}
		};

		foreach (var argument in arguments)
			process.StartInfo.ArgumentList.Add(argument);

		process.Start();
		var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		return new ProcessExecutionResult(process.ExitCode, await stdOutTask, await stdErrTask);
	}

	public sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);

	public sealed record SummaryGenerationResult(
		SummaryGenerationStatus Status,
		string Message,
		string? LogPath = null,
		string? OutputPath = null)
	{
		public bool Succeeded => Status == SummaryGenerationStatus.Success;

		public static SummaryGenerationResult Success(string logPath, string outputPath)
			=> new(SummaryGenerationStatus.Success, "Summary generated.", logPath, outputPath);

		public static SummaryGenerationResult AlreadyExists(string logPath, string outputPath)
			=> new(SummaryGenerationStatus.AlreadyExists, "Summary already exists.", logPath, outputPath);

		public static SummaryGenerationResult MissingOutputDirectory()
			=> new(SummaryGenerationStatus.MissingOutputDirectory, "Activity summary output directory is not configured.");

		public static SummaryGenerationResult MissingLog(string logPath)
			=> new(SummaryGenerationStatus.MissingLog, "Selected activity log file was not found.", logPath);

		public static SummaryGenerationResult NoPreviousDayLog()
			=> new(SummaryGenerationStatus.NoPreviousDayLog, "No previous-day activity log was found.");

		public static SummaryGenerationResult Failed(string message, string? logPath = null, string? outputPath = null)
			=> new(SummaryGenerationStatus.Failed, message, logPath, outputPath);
	}

	public enum SummaryGenerationStatus
	{
		Success,
		AlreadyExists,
		MissingOutputDirectory,
		MissingLog,
		NoPreviousDayLog,
		Failed,
	}
}