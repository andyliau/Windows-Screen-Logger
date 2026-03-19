# Kai — History

> Personal knowledge log. Updated after each work session.

## 2026-03-19 — Team Setup

- Joined the project as C# Developer.
- Project: Windows Screen Logger — C# .NET 9.0 WinForms, self-contained single-file deployment targeting Windows 10+ (22000).
- Key production files: `ScreenshotService.cs`, `CleanupService.cs`, `InstallationCommandService.cs`, `MainForm.cs`, `SettingsForm.cs`, `AppConfiguration.cs`, `DefaultLogger.cs`, `CommandLineHandler.cs`.
- Dependencies: SkiaSharp 3.119.0, System.CommandLine 2.0.0-beta4, System.Text.Json 9.0.8, Microsoft.Windows.ImplementationLibrary.
- Nullable reference types enabled; ImplicitUsings enabled.
- Embedded installation scripts: `Installation/UninstallScript.bat`, `Installation/UninstallScript.ps1`.
