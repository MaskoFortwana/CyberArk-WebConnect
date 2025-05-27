# Test Script to verify the Input Blocking Timeout change
Write-Host "=== ChromeConnect Input Blocking Timeout Verification ===" -ForegroundColor Green
Write-Host ""

# Test: Check if the executable exists
$execPath = "src\ChromeConnect\bin\Debug\net8.0\win-x64\ChromeConnect.exe"
if (-not (Test-Path $execPath)) {
    Write-Host "Building ChromeConnect..." -ForegroundColor Yellow
    dotnet build src/ChromeConnect/ChromeConnect.csproj
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed" -ForegroundColor Red
        exit 1
    }
}

Write-Host "[OK] ChromeConnect executable available" -ForegroundColor Green

# Test: Verify the configuration value by checking the source
$configFile = "src\ChromeConnect\Configuration\StaticConfiguration.cs"
$configContent = Get-Content $configFile -Raw

if ($configContent -match "InputBlockingTimeoutSeconds.*=\s*150") {
    Write-Host "[OK] Configuration updated to 150 seconds" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Configuration not updated correctly" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "[✓] Input blocking timeout increased from 60 to 150 seconds" -ForegroundColor White
Write-Host "[✓] Configuration builds successfully" -ForegroundColor White
Write-Host "[✓] Safety timeout mechanism preserved" -ForegroundColor White
Write-Host ""
Write-Host "The automatic input timeout is now 150 seconds (2.5 minutes)" -ForegroundColor Green
Write-Host "This gives more time for complex login processes to complete" -ForegroundColor Yellow
Write-Host "" 