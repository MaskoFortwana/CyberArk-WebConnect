#!/usr/bin/env pwsh
<#
.SYNOPSIS
    ChromeConnect Two-Tier Deployment Structure Script

.DESCRIPTION
    Reorganizes the published ChromeConnect files into a two-tier structure with launcher:
    - A small launcher executable (ChromeConnect.exe) goes to the parent directory
    - ALL application files including the real ChromeConnect.exe go to the ChromeConnect subdirectory
    - The launcher automatically starts the real application from the subdirectory

.PARAMETER OutputPath
    The path where the published files are located

.PARAMETER ProjectDir
    The project directory (used to find the launcher project)

.PARAMETER Configuration
    The build configuration (used for launcher build)
#>

param (
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    
    [Parameter(Mandatory = $false)]
    [string]$ProjectDir = "",
    
    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Release"
)

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

# Main execution
try {
    Write-Host ""
    Write-Host "==================== TWO-TIER DEPLOYMENT SETUP ====================" -ForegroundColor $InfoColor
    Write-Host "Output Path: $OutputPath" -ForegroundColor White
    Write-Host ""

    # Validate output path exists
    if (-not (Test-Path $OutputPath)) {
        Write-ErrorMessage "Output path does not exist: $OutputPath"
        exit 1
    }

    # Check if ChromeConnect.exe exists
    $executablePath = Join-Path $OutputPath "ChromeConnect.exe"
    if (-not (Test-Path $executablePath)) {
        Write-ErrorMessage "ChromeConnect.exe not found in output path: $OutputPath"
        exit 1
    }

    Write-InfoMessage "Found ChromeConnect.exe: $executablePath"

    # Create the two-tier structure directory
    $chromeConnectSubdir = Join-Path $OutputPath "ChromeConnect"
    if (-not (Test-Path $chromeConnectSubdir)) {
        Write-InfoMessage "Creating ChromeConnect subdirectory: $chromeConnectSubdir"
        New-Item -ItemType Directory -Path $chromeConnectSubdir -Force | Out-Null
    } else {
        Write-InfoMessage "ChromeConnect subdirectory already exists: $chromeConnectSubdir"
    }

    # Get all files and directories in the output directory
    $allFiles = Get-ChildItem $OutputPath -File
    $allDirectories = Get-ChildItem $OutputPath -Directory | Where-Object { $_.Name -ne "ChromeConnect" }

    Write-InfoMessage "Found $($allFiles.Count) files and $($allDirectories.Count) directories to process"

    # Move ALL files (including ChromeConnect.exe) to the subdirectory
    if ($allFiles.Count -gt 0) {
        Write-InfoMessage "Moving ALL $($allFiles.Count) files to ChromeConnect subdirectory..."
        
        foreach ($file in $allFiles) {
            try {
                $destinationPath = Join-Path $chromeConnectSubdir $file.Name
                
                # If file already exists in destination, remove it first
                if (Test-Path $destinationPath) {
                    Remove-Item $destinationPath -Force
                }
                
                Move-Item $file.FullName $destinationPath -Force
                Write-InfoMessage "Moved: $($file.Name)"
            }
            catch {
                Write-ErrorMessage "Failed to move $($file.Name): $($_.Exception.Message)"
            }
        }
    } else {
        Write-InfoMessage "No files to move"
    }

    # Move all directories to the subdirectory
    if ($allDirectories.Count -gt 0) {
        Write-InfoMessage "Moving $($allDirectories.Count) directories to ChromeConnect subdirectory..."
        
        foreach ($dir in $allDirectories) {
            try {
                $destinationPath = Join-Path $chromeConnectSubdir $dir.Name
                
                # If directory already exists in destination, remove it first
                if (Test-Path $destinationPath) {
                    Remove-Item $destinationPath -Recurse -Force
                }
                
                Move-Item $dir.FullName $destinationPath -Force
                Write-InfoMessage "Moved directory: $($dir.Name)"
            }
            catch {
                Write-ErrorMessage "Failed to move directory $($dir.Name): $($_.Exception.Message)"
            }
        }
    } else {
        Write-InfoMessage "No directories to move"
    }

    # Build and deploy the launcher
    Write-InfoMessage "Building and deploying ChromeConnect launcher..."
    
    # Find the launcher project directory
    $launcherProjectDir = ""
    if ($ProjectDir) {
        Write-InfoMessage "Project directory provided: $ProjectDir"
        # Look for launcher project relative to the main project directory
        $possiblePaths = @(
            (Join-Path (Split-Path $ProjectDir -Parent) "ChromeConnect.Launcher"),
            (Join-Path $ProjectDir ".." "ChromeConnect.Launcher"),
            (Join-Path $ProjectDir "ChromeConnect.Launcher")
        )
        
        Write-InfoMessage "Searching for launcher project in possible paths:"
        foreach ($path in $possiblePaths) {
            Write-InfoMessage "  Checking: $path"
            if (Test-Path (Join-Path $path "ChromeConnect.Launcher.csproj")) {
                $launcherProjectDir = $path
                Write-InfoMessage "  Found launcher project!"
                break
            } else {
                Write-InfoMessage "  Not found"
            }
        }
    } else {
        Write-WarningMessage "No project directory provided, using relative search"
        # Try relative to current directory
        $currentDir = Get-Location
        $possiblePaths = @(
            (Join-Path $currentDir "src" "ChromeConnect.Launcher"),
            (Join-Path $currentDir ".." "ChromeConnect.Launcher"),
            (Join-Path $currentDir "ChromeConnect.Launcher")
        )
        
        foreach ($path in $possiblePaths) {
            if (Test-Path (Join-Path $path "ChromeConnect.Launcher.csproj")) {
                $launcherProjectDir = $path
                break
            }
        }
    }
    
    if (-not $launcherProjectDir -or -not (Test-Path $launcherProjectDir)) {
        Write-ErrorMessage "Launcher project directory not found. Tried paths: $($possiblePaths -join ', ')"
        Write-ErrorMessage "Please ensure ChromeConnect.Launcher project exists"
        exit 1
    }
    
    Write-InfoMessage "Found launcher project at: $launcherProjectDir"
    
    # Build the launcher
    $launcherOutputDir = Join-Path $launcherProjectDir "bin" $Configuration "net8.0" "win-x64" "publish"
    
    try {
        Write-InfoMessage "Building launcher project..."
        $buildResult = & dotnet publish "$launcherProjectDir" -c $Configuration -r win-x64 --self-contained true -o "$launcherOutputDir" 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMessage "Launcher build failed:"
            Write-Host $buildResult -ForegroundColor $ErrorColor
            exit 1
        }
        
        Write-SuccessMessage "Launcher built successfully"
    }
    catch {
        Write-ErrorMessage "Failed to build launcher: $($_.Exception.Message)"
        exit 1
    }
    
    # Copy the launcher executable to the parent directory
    $launcherExePath = Join-Path $launcherOutputDir "ChromeConnect.exe"
    if (-not (Test-Path $launcherExePath)) {
        Write-ErrorMessage "Launcher executable not found at: $launcherExePath"
        exit 1
    }
    
    $parentLauncherPath = Join-Path $OutputPath "ChromeConnect.exe"
    try {
        Copy-Item $launcherExePath $parentLauncherPath -Force
        Write-SuccessMessage "Launcher deployed to parent directory"
    }
    catch {
        Write-ErrorMessage "Failed to copy launcher: $($_.Exception.Message)"
        exit 1
    }

    # Verify the final structure
    Write-InfoMessage "Verifying final deployment structure..."
    
    $parentFiles = Get-ChildItem $OutputPath -File
    $subdirFiles = Get-ChildItem $chromeConnectSubdir -File -ErrorAction SilentlyContinue
    $subdirDirs = Get-ChildItem $chromeConnectSubdir -Directory -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "==================== DEPLOYMENT STRUCTURE SUMMARY ====================" -ForegroundColor $InfoColor
    Write-Host "Parent Directory ($OutputPath):" -ForegroundColor White
    
    if ($parentFiles.Count -eq 1 -and $parentFiles[0].Name -eq "ChromeConnect.exe") {
        $launcherSize = [math]::Round($parentFiles[0].Length / 1KB, 2)
        Write-Host "  ✅ ChromeConnect.exe (Launcher - $launcherSize KB)" -ForegroundColor $SuccessColor
    } else {
        Write-Host "  ❌ Unexpected files found:" -ForegroundColor $ErrorColor
        foreach ($file in $parentFiles) {
            Write-Host "    - $($file.Name)" -ForegroundColor $ErrorColor
        }
    }

    Write-Host ""
    Write-Host "ChromeConnect Subdirectory ($chromeConnectSubdir):" -ForegroundColor White
    Write-Host "  📁 $($subdirFiles.Count) files" -ForegroundColor White
    Write-Host "  📁 $($subdirDirs.Count) directories" -ForegroundColor White

    # Show key files that should be in the subdirectory
    $keyFiles = @("ChromeConnect.exe", "ChromeConnect.dll", "ChromeConnect.runtimeconfig.json", "ChromeConnect.deps.json", "System.Private.CoreLib.dll")
    foreach ($keyFile in $keyFiles) {
        $keyFilePath = Join-Path $chromeConnectSubdir $keyFile
        if (Test-Path $keyFilePath) {
            $size = (Get-Item $keyFilePath).Length
            $sizeDisplay = if ($size -gt 1MB) { "$([math]::Round($size / 1MB, 2)) MB" } else { "$([math]::Round($size / 1KB, 2)) KB" }
            $description = if ($keyFile -eq "ChromeConnect.exe") { " (Main App)" } else { "" }
            Write-Host "  ✅ $keyFile$description ($sizeDisplay)" -ForegroundColor $SuccessColor
        } else {
            Write-Host "  ❌ $keyFile (missing)" -ForegroundColor $ErrorColor
        }
    }

    Write-Host "=================================================================" -ForegroundColor $InfoColor
    Write-Host ""

    # Final validation
    $launcherExists = Test-Path (Join-Path $OutputPath "ChromeConnect.exe")
    $mainAppExists = Test-Path (Join-Path $chromeConnectSubdir "ChromeConnect.exe")
    $coreLibExists = Test-Path (Join-Path $chromeConnectSubdir "System.Private.CoreLib.dll")
    $appDllExists = Test-Path (Join-Path $chromeConnectSubdir "ChromeConnect.dll")

    if ($launcherExists -and $mainAppExists -and $coreLibExists -and $appDllExists) {
        Write-SuccessMessage "Two-tier deployment structure with launcher created successfully!"
        Write-InfoMessage "Structure is ready for CyberArk PSM Components deployment"
        Write-InfoMessage "Launcher will automatically start the main application from the subdirectory"
    } else {
        Write-ErrorMessage "Two-tier structure setup failed - missing required files"
        if (-not $launcherExists) { Write-ErrorMessage "  Missing: Launcher executable in parent directory" }
        if (-not $mainAppExists) { Write-ErrorMessage "  Missing: Main application in subdirectory" }
        if (-not $coreLibExists) { Write-ErrorMessage "  Missing: .NET Core library" }
        if (-not $appDllExists) { Write-ErrorMessage "  Missing: Application DLL" }
        exit 1
    }

} catch {
    Write-ErrorMessage "Unexpected error during deployment structure setup: $($_.Exception.Message)"
    exit 1
} 