# PowerShell script to verify the ChromeConnect build

# Navigate to the project directory
Push-Location -Path "src/ChromeConnect"

Write-Host "Attempting to build ChromeConnect project (Release configuration)..."

# Run dotnet build
dotnet build --configuration Release

# Check the last exit code
if ($LASTEXITCODE -eq 0) {
    Write-Host "Build Succeeded!" -ForegroundColor Green
}
else {
    Write-Host "Build Failed! Exit code: $LASTEXITCODE" -ForegroundColor Red
    Pop-Location
    exit $LASTEXITCODE
}

# Return to the original location
Pop-Location

Write-Host "Build verification script completed." 