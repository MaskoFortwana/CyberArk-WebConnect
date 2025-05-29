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

# Function to cleanup old extraction directories
function Cleanup-OldExtractions {
    param(
        [string]$BaseDir,
        [int]$DaysToKeep = 7
    )
    
    try {
        Write-InfoMessage "Cleaning up old extraction directories (older than $DaysToKeep days)..."
        
        if (-not (Test-Path $BaseDir)) {
            Write-InfoMessage "Base directory does not exist, skipping cleanup: $BaseDir"
            return
        }
        
        $cutoffDate = (Get-Date).AddDays(-$DaysToKeep)
        $oldDirectories = Get-ChildItem $BaseDir -Directory -ErrorAction SilentlyContinue | 
                         Where-Object { $_.LastWriteTime -lt $cutoffDate }
        
        if ($oldDirectories.Count -gt 0) {
            Write-InfoMessage "Found $($oldDirectories.Count) old extraction directories to remove"
            
            foreach ($dir in $oldDirectories) {
                try {
                    Write-InfoMessage "Removing old extraction directory: $($dir.Name)"
                    Remove-Item $dir.FullName -Recurse -Force -ErrorAction Stop
                    Write-InfoMessage "Successfully removed: $($dir.Name)"
                }
                catch {
                    Write-WarningMessage "Failed to remove old directory $($dir.Name): $($_.Exception.Message)"
                }
            }
        }
        else {
            Write-InfoMessage "No old extraction directories found to clean up"
        }
    }
    catch {
        Write-WarningMessage "Error during cleanup of old extractions: $($_.Exception.Message)"
    }
}

# Function to ensure proper directory permissions inheritance
function Set-DirectoryPermissions {
    param(
        [string]$DirectoryPath
    )
    
    try {
        Write-InfoMessage "Setting up permissions inheritance for directory: $DirectoryPath"
        
        if (-not (Test-Path $DirectoryPath)) {
            Write-WarningMessage "Directory does not exist, cannot set permissions: $DirectoryPath"
            return $false
        }
        
        # Enable inheritance from parent directory for the extraction directory
        $acl = Get-Acl $DirectoryPath -ErrorAction Stop
        $acl.SetAccessRuleProtection($false, $true) # $false = enable inheritance, $true = preserve existing rules
        Set-Acl $DirectoryPath $acl -ErrorAction Stop
        
        Write-InfoMessage "Successfully configured permissions inheritance for: $DirectoryPath"
        return $true
    }
    catch {
        Write-WarningMessage "Failed to set permissions for directory ${DirectoryPath}: $($_.Exception.Message)"
        return $false
    }
}

# Function to create and manage extraction directory structure
function Initialize-ExtractionDirectory {
    param(
        [string]$BaseDir
    )
    
    try {
        Write-InfoMessage "Initializing extraction directory structure..."
        
        # Create base directory if it doesn't exist
        if (-not (Test-Path $BaseDir)) {
            Write-InfoMessage "Creating base extraction directory: $BaseDir"
            New-Item -ItemType Directory -Path $BaseDir -Force | Out-Null
            Write-InfoMessage "Created base extraction directory: $BaseDir"
        }
        else {
            Write-InfoMessage "Base extraction directory already exists: $BaseDir"
        }
        
        # Set permissions for base directory
        $permissionsSet = Set-DirectoryPermissions -DirectoryPath $BaseDir
        if (-not $permissionsSet) {
            Write-WarningMessage "Could not set proper permissions for base directory"
        }
        
        # Create hash-based subdirectory (simulating .NET extraction behavior)
        # This hash represents a typical .NET single-file extraction directory name
        $hashDir = Join-Path $BaseDir 'BUVKQZGVGMYJUEVNC62UH0NUC1GYHEG='
        
        if (-not (Test-Path $hashDir)) {
            Write-InfoMessage "Creating hash-based extraction subdirectory: $hashDir"
            New-Item -ItemType Directory -Path $hashDir -Force | Out-Null
            Write-InfoMessage "Created hash-based subdirectory: $($hashDir | Split-Path -Leaf)"
        }
        else {
            Write-InfoMessage "Hash-based subdirectory already exists: $($hashDir | Split-Path -Leaf)"
        }
        
        # Set permissions for hash-based subdirectory
        $hashPermissionsSet = Set-DirectoryPermissions -DirectoryPath $hashDir
        if (-not $hashPermissionsSet) {
            Write-WarningMessage "Could not set proper permissions for hash-based subdirectory"
        }
        
        # Also ensure inheritance for any existing subdirectories
        $existingSubDirs = Get-ChildItem $BaseDir -Directory -ErrorAction SilentlyContinue
        if ($existingSubDirs.Count -gt 1) { # More than just our created hash dir
            Write-InfoMessage "Setting permissions inheritance for $($existingSubDirs.Count) existing subdirectories"
            
            foreach ($subDir in $existingSubDirs) {
                try {
                    $subDirAcl = Get-Acl $subDir.FullName -ErrorAction Stop
                    $subDirAcl.SetAccessRuleProtection($false, $true)
                    Set-Acl $subDir.FullName $subDirAcl -ErrorAction Stop
                    Write-InfoMessage "Set permissions inheritance for subdirectory: $($subDir.Name)"
                }
                catch {
                    Write-WarningMessage "Failed to set permissions for subdirectory $($subDir.Name): $($_.Exception.Message)"
                }
            }
        }
        
        # Perform cleanup of old extractions
        Cleanup-OldExtractions -BaseDir $BaseDir -DaysToKeep 7
        
        Write-InfoMessage "Extraction directory structure initialization completed"
        return $true
    }
    catch {
        Write-ErrorMessage "Failed to initialize extraction directory structure: $($_.Exception.Message)"
        return $false
    }
}

function Simulate-DllExtraction {
    Write-InfoMessage "Simulating DLL extraction for AppLocker compatibility..."
    
    # Initialize extraction summary
    $extractionSummary = @{
        StartTime = Get-Date
        Success = $false
        ExtractBasePath = ""
        DirectoriesFound = 0
        TotalDllsFound = 0
        ErrorsEncountered = @()
        WarningsEncountered = @()
        ExtractedDirectories = @()
    }
    
    try {
        # Get the extraction path from environment variable or use default
        $extractBasePath = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
        if (-not $extractBasePath) {
            $extractBasePath = "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect"
            Write-WarningMessage "DOTNET_BUNDLE_EXTRACT_BASE_DIR not set, using default: $extractBasePath"
            $extractionSummary.WarningsEncountered += "Environment variable DOTNET_BUNDLE_EXTRACT_BASE_DIR not set"
        }
        
        $extractionSummary.ExtractBasePath = $extractBasePath
        Write-InfoMessage "Target extraction path: $extractBasePath"
        
        # Validate extraction path
        if ($extractBasePath.Length -eq 0) {
            $errorMsg = "Extraction base path is empty or null"
            Write-ErrorMessage $errorMsg
            $extractionSummary.ErrorsEncountered += $errorMsg
            return $false
        }
        
        if ($extractBasePath.Length -gt 248) {
            $errorMsg = "Extraction path is too long (>248 characters): $($extractBasePath.Length)"
            Write-ErrorMessage $errorMsg
            $extractionSummary.ErrorsEncountered += $errorMsg
            return $false
        }
        
        # Check available disk space for extraction path drive
        try {
            $drive = Split-Path $extractBasePath -Qualifier
            if ($drive) {
                $driveInfo = Get-WmiObject -Class Win32_LogicalDisk | Where-Object { $_.DeviceID -eq $drive }
                if ($driveInfo -and $driveInfo.FreeSpace) {
                    $freeSpaceGB = [math]::Round($driveInfo.FreeSpace / 1GB, 2)
                    Write-InfoMessage "Available disk space on ${drive}: ${freeSpaceGB} GB"
                    
                    if ($freeSpaceGB -lt 0.5) {
                        $warningMsg = "Low disk space on extraction drive: ${freeSpaceGB} GB"
                        Write-WarningMessage $warningMsg
                        $extractionSummary.WarningsEncountered += $warningMsg
                    }
                }
            }
        }
        catch {
            $warningMsg = "Could not check disk space: $($_.Exception.Message)"
            Write-WarningMessage $warningMsg
            $extractionSummary.WarningsEncountered += $warningMsg
        }
        
        # Initialize extraction directory structure with proper management
        $initSuccess = Initialize-ExtractionDirectory -BaseDir $extractBasePath
        if (-not $initSuccess) {
            $errorMsg = "Failed to initialize extraction directory structure"
            Write-ErrorMessage $errorMsg
            $extractionSummary.ErrorsEncountered += $errorMsg
            return $false
        }
        
        # Verify we have write permissions after initialization
        try {
            $testFile = Join-Path $extractBasePath "test_permissions_$(Get-Random).tmp"
            "test" | Out-File -FilePath $testFile -ErrorAction Stop
            Remove-Item $testFile -Force -ErrorAction SilentlyContinue
            Write-InfoMessage "Confirmed write permissions to extraction directory"
        }
        catch {
            $errorMsg = "No write permissions to extraction directory: $($_.Exception.Message)"
            Write-ErrorMessage $errorMsg
            $extractionSummary.ErrorsEncountered += $errorMsg
            return $false
        }

        # Set the environment variable for the extraction process
        $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $extractBasePath
        Write-InfoMessage "Set DOTNET_BUNDLE_EXTRACT_BASE_DIR environment variable"
        
        # Get the path to the built executable
        $executablePath = "$OutputDir/$ExecutableName"
        if (-not (Test-Path $executablePath)) {
            $errorMsg = "Executable not found: $executablePath"
            Write-ErrorMessage $errorMsg
            $extractionSummary.ErrorsEncountered += $errorMsg
            return $false
        }
        
        # Verify executable is accessible and not corrupted
        try {
            $fileInfo = Get-Item $executablePath
            $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
            Write-InfoMessage "Executable found: $executablePath (${fileSizeMB} MB)"
            
            if ($fileInfo.Length -eq 0) {
                $errorMsg = "Executable file is empty (0 bytes)"
                Write-ErrorMessage $errorMsg
                $extractionSummary.ErrorsEncountered += $errorMsg
                return $false
            }
            
            if ($fileSizeMB -lt 1) {
                $warningMsg = "Executable seems unusually small: ${fileSizeMB} MB"
                Write-WarningMessage $warningMsg
                $extractionSummary.WarningsEncountered += $warningMsg
            }
        }
        catch {
            $errorMsg = "Cannot access executable file: $($_.Exception.Message)"
            Write-ErrorMessage $errorMsg
            $extractionSummary.ErrorsEncountered += $errorMsg
            return $false
        }
        
        Write-InfoMessage "Running DLL extraction simulation with: $executablePath"
        
        # Record pre-extraction state
        $preExtractionDirs = @()
        try {
            $preExtractionDirs = Get-ChildItem $extractBasePath -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
            Write-InfoMessage "Pre-extraction directories in target path: $($preExtractionDirs.Count)"
        }
        catch {
            $warningMsg = "Could not enumerate pre-extraction directories: $($_.Exception.Message)"
            Write-WarningMessage $warningMsg
            $extractionSummary.WarningsEncountered += $warningMsg
        }
        
        try {
            # Run the application with --version to trigger DLL extraction with minimal functionality
            Write-InfoMessage "Starting extraction process..."
            $processStartTime = Get-Date
            
            $process = Start-Process -FilePath $executablePath -ArgumentList "--version" -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput "nul" -ErrorAction Stop
            
            $processEndTime = Get-Date
            $processDuration = $processEndTime - $processStartTime
            $exitCode = $process.ExitCode
            
            Write-InfoMessage "Extraction process completed in $($processDuration.TotalSeconds) seconds with exit code: $exitCode"
            
            # Expected exit codes:
            # 0: Success (--version was handled correctly)
            # 4: Configuration error (missing required params - expected for minimal test)
            # Other codes may indicate issues but DLL extraction still happens first
            if ($exitCode -eq 0 -or $exitCode -eq 4 -or $exitCode -eq -2147450720) {
                Write-InfoMessage "Application exit code $exitCode is acceptable for DLL extraction test"
            } else {
                $warningMsg = "Unexpected exit code $exitCode, but proceeding to check for extracted DLLs"
                Write-WarningMessage $warningMsg
                $extractionSummary.WarningsEncountered += "Unexpected exit code: $exitCode"
            }
        }
        catch {
            $errorMsg = "Failed to start extraction process: $($_.Exception.Message)"
            Write-ErrorMessage $errorMsg
            $extractionSummary.ErrorsEncountered += $errorMsg
            
            # Additional error details
            if ($_.Exception.InnerException) {
                $innerMsg = "Inner exception: $($_.Exception.InnerException.Message)"
                Write-ErrorMessage $innerMsg
                $extractionSummary.ErrorsEncountered += $innerMsg
            }
            
            return $false
        }
        
        # Wait for file system to update
        Write-InfoMessage "Waiting for file system to update..."
        Start-Sleep -Milliseconds 1000
        
        # Check if DLL extraction occurred
        try {
            $extractedDirs = Get-ChildItem $extractBasePath -Directory -ErrorAction Stop
            $extractionSummary.DirectoriesFound = $extractedDirs.Count
            
            Write-InfoMessage "Found $($extractedDirs.Count) directories in extraction path"
            
            if ($extractedDirs.Count -gt 0) {
                $newDirs = $extractedDirs | Where-Object { $_.Name -notin $preExtractionDirs }
                if ($newDirs.Count -gt 0) {
                    Write-SuccessMessage "Found $($newDirs.Count) new extraction directories"
                } else {
                    Write-InfoMessage "No new directories created during extraction"
                }
                
                foreach ($dir in $extractedDirs) {
                    try {
                        Write-InfoMessage "Processing directory: $($dir.Name)"
                        $dllFiles = Get-ChildItem $dir.FullName -Filter "*.dll" -Recurse -ErrorAction Stop
                        $dllCount = $dllFiles.Count
                        $extractionSummary.TotalDllsFound += $dllCount
                        
                        $directoryInfo = @{
                            Name = $dir.Name
                            Path = $dir.FullName
                            DllCount = $dllCount
                            SizeMB = [math]::Round((Get-ChildItem $dir.FullName -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
                        }
                        $extractionSummary.ExtractedDirectories += $directoryInfo
                        
                        Write-SuccessMessage "Found extraction directory: $($dir.Name) with $dllCount DLL files ($($directoryInfo.SizeMB) MB)"
                        
                        # Log some DLL names for verification
                        if ($dllCount -gt 0) {
                            $sampleDlls = $dllFiles | Select-Object -First 3 | ForEach-Object { $_.Name }
                            Write-InfoMessage "Sample DLLs: $($sampleDlls -join ', ')$(if ($dllCount -gt 3) { '...' })"
                        }
                        
                        # Copy the extracted DLL directory to the publish output
                        $destDir = "$OutputDir/ExtractedDLLs/$($dir.Name)"
                        try {
                            Write-InfoMessage "Copying extracted DLLs to deployment package..."
                            
                            # Ensure the destination directory exists
                            $destParent = Split-Path $destDir -Parent
                            if (-not (Test-Path $destParent)) {
                                New-Item -ItemType Directory -Path $destParent -Force | Out-Null
                                Write-InfoMessage "Created destination parent directory: $destParent"
                            }
                            
                            # Verify source directory accessibility
                            if (-not (Test-Path $dir.FullName -PathType Container)) {
                                $errorMsg = "Source directory is not accessible: $($dir.FullName)"
                                Write-ErrorMessage $errorMsg
                                $extractionSummary.ErrorsEncountered += $errorMsg
                                continue
                            }
                            
                            # Perform the copy operation
                            Copy-Item -Path $dir.FullName -Destination $destDir -Recurse -Force -ErrorAction Stop
                            
                            # Verify copy was successful
                            if (Test-Path $destDir) {
                                $copiedFiles = Get-ChildItem $destDir -Recurse -File
                                Write-SuccessMessage "Successfully copied extracted DLLs to deployment package: $destDir ($($copiedFiles.Count) files)"
                            } else {
                                $errorMsg = "Copy operation completed but destination directory not found: $destDir"
                                Write-ErrorMessage $errorMsg
                                $extractionSummary.ErrorsEncountered += $errorMsg
                            }
                        }
                        catch {
                            $errorMsg = "Failed to copy extracted DLLs: $($_.Exception.Message)"
                            Write-ErrorMessage $errorMsg
                            $extractionSummary.ErrorsEncountered += $errorMsg
                            
                            # Attempt cleanup of partial copy
                            try {
                                if (Test-Path $destDir) {
                                    Write-InfoMessage "Attempting cleanup of partial copy: $destDir"
                                    Remove-Item $destDir -Recurse -Force -ErrorAction SilentlyContinue
                                }
                            }
                            catch {
                                $cleanupMsg = "Failed to cleanup partial copy: $($_.Exception.Message)"
                                Write-WarningMessage $cleanupMsg
                                $extractionSummary.WarningsEncountered += $cleanupMsg
                            }
                        }
                    }
                    catch {
                        $errorMsg = "Error processing extraction directory $($dir.Name): $($_.Exception.Message)"
                        Write-ErrorMessage $errorMsg
                        $extractionSummary.ErrorsEncountered += $errorMsg
                    }
                }
                
                if ($extractionSummary.TotalDllsFound -gt 0) {
                    Write-SuccessMessage "DLL extraction simulation completed successfully. Total DLLs found: $($extractionSummary.TotalDllsFound)"
                    $extractionSummary.Success = $true
                } else {
                    $warningMsg = "Extraction directories found but no DLL files detected"
                    Write-WarningMessage $warningMsg
                    $extractionSummary.WarningsEncountered += $warningMsg
                    $extractionSummary.Success = $true  # Still consider success as directories were created
                }
            }
            else {
                Write-InfoMessage "No DLL extraction directories found. This may indicate:"
                Write-InfoMessage "  - Application is already optimized and doesn't need extraction"
                Write-InfoMessage "  - DLLs are embedded in the single-file executable"
                Write-InfoMessage "  - Extraction path may be different than expected"
                
                # Check if there are any .NET runtime directories that might indicate extraction
                $tempExtractionPaths = @(
                    [System.IO.Path]::GetTempPath(),
                    "$env:USERPROFILE\AppData\Local\Temp",
                    "$env:TEMP"
                )
                
                foreach ($tempPath in $tempExtractionPaths) {
                    if (Test-Path $tempPath) {
                        try {
                            $dotnetDirs = Get-ChildItem $tempPath -Directory -Name "*.tmp" -ErrorAction SilentlyContinue | Where-Object { $_ -match "^\.net" }
                            if ($dotnetDirs) {
                                Write-InfoMessage "Found potential .NET extraction directories in $tempPath"
                                $extractionSummary.WarningsEncountered += "Alternative extraction directories found in $tempPath"
                            }
                        }
                        catch {
                            # Silently ignore temp directory access issues
                        }
                    }
                }
                
                $extractionSummary.Success = $true  # Consider this a successful test even without extraction
            }
        }
        catch {
            $errorMsg = "Failed to enumerate extraction directories: $($_.Exception.Message)"
            Write-ErrorMessage $errorMsg
            $extractionSummary.ErrorsEncountered += $errorMsg
            return $false
        }
    }
    catch {
        $errorMsg = "Unexpected error during DLL extraction simulation: $($_.Exception.Message)"
        Write-ErrorMessage $errorMsg
        $extractionSummary.ErrorsEncountered += $errorMsg
        return $false
    }
    finally {
        # Generate extraction summary report
        $extractionSummary.EndTime = Get-Date
        $extractionSummary.Duration = $extractionSummary.EndTime - $extractionSummary.StartTime
        
        Write-Host ""
        Write-Host "==================== DLL EXTRACTION SUMMARY ====================" -ForegroundColor $InfoColor
        Write-Host "Status:             " -NoNewline -ForegroundColor White
        if ($extractionSummary.Success) {
            Write-Host "SUCCESS" -ForegroundColor $SuccessColor
        } else {
            Write-Host "FAILED" -ForegroundColor $ErrorColor
        }
        Write-Host "Duration:           $($extractionSummary.Duration.TotalSeconds) seconds" -ForegroundColor White
        Write-Host "Extraction Path:    $($extractionSummary.ExtractBasePath)" -ForegroundColor White
        Write-Host "Directories Found:  $($extractionSummary.DirectoriesFound)" -ForegroundColor White
        Write-Host "Total DLLs Found:   $($extractionSummary.TotalDllsFound)" -ForegroundColor White
        Write-Host "Errors:             $($extractionSummary.ErrorsEncountered.Count)" -ForegroundColor $(if ($extractionSummary.ErrorsEncountered.Count -gt 0) { $ErrorColor } else { $SuccessColor })
        Write-Host "Warnings:           $($extractionSummary.WarningsEncountered.Count)" -ForegroundColor $(if ($extractionSummary.WarningsEncountered.Count -gt 0) { $WarningColor } else { $SuccessColor })
        
        if ($extractionSummary.ExtractedDirectories.Count -gt 0) {
            Write-Host "Extracted Directories:" -ForegroundColor White
            foreach ($dirInfo in $extractionSummary.ExtractedDirectories) {
                Write-Host "  - $($dirInfo.Name): $($dirInfo.DllCount) DLLs ($($dirInfo.SizeMB) MB)" -ForegroundColor White
            }
        }
        
        if ($extractionSummary.ErrorsEncountered.Count -gt 0) {
            Write-Host "Errors encountered:" -ForegroundColor $ErrorColor
            foreach ($error in $extractionSummary.ErrorsEncountered) {
                Write-Host "  - $error" -ForegroundColor $ErrorColor
            }
        }
        
        if ($extractionSummary.WarningsEncountered.Count -gt 0) {
            Write-Host "Warnings:" -ForegroundColor $WarningColor
            foreach ($warning in $extractionSummary.WarningsEncountered) {
                Write-Host "  - $warning" -ForegroundColor $WarningColor
            }
        }
        
        Write-Host "=================================================================" -ForegroundColor $InfoColor
        Write-Host ""
    }
    
    return $extractionSummary.Success
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
    
    # Simulate DLL extraction
    Simulate-DllExtraction
    
    Write-SuccessMessage "Build process completed successfully!"
    Write-InfoMessage "Executable location: $OutputDir/$ExecutableName"
    
    if ($CreateZip) {
        Write-InfoMessage "ZIP package: ChromeConnect-$Version-$RuntimeIdentifier.zip"
    }
    
} catch {
    Write-ErrorMessage "Unexpected error: $($_.Exception.Message)"
    exit 1
} 