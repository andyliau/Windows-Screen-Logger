# Windows Activity Logger

A lightweight Windows background app that periodically captures screenshots and logs which applications you are using — so you can understand where your time goes each day.

Runs as a system tray icon with near-zero CPU impact.

## Features

### Screen capture
- Captures screenshots at a configurable interval (default 5 s)
- Adjustable image size (% of original resolution) and JPEG quality
- Screenshots organised into daily date folders under your save path

### Activity logging *(enabled by default)*
- Records which window is in focus and for how long
- Samples the foreground window every 5 s; buffers writes and flushes once per minute
- One plain-text log file per day (`YYYY-MM-DD.log`) alongside the screenshot folders
- Privacy-aware: titles from password managers and secure messaging apps are redacted

**Log format** — one line per record, no schema:
```
09:00:12 code "auth.ts — VS Code"
09:00:17 code "auth.ts — VS Code"
.
.
09:31:05 chrome "GitHub PR #42 · review"
09:31:10 msedge "Jira · WSL-1234"
.
```
`HH:mm:ss proc "title"` — window record  
`.` — same window still active (one dot per sample, e.g. every 5 s)

The log is designed to be fed into an AI for daily work summarisation. A lightweight pre-processing script can count dots to compute focus duration (dots × sample interval = seconds spent).

### Session awareness
- Pauses capture and logging when the session is locked; resumes on unlock
- Pauses on system suspend; resumes on wake

### Automatic cleanup
- Deletes screenshot folders **and** activity log files older than the configured retention period (`clearDays`, default 30)

### Windows integration
- Optional start with Windows (via registry)
- Installs/uninstalls cleanly via the tray menu

## Requirements

- Windows 10 22H2 or later (build 22000+)
- .NET 10 (self-contained build — no separate install required)

## Installation

Download the latest release and run `WindowsScreenLogger.exe`. The app minimises to the system tray on first launch.

To build from source:
```bash
git clone https://github.com/andyliau/Windows-Activity-Logger.git
cd Windows-Activity-Logger
dotnet build
```

## Usage

1. Run the app — it appears in the system tray.
2. Right-click the tray icon:
   - **Settings** — configure capture interval, image size, quality, retention, and activity logging
   - **Open Saved Image Folder** — opens the screenshot root in Explorer
   - **Open Activity Log** — opens today's activity log in Notepad
   - **Clean Old Screenshots** — manually trigger cleanup
   - **Exit**

## Configuration

Settings are stored in `%APPDATA%\WindowsScreenLogger\config.json`.

| Key | Default | Description |
|-----|---------|-------------|
| `captureInterval` | `5` | Seconds between screenshots |
| `imageSizePercentage` | `100` | Resize captured image (10–100) |
| `imageQuality` | `30` | JPEG quality (10–100) |
| `clearDays` | `30` | Days to keep screenshots and activity logs |
| `enableLogging` | `false` | Write diagnostic log file |
| `enableActivityLogging` | `true` | Enable activity logging |
| `activitySampleIntervalSeconds` | `5` | How often to poll the foreground window (2–30) |
| `startWithWindows` | `false` | Launch on login |

### Activity logging

Activity logging is enabled by default. On first launch a balloon notification confirms it is active. You can disable it anytime via **Settings → Activity Logging**, or by setting `enableActivityLogging` to `false` in `config.json`.

Activity logs are written to:
```
%USERPROFILE%\WindowsScreenLogger\
  2026-03-19\                   ← screenshots
    screenshot_090012.jpg
    screenshot_090017.jpg
  2026-03-19.log                ← activity log (same retention as screenshots)
```

### Privacy

The following process names have their window titles **redacted** to `[redacted]` in the activity log:
KeePass, KeePassXC, 1Password, Bitwarden, LastPass, Dashlane, Signal, Telegram, WhatsApp, PuTTY, SecureCRT, and Windows credential UI processes.

Elevated processes (e.g. Task Manager running as Administrator) are logged as `unknown-elevated "[elevated]"`.

## Contributing

Contributions are welcome. Please open an issue or pull request.

## License

MIT — see [LICENSE](LICENSE).
