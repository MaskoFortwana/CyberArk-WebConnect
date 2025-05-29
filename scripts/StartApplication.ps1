# ChromeConnect Application Startup Wrapper
# This script ensures the DOTNET_BUNDLE_EXTRACT_BASE_DIR environment variable is set
# before launching the ChromeConnect application

param(
    [string]$ApplicationPath,
    [string]$ExtractPath = "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect",
    [string[]]$Arguments = @(),
    [switch]$Wait = $false,
    [switch]$NoWindow = $false
)

# Function to create directory if it doesn't exist
function Ensure-Directory {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        try {
            New-Item -Path $Path -ItemType Directory -Force | Out-Null
            Write-Host "Created extraction directory: $Path" -ForegroundColor Green
            return $true
        }
        catch {
            Write-Error "Failed to create extraction directory: $Path. Error: $($_.Exception.Message)"
            return $false
        }
    }
    return $true
}

# Function to find ChromeConnect executable
function Find-ChromeConnectExecutable {
    # Check common locations
    $possiblePaths = @(
        ".\publish\ChromeConnect.exe",
        ".\ChromeConnect.exe",
        ".\bin\Release\net8.0\ChromeConnect.exe",
        ".\bin\Debug\net8.0\ChromeConnect.exe"
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            return (Resolve-Path $path).Path
        }
    }
    
    return $null
}

# Main execution
Write-Host "ChromeConnect Application Launcher" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan

# Determine application path if not provided
if (-not $ApplicationPath) {
    $ApplicationPath = Find-ChromeConnectExecutable
    if (-not $ApplicationPath) {
        Write-Error "ChromeConnect executable not found. Please specify the path using -ApplicationPath parameter."
        Write-Host "Expected locations checked:" -ForegroundColor Yellow
        Write-Host "  - .\publish\ChromeConnect.exe" -ForegroundColor Yellow
        Write-Host "  - .\ChromeConnect.exe" -ForegroundColor Yellow
        Write-Host "  - .\bin\Release\net8.0\ChromeConnect.exe" -ForegroundColor Yellow
        Write-Host "  - .\bin\Debug\net8.0\ChromeConnect.exe" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "Found ChromeConnect executable: $ApplicationPath" -ForegroundColor Green
}
else {
    if (-not (Test-Path $ApplicationPath)) {
        Write-Error "Application path not found: $ApplicationPath"
        exit 1
    }
    Write-Host "Using specified application path: $ApplicationPath" -ForegroundColor Green
}

# Ensure extraction directory exists
Write-Host "Checking extraction directory..." -ForegroundColor Yellow
if (-not (Ensure-Directory -Path $ExtractPath)) {
    Write-Error "Cannot proceed without valid extraction directory"
    exit 1
}

# Set the environment variable for the application session
Write-Host "Setting environment variable..." -ForegroundColor Yellow
try {
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $ExtractPath
    Write-Host "Environment variable DOTNET_BUNDLE_EXTRACT_BASE_DIR set to: $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR" -ForegroundColor Green
}
catch {
    Write-Error "Failed to set environment variable: $($_.Exception.Message)"
    exit 1
}

# Verify the variable was set correctly
if ($env:DOTNET_BUNDLE_EXTRACT_BASE_DIR -eq $ExtractPath) {
    Write-Host "[SUCCESS] Environment variable verified" -ForegroundColor Green
}
else {
    Write-Error "[FAILED] Environment variable verification failed"
    exit 1
}

# Prepare launch parameters
$processParams = @{
    FilePath = $ApplicationPath
    PassThru = $true
}

if ($Arguments.Count -gt 0) {
    $processParams.ArgumentList = $Arguments
    Write-Host "Application arguments: $($Arguments -join ' ')" -ForegroundColor White
}

if ($NoWindow) {
    $processParams.WindowStyle = 'Hidden'
    Write-Host "Application will run hidden (no window)" -ForegroundColor Yellow
}

# Launch the application
Write-Host "`nLaunching ChromeConnect..." -ForegroundColor Green
Write-Host "Application: $ApplicationPath" -ForegroundColor White
Write-Host "DLL Extraction Path: $ExtractPath" -ForegroundColor White

try {
    $process = Start-Process @processParams
    
    if ($process) {
        Write-Host "[SUCCESS] ChromeConnect launched successfully" -ForegroundColor Green
        Write-Host "Process ID: $($process.Id)" -ForegroundColor White
        
        if ($Wait) {
            Write-Host "Waiting for application to exit..." -ForegroundColor Yellow
            $process.WaitForExit()
            Write-Host "Application exited with code: $($process.ExitCode)" -ForegroundColor White
            exit $process.ExitCode
        }
        else {
            Write-Host "Application started in background" -ForegroundColor Green
        }
    }
    else {
        Write-Error "Failed to start application - no process returned"
        exit 1
    }
}
catch {
    Write-Error "Failed to launch application: $($_.Exception.Message)"
    exit 1
}

Write-Host "`nApplication startup completed!" -ForegroundColor Green 