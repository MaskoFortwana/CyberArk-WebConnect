#!/usr/bin/env pwsh
<#
.SYNOPSIS
    ChromeConnect Deployment Script

.DESCRIPTION
    Automates the build and packaging process for ChromeConnect as a self-contained Windows executable.
    Supports version management, cleanup, optimization, and ZIP package creation.

.PARAMETER Version
    The version number to use for the build (default: 1.0.0)

.PARAMETER Configuration
    The build configuration to use: Debug or Release (default: Release)

.PARAMETER OutputDir
    The output directory for the published files (default: ./publish)

.PARAMETER CreateZip
    Whether to create a ZIP package of the published files (default: true)

.PARAMETER Clean
    Whether to clean the output directory before building (default: true)

.PARAMETER UpdateVersion
    Whether to update the version in the project file (default: true)

.PARAMETER RuntimeIdentifier
    The runtime identifier for the target platform (default: win-x64)

.EXAMPLE
    ./publish.ps1 -Version "1.0.1" -Configuration Release
    
.EXAMPLE
    ./publish.ps1 -Version "1.1.0" -Configuration Debug -CreateZip $false
    
.EXAMPLE
    ./publish.ps1 -Version "1.0.0" -RuntimeIdentifier "win-x86" -Configuration Release
#>

param (
    [string]$Version = "1.0.0",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-x86")]
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDir = "./publish",
    [bool]$CreateZip = $true,
    [bool]$Clean = $true,
    [bool]$UpdateVersion = $true
)

# Script configuration
$ProjectPath = "./src/ChromeConnect"
$ProjectFile = "$ProjectPath/ChromeConnect.csproj"
$ExecutableName = "ChromeConnect.exe"

# Colors for output
$ErrorColor = "Red"
$WarningColor = "Yellow"
$InfoColor = "Cyan"
$SuccessColor = "Green"

function Write-InfoMessage {
    param([string]$Message)
    Write-Host "INFO: $Message" -ForegroundColor $InfoColor
}

function Write-SuccessMessage {
    param([string]$Message)
    Write-Host "SUCCESS: $Message" -ForegroundColor $SuccessColor
}

function Write-WarningMessage {
    param([string]$Message)
    Write-Host "WARNING: $Message" -ForegroundColor $WarningColor
}

function Write-ErrorMessage {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor $ErrorColor
}

function Test-DotnetInstalled {
    try {
        $dotnetVersion = dotnet --version
        Write-InfoMessage ".NET version: $dotnetVersion"
        return $true
    }
    catch {
        Write-ErrorMessage ".NET is not installed or not in PATH"
        return $false
    }
}

function Update-ProjectVersion {
    param([string]$NewVersion)
    
    if (-not $UpdateVersion) {
        Write-InfoMessage "Skipping version update (UpdateVersion=$UpdateVersion)"
        return
    }
    
    if (-not (Test-Path $ProjectFile)) {
        Write-ErrorMessage "Project file not found: $ProjectFile"
        exit 1
    }
    
    Write-InfoMessage "Updating version to $NewVersion in $ProjectFile"
    
    try {
        $content = Get-Content $ProjectFile -Raw
        
        # Update various version tags
        $content = $content -replace '<Version>.*?</Version>', "<Version>$NewVersion</Version>"
        $content = $content -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$NewVersion.0</AssemblyVersion>"
        $content = $content -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$NewVersion.0</FileVersion>"
        
        Set-Content -Path $ProjectFile -Value $content -NoNewline
        Write-SuccessMessage "Version updated successfully"
    }
    catch {
        Write-ErrorMessage "Failed to update version: $($_.Exception.Message)"
        exit 1
    }
}

function Clean-OutputDirectory {
    if (-not $Clean) {
        Write-InfoMessage "Skipping cleanup (Clean=$Clean)"
        return
    }
    
    if (Test-Path $OutputDir) {
        Write-InfoMessage "Cleaning output directory: $OutputDir"
        try {
            Remove-Item -Path $OutputDir -Recurse -Force
            Write-SuccessMessage "Output directory cleaned"
        }
        catch {
            Write-ErrorMessage "Failed to clean output directory: $($_.Exception.Message)"
            exit 1
        }
    }
}

function Restore-Dependencies {
    Write-InfoMessage "Restoring NuGet packages..."
    
    try {
        $restoreResult = dotnet restore $ProjectPath --verbosity quiet
        if ($LASTEXITCODE -eq 0) {
            Write-SuccessMessage "Dependencies restored successfully"
        } else {
            Write-ErrorMessage "Failed to restore dependencies"
            exit 1
        }
    }
    catch {
        Write-ErrorMessage "Error during dependency restoration: $($_.Exception.Message)"
        exit 1
    }
}

function Build-Application {
    Write-InfoMessage "Building application..."
    Write-InfoMessage "Configuration: $Configuration"
    Write-InfoMessage "Runtime: $RuntimeIdentifier"
    Write-InfoMessage "Output: $OutputDir"
    
    # Build parameters
    $buildParams = @(
        "publish",
        $ProjectPath,
        "-c", $Configuration,
        "-r", $RuntimeIdentifier,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-o", $OutputDir,
        "--verbosity", "minimal"
    )
    
    # Add optimization parameters for Release builds
    if ($Configuration -eq "Release") {
        $buildParams += @(
            "-p:PublishTrimmed=true",
            "-p:PublishReadyToRun=true",
            "-p:EnableCompressionInSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true"
        )
    }
    
    try {
        Write-InfoMessage "Running: dotnet $($buildParams -join ' ')"
        & dotnet @buildParams
        
        if ($LASTEXITCODE -eq 0) {
            Write-SuccessMessage "Application built successfully"
        } else {
            Write-ErrorMessage "Build failed with exit code $LASTEXITCODE"
            exit 1
        }
    }
    catch {
        Write-ErrorMessage "Error during build: $($_.Exception.Message)"
        exit 1
    }
}

function Copy-AdditionalFiles {
    Write-InfoMessage "Copying additional files..."
    
    # Files to copy to the output directory
    $filesToCopy = @(
        @{ Source = "README.md"; Destination = "$OutputDir/README.md" },
        @{ Source = "LICENSE"; Destination = "$OutputDir/LICENSE" }
    )
    
    foreach ($file in $filesToCopy) {
        if (Test-Path $file.Source) {
            try {
                Copy-Item -Path $file.Source -Destination $file.Destination -Force
                Write-InfoMessage "Copied: $($file.Source) -> $($file.Destination)"
            }
            catch {
                Write-WarningMessage "Failed to copy $($file.Source): $($_.Exception.Message)"
            }
        } else {
            Write-WarningMessage "File not found, skipping: $($file.Source)"
        }
    }
}

function Get-FileSize {
    param([string]$FilePath)
    
    if (Test-Path $FilePath) {
        $size = (Get-Item $FilePath).Length
        $sizeInMB = [math]::Round($size / 1MB, 2)
        return "$sizeInMB MB"
    }
    return "N/A"
}

function Show-BuildSummary {
    $executablePath = "$OutputDir/$ExecutableName"
    $executableSize = Get-FileSize $executablePath
    
    Write-Host ""
    Write-Host "==================== BUILD SUMMARY ====================" -ForegroundColor $InfoColor
    Write-Host "Version:        $Version" -ForegroundColor White
    Write-Host "Configuration:  $Configuration" -ForegroundColor White
    Write-Host "Runtime:        $RuntimeIdentifier" -ForegroundColor White
    Write-Host "Output Dir:     $OutputDir" -ForegroundColor White
    Write-Host "Executable:     $ExecutableName ($executableSize)" -ForegroundColor White
    
    if (Test-Path $executablePath) {
        Write-Host "Status:         " -NoNewline -ForegroundColor White
        Write-Host "SUCCESS" -ForegroundColor $SuccessColor
    } else {
        Write-Host "Status:         " -NoNewline -ForegroundColor White
        Write-Host "FAILED" -ForegroundColor $ErrorColor
    }
    Write-Host "=======================================================" -ForegroundColor $InfoColor
    Write-Host ""
}

function Create-ZipPackage {
    if (-not $CreateZip) {
        Write-InfoMessage "Skipping ZIP creation (CreateZip=$CreateZip)"
        return
    }
    
    $zipFileName = "ChromeConnect-$Version-$RuntimeIdentifier.zip"
    $zipPath = "./$zipFileName"
    
    Write-InfoMessage "Creating ZIP package: $zipFileName"
    
    try {
        if (Test-Path $zipPath) {
            Remove-Item $zipPath -Force
        }
        
        Compress-Archive -Path "$OutputDir/*" -DestinationPath $zipPath -Force
        
        $zipSize = Get-FileSize $zipPath
        Write-SuccessMessage "ZIP package created: $zipFileName ($zipSize)"
    }
    catch {
        Write-ErrorMessage "Failed to create ZIP package: $($_.Exception.Message)"
    }
}

# Main execution
try {
    Write-Host ""
    Write-Host "==================== CHROMECONNECT BUILD SCRIPT ====================" -ForegroundColor $InfoColor
    Write-Host "Starting build process..." -ForegroundColor White
    Write-Host ""
    
    # Validate prerequisites
    if (-not (Test-DotnetInstalled)) {
        exit 1
    }
    
    # Update version
    Update-ProjectVersion -NewVersion $Version
    
    # Clean output directory
    Clean-OutputDirectory
    
    # Restore dependencies
    Restore-Dependencies
    
    # Build application
    Build-Application
    
    # Copy additional files
    Copy-AdditionalFiles
    
    # Show build summary
    Show-BuildSummary
    
    # Create ZIP package
    Create-ZipPackage
    
    Write-SuccessMessage "Build process completed successfully!"
    Write-InfoMessage "Executable location: $OutputDir/$ExecutableName"
    
    if ($CreateZip) {
        Write-InfoMessage "ZIP package: ChromeConnect-$Version-$RuntimeIdentifier.zip"
    }
    
} catch {
    Write-ErrorMessage "Unexpected error: $($_.Exception.Message)"
    exit 1
} 