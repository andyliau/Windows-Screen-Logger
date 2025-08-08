# Windows Screen Logger Uninstaller
# This script removes the installation directory after the application exits

param(
    [string]$InstallPath
)

# Wait for the application to fully exit
Start-Sleep -Seconds 3

# Delete the installation directory (parent folder) with all contents
if (Test-Path $InstallPath) {
    Write-Host "Removing installation directory: $InstallPath"
    try {
        # Primary method: PowerShell Remove-Item with force and recurse
        Remove-Item $InstallPath -Recurse -Force -ErrorAction Stop
        Write-Host "Installation directory successfully removed using PowerShell"
    }
    catch {
        Write-Host "Failed to remove installation directory with PowerShell: $_"
        Write-Host "Trying alternative method with cmd..."
        try {
            # Fallback method: cmd rmdir
            Start-Process cmd -ArgumentList "/c rmdir /s /q `"$InstallPath`"" -WindowStyle Hidden -Wait -ErrorAction Stop
            if (-not (Test-Path $InstallPath)) {
                Write-Host "Installation directory successfully removed using cmd"
            } else {
                Write-Host "Warning: Installation directory could not be completely removed"
            }
        }
        catch {
            Write-Host "Failed to remove installation directory with cmd: $_"
            # Final attempt: try to remove files individually
            try {
                Get-ChildItem $InstallPath -Recurse -Force | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
                Remove-Item $InstallPath -Force -ErrorAction SilentlyContinue
                if (-not (Test-Path $InstallPath)) {
                    Write-Host "Installation directory successfully removed using individual file deletion"
                } else {
                    Write-Host "Warning: Some files may remain in the installation directory"
                }
            }
            catch {
                Write-Host "Final cleanup attempt failed: $_"
            }
        }
    }
} else {
    Write-Host "Installation directory not found: $InstallPath"
}

# Remove this script
Remove-Item $PSCommandPath -Force -ErrorAction SilentlyContinue