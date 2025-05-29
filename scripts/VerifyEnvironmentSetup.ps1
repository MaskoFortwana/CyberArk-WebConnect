# ChromeConnect Environment Setup Verification Script
# This script verifies that the DOTNET_BUNDLE_EXTRACT_BASE_DIR environment setup is working correctly

param(
    [string]$ExtractPath = "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect",
    [switch]$Detailed = $false
)

# Initialize result tracking
$results = @{
    SessionVariable = $false
    SystemVariable = $false
    DirectoryExists = $false
    DirectoryWritable = $false
    Overall = $false
}

function Write-StatusLine {
    param(
        [string]$Test,
        [bool]$Status,
        [string]$Details = ""
    )
    
    $statusSymbol = if ($Status) { "[OK]" } else { "[FAIL]" }
    $statusColor = if ($Status) { "Green" } else { "Red" }
    
    Write-Host "$statusSymbol $Test" -ForegroundColor $statusColor
    if ($Details -and $Detailed) {
        Write-Host "    $Details" -ForegroundColor Gray
    }
}

# Header
Write-Host "ChromeConnect Environment Setup Verification" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Target extraction path: $ExtractPath" -ForegroundColor White
Write-Host ""

# Test 1: Check session-level environment variable
Write-Host "1. Session Environment Variable Check" -ForegroundColor Yellow
try {
    $sessionValue = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
    if ($sessionValue -eq $ExtractPath) {
        $results.SessionVariable = $true
        Write-StatusLine "Session variable correctly set" $true "Value: $sessionValue"
    }
    elseif ($sessionValue) {
        Write-StatusLine "Session variable set but incorrect" $false "Expected: $ExtractPath, Got: $sessionValue"
    }
    else {
        Write-StatusLine "Session variable not set" $false "Run SetEnvironmentVariable.ps1 for current session"
    }
}
catch {
    Write-StatusLine "Session variable check failed" $false $_.Exception.Message
}

# Test 2: Check system-level environment variable
Write-Host "`n2. System Environment Variable Check" -ForegroundColor Yellow
try {
    $systemValue = [System.Environment]::GetEnvironmentVariable('DOTNET_BUNDLE_EXTRACT_BASE_DIR', [System.EnvironmentVariableTarget]::Machine)
    if ($systemValue -eq $ExtractPath) {
        $results.SystemVariable = $true
        Write-StatusLine "System variable correctly set" $true "Value: $systemValue"
    }
    elseif ($systemValue) {
        Write-StatusLine "System variable set but incorrect" $false "Expected: $ExtractPath, Got: $systemValue"
    }
    else {
        Write-StatusLine "System variable not set" $false "Run SetSystemEnvironmentVariable.ps1 as Administrator"
    }
}
catch {
    Write-StatusLine "System variable check failed" $false $_.Exception.Message
}

# Test 3: Check if directory exists
Write-Host "`n3. Extraction Directory Check" -ForegroundColor Yellow
try {
    if (Test-Path $ExtractPath) {
        $results.DirectoryExists = $true
        Write-StatusLine "Extraction directory exists" $true "Path: $ExtractPath"
        
        # Get directory info for detailed output
        if ($Detailed) {
            $dirInfo = Get-Item $ExtractPath
            Write-Host "    Created: $($dirInfo.CreationTime)" -ForegroundColor Gray
            Write-Host "    Attributes: $($dirInfo.Attributes)" -ForegroundColor Gray
        }
    }
    else {
        Write-StatusLine "Extraction directory does not exist" $false "Will be created automatically on first DLL extraction"
    }
}
catch {
    Write-StatusLine "Directory check failed" $false $_.Exception.Message
}

# Test 4: Check directory write permissions
Write-Host "`n4. Directory Permissions Check" -ForegroundColor Yellow
try {
    # Create the directory if it doesn't exist for permission testing
    if (-not (Test-Path $ExtractPath)) {
        New-Item -Path $ExtractPath -ItemType Directory -Force | Out-Null
        Write-Host "    Created directory for permission testing" -ForegroundColor Gray
    }
    
    # Test write permission
    $testFile = Join-Path $ExtractPath "write_test_$(Get-Random).tmp"
    "test" | Out-File -FilePath $testFile -ErrorAction Stop
    Remove-Item $testFile -ErrorAction SilentlyContinue
    
    $results.DirectoryWritable = $true
    Write-StatusLine "Directory is writable" $true "Write permissions verified"
}
catch {
    Write-StatusLine "Directory write test failed" $false "Error: $($_.Exception.Message)"
    if ($_.Exception.Message -like "*Access*denied*") {
        Write-Host "    Tip: Run as Administrator or check directory permissions" -ForegroundColor Yellow
    }
}

# Test 5: Check for existing DLL extractions
Write-Host "`n5. Existing DLL Extraction Check" -ForegroundColor Yellow
try {
    if (Test-Path $ExtractPath) {
        $subDirs = Get-ChildItem $ExtractPath -Directory -ErrorAction SilentlyContinue
        if ($subDirs.Count -gt 0) {
            Write-StatusLine "Found existing DLL extraction directories" $true "Count: $($subDirs.Count)"
            if ($Detailed) {
                foreach ($dir in $subDirs) {
                    $dllCount = (Get-ChildItem $dir.FullName -Filter "*.dll" -ErrorAction SilentlyContinue).Count
                    Write-Host "    $($dir.Name) - $dllCount DLL files" -ForegroundColor Gray
                }
            }
        }
        else {
            Write-StatusLine "No existing DLL extractions found" $true "Will be created on first application run"
        }
    }
    else {
        Write-StatusLine "Cannot check for existing extractions" $false "Directory does not exist"
    }
}
catch {
    Write-StatusLine "DLL extraction check failed" $false $_.Exception.Message
}

# Overall assessment
Write-Host "`n6. Overall Assessment" -ForegroundColor Yellow
$readyForProduction = $results.SystemVariable -and $results.DirectoryExists -and $results.DirectoryWritable
$readyForDevelopment = ($results.SessionVariable -or $results.SystemVariable) -and $results.DirectoryWritable

if ($readyForProduction) {
    $results.Overall = $true
    Write-StatusLine "Setup ready for production" $true "System variable set, directory configured"
}
elseif ($readyForDevelopment) {
    Write-StatusLine "Setup ready for development" $true "Session variable set, can be used immediately"
    Write-Host "    Recommendation: Set system variable for production deployment" -ForegroundColor Yellow
}
else {
    Write-StatusLine "Setup incomplete" $false "Environment variable or directory issues detected"
}

# Summary and recommendations
Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "--------" -ForegroundColor Cyan

if ($results.Overall) {
    Write-Host "[SUCCESS] Environment setup is complete and ready for use" -ForegroundColor Green
}
else {
    Write-Host "[WARNING] Environment setup needs attention" -ForegroundColor Yellow
    
    if (-not $results.SessionVariable -and -not $results.SystemVariable) {
        Write-Host "• Run SetEnvironmentVariable.ps1 for immediate use" -ForegroundColor Yellow
        Write-Host "• Run SetSystemEnvironmentVariable.ps1 as Administrator for persistent setup" -ForegroundColor Yellow
    }
    elseif (-not $results.SystemVariable) {
        Write-Host "• Run SetSystemEnvironmentVariable.ps1 as Administrator for production deployment" -ForegroundColor Yellow
    }
    
    if (-not $results.DirectoryWritable) {
        Write-Host "• Check directory permissions or run setup as Administrator" -ForegroundColor Yellow
    }
}

# Next steps
Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "-----------" -ForegroundColor Cyan
if ($readyForProduction -or $readyForDevelopment) {
    Write-Host "1. Launch ChromeConnect using:" -ForegroundColor White
    Write-Host "   PowerShell -ExecutionPolicy Bypass -File 'scripts\StartApplication.ps1'" -ForegroundColor Gray
    Write-Host "2. Verify DLL extraction location after application starts" -ForegroundColor White
    Write-Host "3. Check AppLocker logs for any remaining DLL blocking issues" -ForegroundColor White
}
else {
    Write-Host "1. Complete environment variable setup (see recommendations above)" -ForegroundColor White
    Write-Host "2. Re-run this verification script" -ForegroundColor White
    Write-Host "3. Test application launch after setup completion" -ForegroundColor White
}

# Return exit code based on overall status
if ($results.Overall -or $readyForDevelopment) {
    exit 0
}
else {
    exit 1
} 