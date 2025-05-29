# WebConnect Application Startup Wrapper
# This script ensures the DOTNET_BUNDLE_EXTRACT_BASE_DIR environment variable is set
# before launching the WebConnect application

param(
    [string]$ApplicationPath = "",
    [string]$ExtractPath = "C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect",
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

# Function to find WebConnect executable
function Find-WebConnectExecutable {
    $searchPaths = @(
        ".\publish\WebConnect.exe",
        ".\WebConnect.exe",
        ".\bin\Release\net8.0\WebConnect.exe",
        ".\bin\Debug\net8.0\WebConnect.exe"
    )
    
    foreach ($path in $searchPaths) {
        if (Test-Path $path) {
            return (Resolve-Path $path).Path
        }
    }
    
    return $null
}

# Main execution
Write-Host "WebConnect Application Launcher" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan

# Find executable if not provided
$ApplicationPath = Find-WebConnectExecutable
if (-not $ApplicationPath -or -not (Test-Path $ApplicationPath)) {
    Write-Error "WebConnect executable not found. Please specify the path using -ApplicationPath parameter."
    Write-Host "Searched locations:" -ForegroundColor Yellow
    Write-Host "  - .\publish\WebConnect.exe" -ForegroundColor Yellow
    Write-Host "  - .\WebConnect.exe" -ForegroundColor Yellow
    Write-Host "  - .\bin\Release\net8.0\WebConnect.exe" -ForegroundColor Yellow
    Write-Host "  - .\bin\Debug\net8.0\WebConnect.exe" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found WebConnect executable: $ApplicationPath" -ForegroundColor Green

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
Write-Host "`nLaunching WebConnect..." -ForegroundColor Green
Write-Host "Application: $ApplicationPath" -ForegroundColor White
Write-Host "DLL Extraction Path: $ExtractPath" -ForegroundColor White

try {
    $process = Start-Process @processParams
    
    if ($process) {
        Write-Host "[SUCCESS] WebConnect launched successfully" -ForegroundColor Green
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