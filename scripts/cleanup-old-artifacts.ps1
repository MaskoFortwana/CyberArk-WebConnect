#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Cleanup script for old ChromeConnect artifacts

.DESCRIPTION
    Identifies and removes old ChromeConnect build artifacts while preserving WebConnect artifacts.
    Includes safety checks and retention policies to prevent accidental deletion of important files.

.PARAMETER TargetDirectory
    The directory to scan for old artifacts (default: current directory)

.PARAMETER DryRun
    Perform a dry run without actually deleting files (default: false)

.PARAMETER KeepNewest
    Number of newest ChromeConnect artifacts to keep (default: 2)

.PARAMETER Force
    Skip confirmation prompts (default: false)

.EXAMPLE
    ./cleanup-old-artifacts.ps1
    
.EXAMPLE
    ./cleanup-old-artifacts.ps1 -DryRun $true
    
.EXAMPLE
    ./cleanup-old-artifacts.ps1 -TargetDirectory "C:\Builds" -KeepNewest 1 -Force $true
#>

param (
    [string]$TargetDirectory = ".",
    [bool]$DryRun = $false,
    [int]$KeepNewest = 2,
    [bool]$Force = $false
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

function Get-ChromeConnectArtifacts {
    param([string]$Directory)
    
    Write-InfoMessage "Scanning for ChromeConnect artifacts in: $Directory"
    
    $artifacts = @()
    
    # Find ChromeConnect ZIP files
    $zipFiles = Get-ChildItem -Path $Directory -Filter "ChromeConnect*.zip" -ErrorAction SilentlyContinue
    if ($zipFiles) {
        $artifacts += $zipFiles | ForEach-Object {
            @{
                Type = "ZIP Package"
                Path = $_.FullName
                Name = $_.Name
                Size = $_.Length
                LastModified = $_.LastWriteTime
            }
        }
    }
    
    # Find ChromeConnect executables
    $exeFiles = Get-ChildItem -Path $Directory -Filter "ChromeConnect*.exe" -Recurse -ErrorAction SilentlyContinue
    if ($exeFiles) {
        $artifacts += $exeFiles | ForEach-Object {
            @{
                Type = "Executable"
                Path = $_.FullName
                Name = $_.Name
                Size = $_.Length
                LastModified = $_.LastWriteTime
            }
        }
    }
    
    # Find ChromeConnect directories
    $directories = Get-ChildItem -Path $Directory -Directory -Filter "ChromeConnect*" -ErrorAction SilentlyContinue
    if ($directories) {
        $artifacts += $directories | ForEach-Object {
            $dirSize = (Get-ChildItem -Path $_.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
            @{
                Type = "Directory"
                Path = $_.FullName
                Name = $_.Name
                Size = if ($dirSize) { $dirSize } else { 0 }
                LastModified = $_.LastWriteTime
            }
        }
    }
    
    # Find ChromeConnect log files
    $logFiles = Get-ChildItem -Path $Directory -Filter "chromeconnect*.log" -Recurse -ErrorAction SilentlyContinue
    if ($logFiles) {
        $artifacts += $logFiles | ForEach-Object {
            @{
                Type = "Log File"
                Path = $_.FullName
                Name = $_.Name
                Size = $_.Length
                LastModified = $_.LastWriteTime
            }
        }
    }
    
    return $artifacts
}

function Show-ArtifactsSummary {
    param([array]$Artifacts)
    
    if ($Artifacts.Count -eq 0) {
        Write-InfoMessage "No ChromeConnect artifacts found."
        return
    }
    
    Write-InfoMessage "Found $($Artifacts.Count) ChromeConnect artifacts:"
    Write-Host ""
    
    $Artifacts | Sort-Object Type, LastModified | ForEach-Object {
        $sizeString = if ($_.Size -gt 1MB) {
            "{0:N2} MB" -f ($_.Size / 1MB)
        } elseif ($_.Size -gt 1KB) {
            "{0:N2} KB" -f ($_.Size / 1KB)
        } else {
            "$($_.Size) bytes"
        }
        
        Write-Host "  [$($_.Type)] $($_.Name) - $sizeString (Modified: $($_.LastModified.ToString('yyyy-MM-dd HH:mm:ss')))" -ForegroundColor White
    }
    
    # Fix the total size calculation by handling null/empty sizes
    $validSizes = $Artifacts | Where-Object { $_.Size -ne $null -and $_.Size -ne 0 } | ForEach-Object { $_.Size }
    if ($validSizes.Count -gt 0) {
        $totalSize = ($validSizes | Measure-Object -Sum).Sum
    } else {
        $totalSize = 0
    }
    
    $totalSizeString = if ($totalSize -gt 1MB) {
        "{0:N2} MB" -f ($totalSize / 1MB)
    } else {
        "{0:N2} KB" -f ($totalSize / 1KB)
    }
    
    Write-Host ""
    Write-InfoMessage "Total size to be cleaned: $totalSizeString"
}

function Get-ArtifactsToDelete {
    param([array]$Artifacts, [int]$KeepNewest)
    
    # Group artifacts by type
    $zipPackages = $Artifacts | Where-Object { $_.Type -eq "ZIP Package" }
    $otherArtifacts = $Artifacts | Where-Object { $_.Type -ne "ZIP Package" }
    
    $toDelete = @()
    
    # For ZIP packages, keep the newest ones based on KeepNewest parameter
    if ($zipPackages.Count -gt $KeepNewest) {
        $sortedZips = $zipPackages | Sort-Object LastModified -Descending
        $toDelete += $sortedZips | Select-Object -Skip $KeepNewest
        
        if ($KeepNewest -gt 0) {
            $keeping = $sortedZips | Select-Object -First $KeepNewest
            Write-InfoMessage "Keeping $KeepNewest newest ZIP packages:"
            $keeping | ForEach-Object {
                Write-Host "  KEEPING: $($_.Name) (Modified: $($_.LastModified.ToString('yyyy-MM-dd HH:mm:ss')))" -ForegroundColor Green
            }
        }
    }
    
    # For other artifacts (executables, directories, log files), delete all
    $toDelete += $otherArtifacts
    
    return $toDelete
}

function Remove-Artifacts {
    param([array]$ArtifactsToDelete, [bool]$DryRunMode)
    
    if ($ArtifactsToDelete.Count -eq 0) {
        Write-InfoMessage "No artifacts to delete."
        return $true
    }
    
    $errors = @()
    
    foreach ($artifact in $ArtifactsToDelete) {
        try {
            if ($DryRunMode) {
                Write-InfoMessage "[DRY RUN] Would delete: $($artifact.Path)"
            } else {
                Write-InfoMessage "Deleting: $($artifact.Name)"
                
                if ($artifact.Type -eq "Directory") {
                    Remove-Item -Path $artifact.Path -Recurse -Force -ErrorAction Stop
                } else {
                    Remove-Item -Path $artifact.Path -Force -ErrorAction Stop
                }
                
                Write-SuccessMessage "Successfully deleted: $($artifact.Name)"
            }
        }
        catch {
            $errorMsg = "Failed to delete $($artifact.Name): $($_.Exception.Message)"
            Write-ErrorMessage $errorMsg
            $errors += $errorMsg
        }
    }
    
    return $errors.Count -eq 0
}

function Confirm-Deletion {
    param([array]$ArtifactsToDelete)
    
    if ($ArtifactsToDelete.Count -eq 0) {
        return $true
    }
    
    Write-Host ""
    Write-WarningMessage "The following artifacts will be PERMANENTLY DELETED:"
    $ArtifactsToDelete | ForEach-Object {
        Write-Host "  - $($_.Name) [$($_.Type)]" -ForegroundColor Red
    }
    
    Write-Host ""
    $response = Read-Host "Are you sure you want to delete these artifacts? (y/N)"
    return $response -match "^[Yy]$"
}

# Main execution
try {
    Write-InfoMessage "ChromeConnect Artifact Cleanup Script"
    Write-InfoMessage "Target Directory: $TargetDirectory"
    Write-InfoMessage "Dry Run Mode: $DryRun"
    Write-InfoMessage "Keep Newest: $KeepNewest"
    Write-Host ""
    
    # Validate target directory
    if (-not (Test-Path $TargetDirectory)) {
        Write-ErrorMessage "Target directory does not exist: $TargetDirectory"
        exit 1
    }
    
    # Get all ChromeConnect artifacts
    $allArtifacts = Get-ChromeConnectArtifacts -Directory $TargetDirectory
    
    # Show summary
    Show-ArtifactsSummary -Artifacts $allArtifacts
    
    if ($allArtifacts.Count -eq 0) {
        Write-SuccessMessage "No cleanup needed - no ChromeConnect artifacts found."
        exit 0
    }
    
    # Determine which artifacts to delete
    $artifactsToDelete = Get-ArtifactsToDelete -Artifacts $allArtifacts -KeepNewest $KeepNewest
    
    if ($artifactsToDelete.Count -eq 0) {
        Write-SuccessMessage "No cleanup needed - all artifacts are within retention policy."
        exit 0
    }
    
    Write-Host ""
    Write-InfoMessage "Artifacts selected for deletion: $($artifactsToDelete.Count)"
    
    # Confirm deletion (unless Force is specified or DryRun mode)
    if (-not $Force -and -not $DryRun) {
        if (-not (Confirm-Deletion -ArtifactsToDelete $artifactsToDelete)) {
            Write-InfoMessage "Cleanup cancelled by user."
            exit 0
        }
    }
    
    # Perform cleanup
    Write-Host ""
    $success = Remove-Artifacts -ArtifactsToDelete $artifactsToDelete -DryRunMode $DryRun
    
    if ($success) {
        if ($DryRun) {
            Write-SuccessMessage "Dry run completed successfully. No files were actually deleted."
        } else {
            Write-SuccessMessage "Cleanup completed successfully!"
        }
    } else {
        Write-ErrorMessage "Cleanup completed with errors. Please check the output above."
        exit 1
    }
}
catch {
    Write-ErrorMessage "An unexpected error occurred: $($_.Exception.Message)"
    Write-ErrorMessage "Stack trace: $($_.ScriptStackTrace)"
    exit 1
} 