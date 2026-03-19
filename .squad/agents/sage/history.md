# Sage — History

> Personal knowledge log. Updated after each work session.

## 2026-03-19 — Team Setup

- Joined the project as QA Engineer.
- Project: Windows Screen Logger — .NET 9 WinForms app targeting Windows 10+ (22000).
- Existing test project: `Tests/WindowsScreenLogger.Tests.csproj` using xUnit 2.5.0 + coverlet.
- Existing test file: `Tests/BitmapMemoryTests.cs` (bitmap memory handling).
- Services to cover: `ScreenshotService`, `CleanupService`, `InstallationCommandService`.
- Key testing concern: bitmap/GDI handle leaks — SkiaSharp resources must be disposed.
- Windows-specific APIs (registry, screen capture, session events) will need mocking strategy.
