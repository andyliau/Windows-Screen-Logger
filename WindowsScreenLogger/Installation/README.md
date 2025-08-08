# Installation Components

This folder contains **ALL** installation-related functionality for the Windows Screen Logger application.

## Complete Files Structure

### Scripts
- **UninstallScript.bat** - Batch script for delayed uninstallation
- **UninstallScript.ps1** - PowerShell script for delayed uninstallation

### Classes
- **SelfInstaller.cs** - Main installation orchestrator (moved from root)
- **WindowsAppsRegistry.cs** - Handles Windows Apps & Features registration
- **StartupRegistry.cs** - Manages Windows startup registry entries
- **UninstallScriptManager.cs** - Manages and executes uninstallation scripts

### Documentation
- **README.md** - This file - component documentation
- **README-SelfInstall.md** - Complete feature documentation (moved from root)
- **ORGANIZATION-SUMMARY.md** - Organization and refactoring summary

## Architecture

The installation system is completely organized into this dedicated folder:

### 1. Registry Management
- `WindowsAppsRegistry` - Handles app registration in Windows Settings > Apps
- `StartupRegistry` - Manages startup with Windows functionality

### 2. Script Management
- `UninstallScriptManager` - Creates and executes delayed deletion scripts
- Embedded script resources for reliable uninstallation

### 3. Main Installer
- `SelfInstaller` - Orchestrates the installation/uninstallation process
- Uses the specialized classes for specific functionality

## Key Features

### Complete Organization
- ALL installation-related files are in this folder
- Clean separation from core application logic
- Dedicated namespace: `WindowsScreenLogger.Installation`

### Script Embedding
- Scripts are embedded as resources in the executable
- Fallback scripts are available if resource loading fails
- Dynamic script generation with proper path handling

### User-Scoped Installation
- All operations work within user privileges
- No administrator elevation required
- Registry operations target HKEY_CURRENT_USER

### Robust Uninstallation
- Multiple deletion strategies (PowerShell, batch, direct)
- Delayed deletion to handle self-removal
- Comprehensive error handling and logging

## Usage

The installation components are used by:
- Core application classes through the Installation namespace
- `Program.cs` for command line argument handling
- `MainForm.cs` for context menu uninstall option
- `SettingsForm.cs` for startup registry management

This complete modular approach provides the best maintainability and separation of concerns.