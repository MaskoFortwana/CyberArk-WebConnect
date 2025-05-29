# ExtractDlls.ps1 - Build-time DLL extraction script for ChromeConnect
# This script is called during the build process to simulate DLL extraction
# for AppLocker compatibility

param(
    [string]$OutputPath = "",
    [string]$ProjectDir = "",
    [string]$Configuration = "Release"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Function to write colored output
function Write-InfoMessage {
    param([string]$Message)
    Write-Host "INFO: $Message" -ForegroundColor Green
}

function Write-WarningMessage {
    param([string]$Message)
    Write-Host "WARNING: $Message" -ForegroundColor Yellow
}

function Write-ErrorMessage {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
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

# Main execution
try {
    Write-InfoMessage "Starting build-time DLL extraction for ChromeConnect"
    
    # Clean up paths - remove any trailing quotes or extra characters
    if ($OutputPath) {
        $OutputPath = $OutputPath.Trim('"').TrimEnd('\').TrimEnd('/')
    }
    if ($ProjectDir) {
        $ProjectDir = $ProjectDir.Trim('"').TrimEnd('\').TrimEnd('/')
    }
    
    # Determine paths
    if (-not $OutputPath) {
        $OutputPath = Join-Path $ProjectDir "bin\$Configuration\net8.0\publish"
    }
    
    if (-not $ProjectDir) {
        $ProjectDir = $PSScriptRoot
    }
    
    Write-InfoMessage "Project Directory: $ProjectDir"
    Write-InfoMessage "Output Path: $OutputPath"
    
    # Validate paths exist
    if (-not (Test-Path $ProjectDir)) {
        Write-WarningMessage "Project directory does not exist: $ProjectDir"
        exit 0
    }
    
    if (-not (Test-Path $OutputPath)) {
        Write-WarningMessage "Output directory does not exist: $OutputPath"
        exit 0
    }
    
    # Check if the executable exists
    $executablePath = Join-Path $OutputPath "ChromeConnect.exe"
    if (-not (Test-Path $executablePath)) {
        Write-WarningMessage "ChromeConnect.exe not found at $executablePath"
        Write-InfoMessage "This is normal during build process - DLL extraction will occur at runtime"
        exit 0
    }
    
    # Set up extraction environment
    $extractBasePath = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
    if (-not $extractBasePath) {
        $extractBasePath = "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect"
        Write-InfoMessage "Using default extraction path: $extractBasePath"
    }
    
    # Initialize extraction directory structure with proper management
    $initSuccess = Initialize-ExtractionDirectory -BaseDir $extractBasePath
    if (-not $initSuccess) {
        Write-WarningMessage "Could not fully initialize extraction directory structure"
        Write-InfoMessage "DLL extraction will be handled at runtime"
        exit 0
    }

    # Test write permissions
    try {
        $testFile = Join-Path $extractBasePath "build_test_$(Get-Random).tmp"
        "test" | Out-File -FilePath $testFile -ErrorAction Stop
        Remove-Item $testFile -Force -ErrorAction SilentlyContinue
        Write-InfoMessage "Confirmed write permissions to extraction directory"
    }
    catch {
        Write-WarningMessage "No write permissions to extraction directory: $($_.Exception.Message)"
        Write-InfoMessage "DLL extraction will be handled at runtime"
        exit 0
    }
    
    # Run DLL extraction simulation
    Write-InfoMessage "Running DLL extraction simulation..."
    try {
        $process = Start-Process -FilePath $executablePath -ArgumentList "--version" -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput "nul" -ErrorAction Stop
        $exitCode = $process.ExitCode
        
        Write-InfoMessage "Extraction simulation completed with exit code: $exitCode"
        
        # Check for extracted DLLs
        $extractedDirs = Get-ChildItem $extractBasePath -Directory -ErrorAction SilentlyContinue
        if ($extractedDirs.Count -gt 0) {
            $totalDlls = 0
            foreach ($dir in $extractedDirs) {
                $dllFiles = Get-ChildItem $dir.FullName -Filter "*.dll" -Recurse -ErrorAction SilentlyContinue
                $totalDlls += $dllFiles.Count
            }
            
            Write-InfoMessage "Found $($extractedDirs.Count) extraction directories with $totalDlls DLL files"
            
            # Copy extracted DLLs to output directory
            $extractedDllsPath = Join-Path $OutputPath "ExtractedDLLs"
            if (-not (Test-Path $extractedDllsPath)) {
                New-Item -ItemType Directory -Path $extractedDllsPath -Force | Out-Null
            }
            
            foreach ($dir in $extractedDirs) {
                $destPath = Join-Path $extractedDllsPath $dir.Name
                try {
                    Copy-Item $dir.FullName $destPath -Recurse -Force
                    Write-InfoMessage "Copied extracted DLLs from $($dir.Name) to build output"
                }
                catch {
                    Write-WarningMessage "Failed to copy extracted DLLs: $($_.Exception.Message)"
                }
            }
        }
        else {
            Write-InfoMessage "No DLL extraction occurred - this is normal for some build configurations"
        }
    }
    catch {
        Write-WarningMessage "DLL extraction simulation failed: $($_.Exception.Message)"
        Write-InfoMessage "This is not critical - DLL extraction will be handled at runtime"
    }
    
    Write-InfoMessage "Build-time DLL extraction completed successfully"
}
catch {
    Write-ErrorMessage "Build-time DLL extraction failed: $($_.Exception.Message)"
    Write-InfoMessage "Build will continue - DLL extraction will be handled at runtime"
    exit 0  # Don't fail the build
} 