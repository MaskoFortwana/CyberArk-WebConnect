@echo off
REM ChromeConnect Simple Build Script
REM Usage: publish.bat [Version] [Configuration]
REM Example: publish.bat 1.0.1 Release

setlocal enabledelayedexpansion

REM Default parameters
set VERSION=%1
set CONFIGURATION=%2
if "%VERSION%"=="" set VERSION=1.0.0
if "%CONFIGURATION%"=="" set CONFIGURATION=Release

set PROJECT_PATH=src\ChromeConnect
set OUTPUT_DIR=publish
set RUNTIME_ID=win-x64

echo.
echo ==================== CHROMECONNECT BUILD SCRIPT ====================
echo Version: %VERSION%
echo Configuration: %CONFIGURATION%
echo Runtime: %RUNTIME_ID%
echo Output: %OUTPUT_DIR%
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET is not installed or not in PATH
    exit /b 1
)

echo INFO: .NET version:
dotnet --version

REM Clean output directory
if exist "%OUTPUT_DIR%" (
    echo INFO: Cleaning output directory...
    rmdir /s /q "%OUTPUT_DIR%"
)

REM Restore dependencies
echo INFO: Restoring dependencies...
dotnet restore "%PROJECT_PATH%" --verbosity quiet
if errorlevel 1 (
    echo ERROR: Failed to restore dependencies
    exit /b 1
)

REM Build application
echo INFO: Building application...
if /i "%CONFIGURATION%"=="Release" (
    dotnet publish "%PROJECT_PATH%" -c %CONFIGURATION% -r %RUNTIME_ID% --self-contained true ^
        -p:PublishReadyToRun=true ^
        -o "%OUTPUT_DIR%" ^
        --verbosity minimal
) else (
    dotnet publish "%PROJECT_PATH%" -c %CONFIGURATION% -r %RUNTIME_ID% --self-contained true ^
        -o "%OUTPUT_DIR%" ^
        --verbosity minimal
)

if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)

REM Copy additional files
if exist "README.md" copy "README.md" "%OUTPUT_DIR%\" >nul
if exist "LICENSE" copy "LICENSE" "%OUTPUT_DIR%\" >nul

REM Show results
echo.
echo ==================== BUILD SUMMARY ====================
if exist "%OUTPUT_DIR%\ChromeConnect.exe" (
    echo Status: SUCCESS
    echo Executable: %OUTPUT_DIR%\ChromeConnect.exe
    for %%A in ("%OUTPUT_DIR%\ChromeConnect.exe") do echo Size: %%~zA bytes
) else (
    echo Status: FAILED
)
echo ======================================================
echo.

if exist "%OUTPUT_DIR%\ChromeConnect.exe" (
    echo SUCCESS: Build completed successfully!
    echo Location: %OUTPUT_DIR%\ChromeConnect.exe
) else (
    echo ERROR: Build failed - executable not found
    exit /b 1
)

endlocal 