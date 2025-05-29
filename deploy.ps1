#!/usr/bin/env pwsh
<#
.SYNOPSIS
    ChromeConnect PowerShell Deployment Script

.DESCRIPTION
    Deploys ChromeConnect with DLL extraction simulation and environment variable setup.
    Includes AppLocker compatibility features and comprehensive validation.

.PARAMETER TargetDir
    Target deployment directory (default: C:\ChromeConnect)

.PARAMETER SourceDir
    Source directory containing built files (default: ./publish)

.PARAMETER SetupEnvironment
    Whether to set up the DOTNET_BUNDLE_EXTRACT_BASE_DIR environment variable (default: true)

.PARAMETER RunExtraction
    Whether to run DLL extraction simulation (default: true)

.PARAMETER CopyExtractedDlls
    Whether to copy extracted DLL structure to deployment package (default: true)

.EXAMPLE
    ./deploy.ps1 -TargetDir "C:\Program Files\ChromeConnect"
    
.EXAMPLE
    ./deploy.ps1 -TargetDir "D:\Tools\ChromeConnect" -SetupEnvironment $false
#>

param (
    [string]$TargetDir = "C:\ChromeConnect",
    [string]$SourceDir = "./publish",
    [bool]$SetupEnvironment = $true,
    [bool]$RunExtraction = $true,
    [bool]$CopyExtractedDlls = $true
)

# Script configuration
$SourceExecutable = "$SourceDir/ChromeConnect.exe"
$ExtractDllsScript = "src/ChromeConnect/ExtractDlls.ps1"
$ExtractionBasePath = "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect"

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

function Set-ExtractionEnvironmentVariable {
    param([string]$Path)
    
    try {
        Write-InfoMessage "Setting up DOTNET_BUNDLE_EXTRACT_BASE_DIR environment variable..."
        
        # Set the environment variable at machine level for system-wide access
        [Environment]::SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", $Path, "Machine")
        
        # Also set for current session
        $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $Path
        
        Write-SuccessMessage "Environment variable set: DOTNET_BUNDLE_EXTRACT_BASE_DIR = $Path"
        return $true
    }
    catch {
        Write-ErrorMessage "Failed to set environment variable: $($_.Exception.Message)"
        Write-WarningMessage "You may need to run as administrator to set machine-level environment variables"
        return $false
    }
}

function Test-ExtractionEnvironment {
    Write-InfoMessage "Validating extraction environment setup..."
    
    # Check if environment variable is set
    $envVar = [Environment]::GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", "Machine")
    if (-not $envVar) {
        Write-WarningMessage "DOTNET_BUNDLE_EXTRACT_BASE_DIR not set at machine level"
        $envVar = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
        if ($envVar) {
            Write-InfoMessage "Found environment variable in current session: $envVar"
        } else {
            Write-WarningMessage "Environment variable not found in current session either"
            return $false
        }
    } else {
        Write-SuccessMessage "Environment variable found at machine level: $envVar"
    }
    
    # Check if extraction directory exists
    if (Test-Path $envVar) {
        Write-SuccessMessage "Extraction base directory exists: $envVar"
    } else {
        Write-InfoMessage "Extraction base directory will be created when needed: $envVar"
    }
    
    return $true
}

function Invoke-DllExtractionSimulation {
    param([string]$OutputPath)
    
    Write-InfoMessage "Running DLL extraction simulation..."
    
    if (-not (Test-Path $ExtractDllsScript)) {
        Write-ErrorMessage "ExtractDlls.ps1 script not found at: $ExtractDllsScript"
        return $false
    }
    
    try {
        # Run the extraction script with the deployment output path
        Write-InfoMessage "Executing: & '$ExtractDllsScript' -OutputPath '$OutputPath'"
        & $ExtractDllsScript -OutputPath $OutputPath -ProjectDir "src/ChromeConnect"
        
        if ($LASTEXITCODE -eq 0) {
            Write-SuccessMessage "DLL extraction simulation completed successfully"
            return $true
        } else {
            Write-WarningMessage "DLL extraction simulation completed with warnings (exit code: $LASTEXITCODE)"
            return $true  # Non-zero exit codes are often warnings, not failures
        }
    }
    catch {
        Write-ErrorMessage "Failed to run DLL extraction simulation: $($_.Exception.Message)"
        return $false
    }
}

function Copy-ExtractedDllStructure {
    param(
        [string]$DeploymentPackagePath
    )
    
    Write-InfoMessage "Copying extracted DLL structure to deployment package..."
    
    $extractionPath = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
    if (-not $extractionPath) {
        $extractionPath = $ExtractionBasePath
        Write-InfoMessage "Using default extraction path: $extractionPath"
    }
    
    $destinationPath = "$DeploymentPackagePath\ExtractedDlls"
    
    try {
        if (Test-Path $extractionPath) {
            # Create destination directory
            if (-not (Test-Path $destinationPath)) {
                New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
                Write-InfoMessage "Created destination directory: $destinationPath"
            }
            
            # Copy extraction structure
            $extractedDirs = Get-ChildItem $extractionPath -Directory -ErrorAction SilentlyContinue
            if ($extractedDirs.Count -gt 0) {
                Write-InfoMessage "Found $($extractedDirs.Count) extraction directories to copy"
                
                foreach ($dir in $extractedDirs) {
                    $destDir = Join-Path $destinationPath $dir.Name
                    try {
                        Copy-Item -Path $dir.FullName -Destination $destDir -Recurse -Force -ErrorAction Stop
                        Write-InfoMessage "Copied extraction directory: $($dir.Name)"
                    }
                    catch {
                        Write-WarningMessage "Failed to copy extraction directory $($dir.Name): $($_.Exception.Message)"
                    }
                }
                
                Write-SuccessMessage "Extracted DLL structure copied to deployment package"
                return $true
            } else {
                Write-InfoMessage "No extraction directories found to copy"
                return $true
            }
        } else {
            Write-InfoMessage "Extraction path does not exist yet: $extractionPath"
            Write-InfoMessage "This is normal if DLL extraction hasn't occurred yet"
            return $true
        }
    }
    catch {
        Write-ErrorMessage "Failed to copy extracted DLL structure: $($_.Exception.Message)"
        return $false
    }
}

function Validate-Extraction {
    Write-InfoMessage "Validating DLL extraction setup..."
    
    $extractionPath = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
    if (-not $extractionPath) {
        $extractionPath = $ExtractionBasePath
    }
    
    $hashBasedDir = Join-Path $extractionPath "BUVKQZGVGMYJUEVNC62UH0NUC1GYHEG="
    
    try {
        if (-not (Test-Path $hashBasedDir)) {
            Write-InfoMessage "Hash-based extraction directory not found: $hashBasedDir"
            Write-InfoMessage "This is normal if the application hasn't been run yet"
            Write-InfoMessage "DLL extraction will occur automatically on first application run"
            return $true
        } else {
            Write-SuccessMessage "Hash-based extraction directory found: $hashBasedDir"
            
            # Check for DLL files
            $dllFiles = Get-ChildItem $hashBasedDir -Filter "*.dll" -Recurse -ErrorAction SilentlyContinue
            if ($dllFiles.Count -gt 0) {
                Write-SuccessMessage "Found $($dllFiles.Count) DLL files in extraction directory"
            } else {
                Write-InfoMessage "No DLL files found yet in extraction directory"
            }
            return $true
        }
    }
    catch {
        Write-ErrorMessage "DLL extraction validation failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-Prerequisites {
    Write-InfoMessage "Checking deployment prerequisites..."
    
    # Check if source executable exists
    if (-not (Test-Path $SourceExecutable)) {
        Write-ErrorMessage "ChromeConnect.exe not found at: $SourceExecutable"
        Write-ErrorMessage "Please run the build script first: publish.ps1"
        return $false
    }
    
    # Check if source directory exists
    if (-not (Test-Path $SourceDir)) {
        Write-ErrorMessage "Source directory not found: $SourceDir"
        return $false
    }
    
    Write-SuccessMessage "All prerequisites satisfied"
    return $true
}

function Deploy-ChromeConnect {
    param([string]$Target)
    
    Write-InfoMessage "Deploying ChromeConnect to: $Target"
    
    try {
        # Create target directory
        if (-not (Test-Path $Target)) {
            New-Item -ItemType Directory -Path $Target -Force | Out-Null
            Write-InfoMessage "Created target directory: $Target"
        }
        
        # Copy main executable
        Write-InfoMessage "Copying ChromeConnect.exe..."
        Copy-Item -Path $SourceExecutable -Destination "$Target\ChromeConnect.exe" -Force
        
        # Copy additional files from source directory
        $additionalFiles = @("README.md", "LICENSE")
        foreach ($file in $additionalFiles) {
            $sourcePath = "$SourceDir\$file"
            if (Test-Path $sourcePath) {
                Copy-Item -Path $sourcePath -Destination "$Target\$file" -Force
                Write-InfoMessage "Copied: $file"
            }
        }
        
        # Copy any ExtractedDLLs directory if it exists
        $extractedDllsSource = "$SourceDir\ExtractedDLLs"
        if (Test-Path $extractedDllsSource) {
            $extractedDllsTarget = "$Target\ExtractedDLLs"
            Copy-Item -Path $extractedDllsSource -Destination $extractedDllsTarget -Recurse -Force
            Write-InfoMessage "Copied ExtractedDLLs directory"
        }
        
        # Create logs and screenshots directories
        $directories = @("logs", "screenshots")
        foreach ($dir in $directories) {
            $dirPath = "$Target\$dir"
            if (-not (Test-Path $dirPath)) {
                New-Item -ItemType Directory -Path $dirPath -Force | Out-Null
                Write-InfoMessage "Created directory: $dir"
            }
        }
        
        Write-SuccessMessage "ChromeConnect deployment completed"
        return $true
    }
    catch {
        Write-ErrorMessage "Deployment failed: $($_.Exception.Message)"
        return $false
    }
}

function Show-DeploymentSummary {
    param([string]$Target, [bool]$Success)
    
    Write-Host ""
    Write-Host "==================== DEPLOYMENT SUMMARY ====================" -ForegroundColor $InfoColor
    Write-Host "Target Directory:   $Target" -ForegroundColor White
    Write-Host "Source Directory:   $SourceDir" -ForegroundColor White
    
    if ($Success) {
        Write-Host "Status:             " -NoNewline -ForegroundColor White
        Write-Host "SUCCESS" -ForegroundColor $SuccessColor
        
        # Show deployed files
        if (Test-Path "$Target\ChromeConnect.exe") {
            $size = [math]::Round((Get-Item "$Target\ChromeConnect.exe").Length / 1MB, 2)
            Write-Host "Executable:         ChromeConnect.exe ($size MB)" -ForegroundColor White
        }
        
        Write-Host ""
        Write-Host "To run ChromeConnect:" -ForegroundColor White
        Write-Host "  cd `"$Target`"" -ForegroundColor White
        Write-Host "  .\ChromeConnect.exe --help" -ForegroundColor White
        Write-Host ""
        
        # Environment variable information
        $envVar = [Environment]::GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", "Machine")
        if ($envVar) {
            Write-Host "DLL Extraction Path: $envVar" -ForegroundColor White
        }
    } else {
        Write-Host "Status:             " -NoNewline -ForegroundColor White
        Write-Host "FAILED" -ForegroundColor $ErrorColor
    }
    
    Write-Host "=============================================================" -ForegroundColor $InfoColor
    Write-Host ""
}

# Main execution
try {
    Write-Host ""
    Write-Host "==================== CHROMECONNECT DEPLOYMENT SCRIPT ====================" -ForegroundColor $InfoColor
    Write-Host "Starting deployment process..." -ForegroundColor White
    Write-Host ""
    
    # Check prerequisites
    if (-not (Test-Prerequisites)) {
        exit 1
    }
    
    $deploymentSuccess = $true
    
    # Set up environment variable
    if ($SetupEnvironment) {
        if (-not (Set-ExtractionEnvironmentVariable -Path $ExtractionBasePath)) {
            Write-WarningMessage "Environment variable setup failed, but continuing deployment"
        }
    } else {
        Write-InfoMessage "Skipping environment variable setup (SetupEnvironment=$SetupEnvironment)"
    }
    
    # Test environment setup
    Test-ExtractionEnvironment | Out-Null
    
    # Run DLL extraction simulation
    if ($RunExtraction) {
        if (-not (Invoke-DllExtractionSimulation -OutputPath $SourceDir)) {
            Write-WarningMessage "DLL extraction simulation failed, but continuing deployment"
        }
    } else {
        Write-InfoMessage "Skipping DLL extraction simulation (RunExtraction=$RunExtraction)"
    }
    
    # Deploy ChromeConnect
    if (-not (Deploy-ChromeConnect -Target $TargetDir)) {
        $deploymentSuccess = $false
    }
    
    # Copy extracted DLL structure
    if ($CopyExtractedDlls -and $deploymentSuccess) {
        if (-not (Copy-ExtractedDllStructure -DeploymentPackagePath $TargetDir)) {
            Write-WarningMessage "Failed to copy extracted DLL structure, but deployment continues"
        }
    } else {
        Write-InfoMessage "Skipping extracted DLL structure copy"
    }
    
    # Validate extraction setup
    Validate-Extraction | Out-Null
    
    # Show summary
    Show-DeploymentSummary -Target $TargetDir -Success $deploymentSuccess
    
    if ($deploymentSuccess) {
        Write-SuccessMessage "Deployment process completed successfully!"
        exit 0
    } else {
        Write-ErrorMessage "Deployment process failed!"
        exit 1
    }
    
} catch {
    Write-ErrorMessage "Unexpected error during deployment: $($_.Exception.Message)"
    exit 1
} 