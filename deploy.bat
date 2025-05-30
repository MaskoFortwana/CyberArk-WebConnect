@echo off
setlocal enabledelayedexpansion

:: WebConnect Deployment Script
:: This script helps deploy WebConnect with ChromeDriver to a target directory

echo.
echo ==================== WebConnect Deployment Script ====================
echo.

:: Default values
set "TARGET_DIR=C:\WebConnect"
set "SOURCE_EXE=publish\WebConnect.exe"
set "CHROMEDRIVER_URL=https://chromedriver.storage.googleapis.com/LATEST_RELEASE"

:: Parse command line arguments
:parse_args
if "%~1"=="" goto :check_args
if /i "%~1"=="--target" (
    set "TARGET_DIR=%~2"
    shift
    shift
    goto :parse_args
)
if /i "%~1"=="--help" (
    goto :show_help
)
shift
goto :parse_args

:check_args
echo Target Directory: %TARGET_DIR%
echo Source Executable: %SOURCE_EXE%
echo.

:: Check if source executable exists
if not exist "%SOURCE_EXE%" (
    echo ERROR: WebConnect.exe not found at %SOURCE_EXE%
    echo Please run the build script first: publish.ps1
    exit /b 1
)

:: Create target directory
echo Creating target directory...
if not exist "%TARGET_DIR%" (
    mkdir "%TARGET_DIR%" 2>nul
    if errorlevel 1 (
        echo ERROR: Failed to create directory %TARGET_DIR%
        echo Please run as administrator or choose a different location
        exit /b 1
    )
)

:: Copy WebConnect.exe
echo Copying WebConnect.exe...
copy "%SOURCE_EXE%" "%TARGET_DIR%\" >nul
if errorlevel 1 (
    echo ERROR: Failed to copy WebConnect.exe
    exit /b 1
)

:: Copy README if it exists
if exist "publish\README.md" (
    echo Copying README.md...
    copy "publish\README.md" "%TARGET_DIR%\" >nul
)

:: Check for ChromeDriver
echo.
echo Checking for ChromeDriver...
if exist "%TARGET_DIR%\chromedriver.exe" (
    echo ChromeDriver already exists in target directory
) else (
    echo WARNING: ChromeDriver.exe not found in %TARGET_DIR%
    echo.
    echo WebConnect requires ChromeDriver.exe to be in the same directory.
    echo Please download ChromeDriver from:
    echo https://chromedriver.chromium.org/downloads
    echo.
    echo Or use WebDriverManager to auto-download compatible version:
    echo WebConnect will attempt to download ChromeDriver automatically on first run.
)

:: Create logs directory
echo Creating logs directory...
if not exist "%TARGET_DIR%\logs" mkdir "%TARGET_DIR%\logs" 2>nul

:: Create screenshots directory  
echo Creating screenshots directory...
if not exist "%TARGET_DIR%\screenshots" mkdir "%TARGET_DIR%\screenshots" 2>nul

echo.
echo ==================== Deployment Complete ====================
echo.
echo WebConnect has been deployed to: %TARGET_DIR%
echo.
echo To run WebConnect:
echo   cd "%TARGET_DIR%"
echo   WebConnect.exe --help
echo.
echo To add to PATH (run as administrator):
echo   setx PATH "%%PATH%%;%TARGET_DIR%" /M
echo.
echo ============================================================
echo.

goto :end

:show_help
echo.
echo WebConnect Deployment Script
echo.
echo Usage: deploy.bat [options]
echo.
echo Options:
echo   --target DIR     Target deployment directory (default: C:\WebConnect)
echo   --help           Show this help message
echo.
echo Examples:
echo   deploy.bat
echo   deploy.bat --target "C:\Program Files\WebConnect"
echo   deploy.bat --target "D:\Tools\WebConnect"
echo.
echo Requirements:
echo   - WebConnect.exe must exist in publish\ directory
echo   - Run publish.ps1 first to build the executable
echo   - Administrator rights may be required for system directories
echo.

:end
endlocal 