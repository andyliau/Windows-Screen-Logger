# Windows Screen Logger

Windows Screen Logger is a lightweight application designed to periodically capture screenshots of your desktop. It runs in the background with a system tray icon, allowing you to configure settings and manage captured screenshots easily.

## Features

- **Periodic Screen Capture**: Automatically captures screenshots at user-defined intervals.
- **Customizable Settings**:
  - Configure capture interval (in seconds).
  - Adjust image size (percentage of original resolution).
  - Set image quality (JPEG compression level).
- **Session Awareness**: Pauses screen capture when the session is locked and resumes when unlocked.
- **Automatic Cleanup**: Deletes screenshots older than a specified number of days.
- **Windows Integration**:
  - Option to start with Windows.
  - Uses the Windows Registry for startup configuration.
- **Single Instance Enforcement**: Ensures only one instance of the application runs at a time.
- **System Tray Icon**:
  - Access settings.
  - Open the folder containing saved screenshots.
  - Manually clean up old screenshots.
  - Exit the application.

## Requirements

- .NET 9.0 or later
- Windows 10 or later

## Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/AndyLi/WindowsScreenLogger.git
   ```
2. Open the solution in Visual Studio.
3. Build the project to generate the executable.

## Usage

1. Run the application. It will minimize to the system tray.
2. Right-click the tray icon to access the context menu:
   - **Settings**: Configure capture interval, image size, quality, and cleanup settings.
   - **Open Saved Image Folder**: Open the folder where screenshots are saved.
   - **Clean Old Screenshots**: Manually delete screenshots older than the configured number of days.
   - **Exit**: Close the application.
3. Screenshots are saved in the `WindowsScreenLogger` folder under your user profile directory, organized by date.

## Configuration

- **Settings**:
  - Accessible via the system tray icon.
  - Changes are saved automatically and applied immediately.
- **Startup**:
  - Enable or disable starting the application with Windows.

## Contributing

Contributions are welcome! Feel free to submit issues or pull requests to improve the project.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.