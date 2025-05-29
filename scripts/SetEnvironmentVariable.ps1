# Set Environment Variable for ChromeConnect DLL Extraction
# This script sets the DOTNET_BUNDLE_EXTRACT_BASE_DIR environment variable
# to redirect .NET single-file DLL extraction from user temp to approved directory

param(
    [string]$ExtractPath = "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect"
)

# Function to create directory if it doesn't exist
function Ensure-Directory {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        try {
            New-Item -Path $Path -ItemType Directory -Force | Out-Null
            Write-Host "Created directory: $Path" -ForegroundColor Green
        }
        catch {
            Write-Error "Failed to create directory: $Path. Error: $($_.Exception.Message)"
            return $false
        }
    }
    else {
        Write-Host "Directory already exists: $Path" -ForegroundColor Yellow
    }
    return $true
}

# Main execution
Write-Host "ChromeConnect DLL Extraction Environment Setup" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Validate and create the target directory
if (-not (Ensure-Directory -Path $ExtractPath)) {
    Write-Error "Cannot proceed without valid extraction directory"
    exit 1
}

# Set the environment variable for current session
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
    Write-Host "[SUCCESS] Environment variable verified successfully" -ForegroundColor Green
}
else {
    Write-Error "[FAILED] Environment variable verification failed"
    exit 1
}

# Check directory permissions
try {
    $testFile = Join-Path $ExtractPath "test_permissions.tmp"
    "test" | Out-File -FilePath $testFile -Encoding UTF8
    Remove-Item $testFile -Force
    Write-Host "[SUCCESS] Directory permissions verified - write access confirmed" -ForegroundColor Green
}
catch {
    Write-Warning "[WARNING] Directory permissions may be insufficient: $($_.Exception.Message)"
    Write-Host "Consider running as administrator or adjusting directory permissions" -ForegroundColor Yellow
}

Write-Host "`nEnvironment setup completed successfully!" -ForegroundColor Green
Write-Host "The DLL extraction will now use: $ExtractPath" -ForegroundColor White 