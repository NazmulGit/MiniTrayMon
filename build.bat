@echo off
setlocal
:: ============================================================
::  MiniTrayMon - Release Build Script
::  Compiles all .cs sources into a single optimized .exe
::  using the .NET Framework 4.x C# compiler (ships with Windows)
:: ============================================================

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "SRC_DIR=%~dp0src"
set "OUT_DIR=%~dp0release"
set "OUT_EXE=%OUT_DIR%\MiniTrayMon.exe"

if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"

if not exist "%CSC%" (
    echo [ERROR] C# compiler not found at: %CSC%
    exit /b 1
)

echo.
echo [BUILD] MiniTrayMon Release
echo   Compiler : %CSC%
echo   Sources  : %SRC_DIR%\*.cs
echo   Output   : %OUT_EXE%
echo.

:: Collect all .cs files
set "CS_FILES="
:: PixelVerify.cs and TestHarness.cs are dev tools with their own Main(); keep them out of the app exe
for %%f in ("%SRC_DIR%\*.cs") do if /i not "%%~nf"=="PixelVerify" if /i not "%%~nf"=="TestHarness" call :append "%%f"

"%CSC%" /target:winexe /out:"%OUT_EXE%" /nologo /optimize+ /warn:4 ^
    /define:RELEASE;TRACE /main:MiniTrayMon.Program ^
    %CS_FILES%

if errorlevel 1 (
    echo.
    echo [FAILED] Build returned errors. See above.
    exit /b 1
)

echo.
echo [OK] Build successful.
echo      %OUT_EXE%
for %%I in ("%OUT_EXE%") do echo      Size: %%~zI bytes
echo.
endlocal
exit /b 0

:append
set "CS_FILES=%CS_FILES% %~1"
goto :eof
