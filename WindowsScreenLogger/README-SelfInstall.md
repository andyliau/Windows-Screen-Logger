# Windows Screen Logger - Self-Installation Feature

## Overview
The Windows Screen Logger now includes a comprehensive self-installation system that provides a professional installation experience similar to commercial applications.

## Features

### Automatic Installation Prompt
- When the application is run from any location other than Program Files, it automatically prompts the user for installation
- Users can choose to install or continue running from the current location

### Installation Process
- **Elevation Handling**: Automatically requests administrator privileges when needed
- **Program Files Installation**: Copies the executable to `C:\Program Files\Windows Screen Logger\`
- **Windows Apps Registration**: Registers the application in Windows Apps & Features
- **Startup Integration**: Properly configures startup with Windows using the installed location
- **Automatic Restart**: Launches the installed version after successful installation

### Windows Apps Integration
- Appears in Windows Settings > Apps & features
- Includes proper metadata (version, publisher, install date, size)
- Provides standard uninstall functionality
- Supports both regular and quiet uninstallation

### Uninstallation
- **Context Menu Option**: Right-click system tray icon > Uninstall (when running from installed location)
- **Windows Apps**: Standard uninstall through Windows Settings > Apps & features
- **Command Line**: `WindowsScreenLogger.exe /uninstall` or `/uninstall /quiet`
- **Complete Removal**: Removes files, registry entries, and startup configuration
- **Self-Deletion Handling**: Uses delayed deletion mechanisms to properly remove the executable while it's running

## Command Line Arguments

| Argument | Description |
|----------|-------------|
| `/install` | Forces installation (requires admin privileges) |
| `/uninstall` | Uninstalls the application (requires admin privileges) |
| `/uninstall /quiet` | Silent uninstallation without user prompts |

## Installation Locations

### Executable Location
- **Installed**: `C:\Program Files\Windows Screen Logger\WindowsScreenLogger.exe`
- **Screenshots**: `%USERPROFILE%\WindowsScreenLogger\` (unchanged)

### Registry Entries
- **Startup**: `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- **Apps Registration**: `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{B3E7C6A8-9F2D-4E5A-B1C3-8D7F6E9A2B4C}`

## User Experience

### First Run (from any location)
1. Application starts normally
2. Shows installation prompt dialog
3. If user accepts:
   - Requests admin privileges
   - Copies to Program Files
   - Registers in Windows Apps
   - Restarts from installed location

### Subsequent Runs (from installed location)
1. Runs normally without prompts
2. Context menu includes uninstall option
3. Can be uninstalled through Windows Settings

### Portable Mode
- Users can decline installation and continue using from any location
- Full functionality maintained in portable mode
- Startup registration works with current location

## Technical Implementation

### Security
- Proper UAC elevation handling
- Administrator privilege checks
- Secure file operations with error handling

### Reliability
- Mutex-based single instance enforcement
- Graceful error handling and user feedback
- Registry operations with proper cleanup

### Self-Deletion During Uninstall
- **Problem**: Applications cannot delete themselves while running
- **Solution**: Delayed deletion using external scripts
- **Primary Method**: PowerShell script with execution policy bypass
- **Fallback Method**: Batch file execution
- **Safety Checks**: Prevents uninstallation when not actually installed

### Compatibility
- Windows 11 optimized (minimum Windows 10 version 22000)
- Self-contained deployment (no .NET runtime required)
- Single executable with embedded dependencies

## Troubleshooting

### Uninstall Issues
If you encounter "Access denied" errors during uninstallation:
1. The application uses delayed deletion mechanisms
2. Files are removed after the application fully exits
3. If manual cleanup is needed, delete: `C:\Program Files\Windows Screen Logger\`

### Manual Uninstall
If automatic uninstall fails:
1. Delete folder: `C:\Program Files\Windows Screen Logger\`
2. Remove registry key: `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{B3E7C6A8-9F2D-4E5A-B1C3-8D7F6E9A2B4C}`
3. Remove startup entry: `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\Windows Screen Logger`

## Building and Deployment

```bash
# Build release version
dotnet publish -c Release

# Output location
WindowsScreenLogger\bin\Release\net9.0-windows10.0.22000.0\win-x64\publish\WindowsScreenLogger.exe
```

The published executable can be distributed directly - no installer needed. Users simply run it and choose whether to install when prompted.