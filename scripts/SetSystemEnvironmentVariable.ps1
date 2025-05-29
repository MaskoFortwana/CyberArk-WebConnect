# Set System-Level Environment Variable for ChromeConnect DLL Extraction
# This script sets the DOTNET_BUNDLE_EXTRACT_BASE_DIR environment variable at machine level
# Requires administrative privileges to modify system environment variables

param(
    [string]$ExtractPath = "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect"
)

# Check if running as administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

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
Write-Host "ChromeConnect System Environment Variable Setup" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

# Check administrative privileges
if (-not (Test-Administrator)) {
    Write-Error "This script requires administrative privileges to set system environment variables."
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Yellow
    Write-Host "Or use the session-only script: SetEnvironmentVariable.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host "Running with administrative privileges" -ForegroundColor Green

# Validate and create the target directory
if (-not (Ensure-Directory -Path $ExtractPath)) {
    Write-Error "Cannot proceed without valid extraction directory"
    exit 1
}

# Set the environment variable at machine level
try {
    Write-Host "Setting system environment variable..." -ForegroundColor Yellow
    [System.Environment]::SetEnvironmentVariable(
        'DOTNET_BUNDLE_EXTRACT_BASE_DIR', 
        $ExtractPath, 
        [System.EnvironmentVariableTarget]::Machine
    )
    Write-Host "System environment variable DOTNET_BUNDLE_EXTRACT_BASE_DIR set successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to set system environment variable: $($_.Exception.Message)"
    exit 1
}

# Verify the variable was set at machine level
try {
    $systemValue = [System.Environment]::GetEnvironmentVariable(
        'DOTNET_BUNDLE_EXTRACT_BASE_DIR', 
        [System.EnvironmentVariableTarget]::Machine
    )
    
    if ($systemValue -eq $ExtractPath) {
        Write-Host "[SUCCESS] System environment variable verified successfully" -ForegroundColor Green
        Write-Host "Value: $systemValue" -ForegroundColor White
    }
    else {
        Write-Error "[FAILED] System environment variable verification failed"
        Write-Host "Expected: $ExtractPath" -ForegroundColor Red
        Write-Host "Actual: $systemValue" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Error "Failed to verify system environment variable: $($_.Exception.Message)"
    exit 1
}

# Also set for current session
try {
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $ExtractPath
    Write-Host "[SUCCESS] Current session environment variable also set" -ForegroundColor Green
}
catch {
    Write-Warning "Failed to set current session variable, but system variable is set correctly"
}

Write-Host "`nSystem environment setup completed successfully!" -ForegroundColor Green
Write-Host "The DLL extraction will now use: $ExtractPath" -ForegroundColor White
Write-Host "`nNOTE: New processes will automatically use this path." -ForegroundColor Yellow
Write-Host "Existing processes may need to be restarted to see the new environment variable." -ForegroundColor Yellow 