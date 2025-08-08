@echo off
rem Windows Screen Logger Uninstaller
rem This script removes the installation directory after the application exits

rem Wait for the application to fully exit
timeout /t 3 /nobreak >nul

rem Delete the installation directory (parent folder) with all contents
if exist "%~1" (
    echo Removing installation directory: %~1
    rmdir /s /q "%~1"
    if exist "%~1" (
        echo Failed to remove directory with rmdir, trying alternative method...
        rd /s /q "%~1"
    )
    if exist "%~1" (
        echo Warning: Installation directory could not be completely removed
    ) else (
        echo Installation directory successfully removed
    )
)

rem Delete this batch file
del "%~f0"