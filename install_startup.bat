@echo off
:: ============================================================
::  MiniTrayMon - Startup Shortcut Installer
::  Creates a shortcut in the Windows Startup folder so that
::  MiniTrayMon.exe launches automatically on login.
::
::  Usage: double-click this .bat, OR run from a command prompt.
::  Safe to re-run; overwrites any previous shortcut.
:: ============================================================
setlocal enabledelayedexpansion

:: --- locate MiniTrayMon.exe ----------------------------------
:: Prefer same-folder, then release\ subfolder, then parent\release.
set "EXE="
if exist "%~dp0MiniTrayMon.exe"            set "EXE=%~dp0MiniTrayMon.exe"
if not defined EXE if exist "%~dp0release\MiniTrayMon.exe" set "EXE=%~dp0release\MiniTrayMon.exe"
if not defined EXE if exist "%~dp0..\release\MiniTrayMon.exe" set "EXE=%~dp0..\release\MiniTrayMon.exe"

if not defined EXE (
    echo [ERROR] MiniTrayMon.exe was not found.
    echo         Place this script next to MiniTrayMon.exe, or in the parent
    echo         folder of a 'release' subdirectory containing the exe.
    echo.
    echo Press any key to close...
    pause >nul
    exit /b 1
)

:: --- normalize path (resolve ..\ etc.) -----------------------
for %%I in ("%EXE%") do set "EXE=%%~fI"

:: --- target startup folder -----------------------------------
set "STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
if not exist "%STARTUP%" mkdir "%STARTUP%" 2>nul
set "LNK=%STARTUP%\MiniTrayMon.lnk"

:: --- create shortcut via PowerShell + WScript.Shell COM ------
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ws = New-Object -ComObject WScript.Shell;" ^
    "$sc = $ws.CreateShortcut('%LNK%');" ^
    "$sc.TargetPath = '%EXE%';" ^
    "$sc.WorkingDirectory = (Split-Path '%EXE%');" ^
    "$sc.WindowStyle = 7;" ^
    "$sc.Description = 'MiniTrayMon - system tray monitor';" ^
    "$sc.Save();"

if errorlevel 1 (
    echo [ERROR] Failed to create shortcut.
    echo         PowerShell is required (present on all Windows 10/11 installs).
    echo.
    echo Press any key to close...
    pause >nul
    exit /b 1
)

:: --- success -------------------------------------------------
echo.
echo [OK] MiniTrayMon startup shortcut created.
echo.
echo     Target  : %EXE%
echo     Shortcut: %LNK%
echo.
echo MiniTrayMon will launch automatically the next time you log in.
echo.
echo To remove later: open shell:startup in File Explorer and delete
echo                  the MiniTrayMon.lnk shortcut.
echo.
endlocal
