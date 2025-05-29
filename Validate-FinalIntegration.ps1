#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Final Integration and Validation Script for ChromeConnect DLL Extraction Solution

.DESCRIPTION
    This script performs comprehensive integration testing and validation of the entire
    DLL extraction solution for ChromeConnect.

.PARAMETER TestVersion
    Version to use for the integration test (default: 1.0.5-integration-test)

.PARAMETER CleanAll
    Whether to perform a complete clean before testing (default: true)

.PARAMETER SkipBuild
    Whether to skip the build test and use existing build (default: false)

.PARAMETER GenerateReport
    Whether to generate a detailed final report (default: true)

.PARAMETER VerboseLogging
    Enable verbose logging output (default: false)

.EXAMPLE
    .\Validate-FinalIntegration.ps1
    
.EXAMPLE
    .\Validate-FinalIntegration.ps1 -TestVersion "1.0.5-final" -VerboseLogging $true
#>

param (
    [string]$TestVersion = "1.0.5-integration-test",
    [bool]$CleanAll = $true,
    [bool]$SkipBuild = $false,
    [bool]$GenerateReport = $true,
    [bool]$VerboseLogging = $false
)

# Script configuration
$ErrorActionPreference = "Continue"
$IntegrationStartTime = Get-Date
$ValidationResults = @()
$ExpectedExtractionPath = "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect"
$TestOutputDir = "./integration-test-results"
$ReportFile = "$TestOutputDir/FinalIntegrationReport.md"

# Colors for output
$Colors = @{
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
    Success = "Green"
    Test = "Magenta"
    Header = "White"
}

# Logging functions
function Write-HeaderMessage {
    param([string]$Message)
    Write-Host ""
    Write-Host "=================================================================" -ForegroundColor $Colors.Header
    Write-Host $Message -ForegroundColor $Colors.Header
    Write-Host "=================================================================" -ForegroundColor $Colors.Header
    Write-Host ""
}

function Write-TestMessage {
    param([string]$Message)
    Write-Host "TEST: $Message" -ForegroundColor $Colors.Test
}

function Write-InfoMessage {
    param([string]$Message)
    Write-Host "INFO: $Message" -ForegroundColor $Colors.Info
    if ($VerboseLogging) {
        Add-Content -Path "$TestOutputDir/verbose.log" -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - INFO: $Message" -ErrorAction SilentlyContinue
    }
}

function Write-SuccessMessage {
    param([string]$Message)
    Write-Host "SUCCESS: $Message" -ForegroundColor $Colors.Success
}

function Write-WarningMessage {
    param([string]$Message)
    Write-Host "WARNING: $Message" -ForegroundColor $Colors.Warning
}

function Write-ErrorMessage {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor $Colors.Error
}

# Test assertion function
function Assert-ValidationCriteria {
    param(
        [bool]$Condition,
        [string]$Message,
        [string]$Category,
        [string]$Details = ""
    )
    
    $result = @{
        Category = $Category
        Message = $Message
        Passed = $Condition
        Details = $Details
        Timestamp = Get-Date
    }
    
    if ($Condition) {
        Write-SuccessMessage "PASS: $Category - $Message"
    } else {
        Write-ErrorMessage "FAIL: $Category - $Message"
        if ($Details) {
            Write-ErrorMessage "  Details: $Details"
        }
    }
    
    $script:ValidationResults += $result
    return $Condition
}

# Initialize test environment
function Initialize-TestEnvironment {
    Write-HeaderMessage "INITIALIZING TEST ENVIRONMENT"
    
    try {
        # Create test output directory
        if ($CleanAll -and (Test-Path $TestOutputDir)) {
            Write-InfoMessage "Cleaning previous test results..."
            Remove-Item $TestOutputDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        
        if (-not (Test-Path $TestOutputDir)) {
            New-Item -ItemType Directory -Path $TestOutputDir -Force | Out-Null
            Write-InfoMessage "Created test output directory: $TestOutputDir"
        }
        
        # Initialize log files
        if ($VerboseLogging) {
            "Integration Test Started: $(Get-Date)" | Out-File -FilePath "$TestOutputDir/verbose.log" -Force
        }
        
        # Check prerequisites
        Write-InfoMessage "Checking prerequisites..."
        
        # Check .NET installation
        try {
            $dotnetVersion = dotnet --version
            Write-InfoMessage ".NET version: $dotnetVersion"
            Assert-ValidationCriteria $true "Prerequisites - .NET SDK installed" "Prerequisites" "Version: $dotnetVersion"
        }
        catch {
            Assert-ValidationCriteria $false "Prerequisites - .NET SDK installation" "Prerequisites" $_.Exception.Message
            return $false
        }
        
        # Check PowerShell execution policy
        $executionPolicy = Get-ExecutionPolicy
        Write-InfoMessage "PowerShell execution policy: $executionPolicy"
        Assert-ValidationCriteria ($executionPolicy -ne "Restricted") "Prerequisites - PowerShell execution policy allows scripts" "Prerequisites" "Policy: $executionPolicy"
        
        return $true
    }
    catch {
        Write-ErrorMessage "Failed to initialize test environment: $($_.Exception.Message)"
        return $false
    }
}

# Test 1: Environment Variable Configuration
function Test-EnvironmentConfiguration {
    Write-HeaderMessage "VALIDATION 1: ENVIRONMENT VARIABLE CONFIGURATION"
    
    try {
        # Check current environment variable
        $envVar = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
        Assert-ValidationCriteria ($envVar -ne $null) "Environment variable DOTNET_BUNDLE_EXTRACT_BASE_DIR exists" "EnvironmentConfig" "Value: $envVar"
        
        if ($envVar) {
            Assert-ValidationCriteria ($envVar -eq $ExpectedExtractionPath) "Environment variable set to correct path" "EnvironmentConfig" "Expected: $ExpectedExtractionPath, Actual: $envVar"
        }
        
        # Check system-level environment variable
        try {
            $systemEnvVar = [System.Environment]::GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", "Machine")
            if ($systemEnvVar) {
                Assert-ValidationCriteria ($systemEnvVar -eq $ExpectedExtractionPath) "System-level environment variable configured correctly" "EnvironmentConfig" "System value: $systemEnvVar"
            } else {
                Write-InfoMessage "System-level environment variable not found - testing current session variable only"
            }
        }
        catch {
            Write-WarningMessage "Could not check system-level environment variable: $($_.Exception.Message)"
        }
        
        # Test environment setup scripts
        $setupScripts = @(
            "scripts\SetEnvironmentVariable.ps1",
            "scripts\SetSystemEnvironmentVariable.ps1",
            "scripts\VerifyEnvironmentSetup.ps1"
        )
        
        foreach ($script in $setupScripts) {
            $exists = Test-Path $script
            Assert-ValidationCriteria $exists "Environment setup script exists: $(Split-Path $script -Leaf)" "EnvironmentConfig" "Path: $script"
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "Environment configuration test failed: $($_.Exception.Message)"
        return $false
    }
}

# Test 2: Clean Build Process
function Test-CleanBuildProcess {
    Write-HeaderMessage "VALIDATION 2: CLEAN BUILD AND PUBLISH PROCESS"
    
    if ($SkipBuild) {
        Write-InfoMessage "Skipping build process (SkipBuild = true)"
        return $true
    }
    
    try {
        Write-InfoMessage "Starting clean build process..."
        
        # Clean any previous builds
        if ($CleanAll) {
            Write-InfoMessage "Cleaning previous builds..."
            if (Test-Path "./publish") {
                Remove-Item "./publish" -Recurse -Force -ErrorAction SilentlyContinue
            }
            if (Test-Path "./src/ChromeConnect/bin") {
                Remove-Item "./src/ChromeConnect/bin" -Recurse -Force -ErrorAction SilentlyContinue
            }
            if (Test-Path "./src/ChromeConnect/obj") {
                Remove-Item "./src/ChromeConnect/obj" -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        
        # Run publish script
        Write-InfoMessage "Running publish script with version $TestVersion..."
        $publishArgs = @(
            "-Version", $TestVersion,
            "-Configuration", "Release",
            "-CreateZip", "$true",
            "-Clean", "$CleanAll",
            "-UpdateVersion", "$false"
        )
        
        $publishStartTime = Get-Date
        try {
            & "./publish.ps1" @publishArgs
            $publishExitCode = $LASTEXITCODE
            $publishEndTime = Get-Date
            $publishDuration = $publishEndTime - $publishStartTime
            
            Write-InfoMessage "Publish script completed in $($publishDuration.TotalSeconds) seconds with exit code: $publishExitCode"
            Assert-ValidationCriteria ($publishExitCode -eq 0) "Build process completed successfully" "BuildProcess" "Exit code: $publishExitCode, Duration: $($publishDuration.TotalSeconds)s"
        }
        catch {
            Assert-ValidationCriteria $false "Build process execution" "BuildProcess" $_.Exception.Message
            return $false
        }
        
        # Verify build outputs
        $executablePath = "./publish/ChromeConnect.exe"
        $executableExists = Test-Path $executablePath
        Assert-ValidationCriteria $executableExists "Executable created successfully" "BuildProcess" "Path: $executablePath"
        
        if ($executableExists) {
            $fileSize = (Get-Item $executablePath).Length
            $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
            Write-InfoMessage "Executable size: $fileSizeMB MB"
            Assert-ValidationCriteria ($fileSize -gt 0) "Executable has valid size" "BuildProcess" "Size: $fileSizeMB MB"
        }
        
        # Verify ZIP package
        $zipPath = "./ChromeConnect-$TestVersion-win-x64.zip"
        $zipExists = Test-Path $zipPath
        Assert-ValidationCriteria $zipExists "ZIP package created successfully" "BuildProcess" "Path: $zipPath"
        
        return $true
    }
    catch {
        Write-ErrorMessage "Build process test failed: $($_.Exception.Message)"
        Assert-ValidationCriteria $false "Build process completed without errors" "BuildProcess" $_.Exception.Message
        return $false
    }
}

# Test 3: DLL Extraction Validation
function Test-DllExtractionProcess {
    Write-HeaderMessage "VALIDATION 3: DLL EXTRACTION TO TARGET DIRECTORY"
    
    try {
        # Verify extraction path setup
        $extractionPath = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
        if (-not $extractionPath) {
            $extractionPath = $ExpectedExtractionPath
        }
        
        Write-InfoMessage "Testing DLL extraction to: $extractionPath"
        
        # Check if extraction directory exists and is accessible
        $pathExists = Test-Path $extractionPath -ErrorAction SilentlyContinue
        Assert-ValidationCriteria $pathExists "Extraction directory is accessible" "DllExtraction" "Path: $extractionPath"
        
        if ($pathExists) {
            # Test write permissions
            try {
                $testFile = Join-Path $extractionPath "integration_test_$(Get-Random).tmp"
                "test" | Out-File -FilePath $testFile -ErrorAction Stop
                Remove-Item $testFile -Force -ErrorAction SilentlyContinue
                Assert-ValidationCriteria $true "Write permissions confirmed for extraction directory" "DllExtraction" ""
            }
            catch {
                Assert-ValidationCriteria $false "Write permissions test" "DllExtraction" $_.Exception.Message
            }
            
            # Check for existing extracted DLLs
            $preExtractionDirs = Get-ChildItem $extractionPath -Directory -ErrorAction SilentlyContinue
            Write-InfoMessage "Found $($preExtractionDirs.Count) existing directories in extraction path"
            
            # Test actual DLL extraction if executable exists
            $executablePath = "./publish/ChromeConnect.exe"
            if (Test-Path $executablePath) {
                Write-InfoMessage "Testing DLL extraction with built executable..."
                
                try {
                    # Set environment variable for this test
                    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $extractionPath
                    
                    # Run application briefly to trigger extraction
                    $process = Start-Process -FilePath $executablePath -ArgumentList "--version" -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput "nul" -ErrorAction Stop
                    $exitCode = $process.ExitCode
                    
                    Write-InfoMessage "Extraction test completed with exit code: $exitCode"
                    
                    # Wait for file system to update
                    Start-Sleep -Milliseconds 1000
                    
                    # Check for extracted DLLs
                    $postExtractionDirs = Get-ChildItem $extractionPath -Directory -ErrorAction SilentlyContinue
                    $totalDlls = 0
                    $extractedDirCount = 0
                    
                    foreach ($dir in $postExtractionDirs) {
                        $dllFiles = Get-ChildItem $dir.FullName -Filter "*.dll" -Recurse -ErrorAction SilentlyContinue
                        if ($dllFiles.Count -gt 0) {
                            $totalDlls += $dllFiles.Count
                            $extractedDirCount++
                            Write-InfoMessage "Found extraction directory: $($dir.Name) with $($dllFiles.Count) DLL files"
                        }
                    }
                    
                    Assert-ValidationCriteria ($extractedDirCount -gt 0) "DLL extraction directories created in target path" "DllExtraction" "Directories: $extractedDirCount, Total DLLs: $totalDlls"
                    Assert-ValidationCriteria ($totalDlls -gt 0) "DLL files extracted successfully" "DllExtraction" "Total DLL files found: $totalDlls"
                }
                catch {
                    Assert-ValidationCriteria $false "DLL extraction execution test" "DllExtraction" $_.Exception.Message
                }
            } else {
                Write-WarningMessage "Executable not found for DLL extraction test"
            }
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "DLL extraction test failed: $($_.Exception.Message)"
        return $false
    }
}

# Test 4: Deployment Package Validation
function Test-DeploymentPackage {
    Write-HeaderMessage "VALIDATION 4: DEPLOYMENT PACKAGE INCLUDES EXTRACTED DLLS"
    
    try {
        # Check if ExtractedDLLs folder exists in publish output
        $extractedDllsPath = "./publish/ExtractedDLLs"
        $extractedDllsExists = Test-Path $extractedDllsPath
        Assert-ValidationCriteria $extractedDllsExists "ExtractedDLLs folder included in deployment package" "DeploymentPackage" "Path: $extractedDllsPath"
        
        if ($extractedDllsExists) {
            # Check contents of ExtractedDLLs folder
            $extractedDirs = Get-ChildItem $extractedDllsPath -Directory -ErrorAction SilentlyContinue
            Assert-ValidationCriteria ($extractedDirs.Count -gt 0) "ExtractedDLLs folder contains extraction directories" "DeploymentPackage" "Directories found: $($extractedDirs.Count)"
            
            # Verify DLL files in extracted directories
            $totalDllsInPackage = 0
            foreach ($dir in $extractedDirs) {
                $dllFiles = Get-ChildItem $dir.FullName -Filter "*.dll" -Recurse -ErrorAction SilentlyContinue
                $totalDllsInPackage += $dllFiles.Count
            }
            
            Assert-ValidationCriteria ($totalDllsInPackage -gt 0) "DLL files included in deployment package" "DeploymentPackage" "Total DLLs: $totalDllsInPackage"
        }
        
        # Check ZIP package includes ExtractedDLLs
        $zipPath = "./ChromeConnect-$TestVersion-win-x64.zip"
        if (Test-Path $zipPath) {
            try {
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
                $extractedDllsEntry = $zip.Entries | Where-Object { $_.FullName -like "*ExtractedDLLs*" }
                $hasExtractedDlls = $extractedDllsEntry.Count -gt 0
                $zip.Dispose()
                
                Assert-ValidationCriteria $hasExtractedDlls "ZIP package includes ExtractedDLLs folder" "DeploymentPackage" "Entries found: $($extractedDllsEntry.Count)"
            }
            catch {
                Write-WarningMessage "Could not verify ZIP contents: $($_.Exception.Message)"
            }
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "Deployment package test failed: $($_.Exception.Message)"
        return $false
    }
}

# Test 5: Existing Functionality
function Test-ExistingFunctionality {
    Write-HeaderMessage "VALIDATION 5: EXISTING FUNCTIONALITY PRESERVED"
    
    try {
        # Test executable can run basic commands
        $executablePath = "./publish/ChromeConnect.exe"
        if (Test-Path $executablePath) {
            try {
                # Test version command
                $process = Start-Process -FilePath $executablePath -ArgumentList "--version" -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput "$TestOutputDir/version-output.txt" -ErrorAction Stop
                $exitCode = $process.ExitCode
                
                Assert-ValidationCriteria ($exitCode -eq 0) "Application responds to --version command" "FunctionalityPreservation" "Exit code: $exitCode"
                
                # Test help command
                $process = Start-Process -FilePath $executablePath -ArgumentList "--help" -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput "$TestOutputDir/help-output.txt" -ErrorAction Stop
                $exitCode = $process.ExitCode
                
                Assert-ValidationCriteria ($exitCode -eq 0) "Application responds to --help command" "FunctionalityPreservation" "Exit code: $exitCode"
            }
            catch {
                Assert-ValidationCriteria $false "Basic functionality test" "FunctionalityPreservation" $_.Exception.Message
            }
        } else {
            Assert-ValidationCriteria $false "Executable available for functionality testing" "FunctionalityPreservation" "Path: $executablePath"
        }
        
        # Verify configuration files are preserved
        $configFile = "./publish/appsettings.json"
        $configExists = Test-Path $configFile
        Assert-ValidationCriteria $configExists "Configuration file preserved in deployment" "FunctionalityPreservation" "Path: $configFile"
        
        return $true
    }
    catch {
        Write-ErrorMessage "Existing functionality test failed: $($_.Exception.Message)"
        return $false
    }
}

# Test 6: Script Integration
function Test-ScriptIntegration {
    Write-HeaderMessage "VALIDATION 6: SCRIPT INTEGRATION AND BUILD PROCESS"
    
    try {
        # Check core scripts exist
        $coreScripts = @(
            "./publish.ps1",
            "./deploy.ps1",
            "./scripts/ExtractDlls.ps1"
        )
        
        foreach ($script in $coreScripts) {
            $exists = Test-Path $script
            Assert-ValidationCriteria $exists "Core script exists: $(Split-Path $script -Leaf)" "ScriptIntegration" "Path: $script"
        }
        
        # Verify publish script has DLL extraction integration
        if (Test-Path "./publish.ps1") {
            $publishContent = Get-Content "./publish.ps1" -Raw
            $hasExtractionCode = $publishContent -match "ExtractDlls\.ps1|ExtractedDLLs|DOTNET_BUNDLE_EXTRACT_BASE_DIR"
            Assert-ValidationCriteria $hasExtractionCode "Publish script contains DLL extraction integration" "ScriptIntegration" ""
        }
        
        # Check if build completed without errors (from previous test)
        $buildSuccessful = ($ValidationResults | Where-Object { $_.Category -eq "BuildProcess" -and $_.Message -match "completed successfully" }).Count -gt 0
        Assert-ValidationCriteria $buildSuccessful "Build process integration successful" "ScriptIntegration" ""
        
        return $true
    }
    catch {
        Write-ErrorMessage "Script integration test failed: $($_.Exception.Message)"
        return $false
    }
}

# Generate final report
function Generate-FinalReport {
    try {
        Write-InfoMessage "Generating final integration report..."
        
        $reportContent = @()
        $reportContent += "# ChromeConnect DLL Extraction Solution - Final Integration Report"
        $reportContent += ""
        $reportContent += "**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        $reportContent += "**Test Version:** $TestVersion"
        $reportContent += "**Test Duration:** $([math]::Round(((Get-Date) - $IntegrationStartTime).TotalMinutes, 2)) minutes"
        $reportContent += ""
        $reportContent += "## Executive Summary"
        $reportContent += ""
        $reportContent += "This report summarizes the final integration testing and validation of the ChromeConnect DLL extraction solution."
        $reportContent += ""
        
        # Calculate overall statistics
        $totalValidations = $ValidationResults.Count
        $passedValidations = ($ValidationResults | Where-Object { $_.Passed }).Count
        $failedValidations = $totalValidations - $passedValidations
        $successRate = if ($totalValidations -gt 0) { [math]::Round(($passedValidations / $totalValidations) * 100, 1) } else { 0 }
        
        $reportContent += "## Validation Results Summary"
        $reportContent += ""
        $reportContent += "**Overall Success Rate:** $successRate% ($passedValidations of $totalValidations validations passed)"
        $reportContent += ""
        $reportContent += "- **Passed:** $passedValidations validations"
        $reportContent += "- **Failed:** $failedValidations validations"
        $reportContent += ""
        
        # Group results by category
        $categorizedResults = $ValidationResults | Group-Object -Property Category
        
        foreach ($category in $categorizedResults) {
            $categoryPassed = ($category.Group | Where-Object { $_.Passed }).Count
            $categoryTotal = $category.Group.Count
            $categoryRate = [math]::Round(($categoryPassed / $categoryTotal) * 100, 1)
            
            $reportContent += "### $($category.Name) - $categoryRate% ($categoryPassed of $categoryTotal)"
            $reportContent += ""
            
            foreach ($result in $category.Group) {
                $status = if ($result.Passed) { "PASS" } else { "FAIL" }
                $line = "- **$status:** $($result.Message)"
                if ($result.Details) {
                    $line += " - $($result.Details)"
                }
                $reportContent += $line
            }
            $reportContent += ""
        }
        
        # Add success criteria validation
        $reportContent += "## Success Criteria Validation"
        $reportContent += ""
        
        $dllExtractionResults = $ValidationResults | Where-Object { $_.Category -eq "DllExtraction" }
        $dllExtractionPassed = ($dllExtractionResults | Where-Object { $_.Passed }).Count -eq $dllExtractionResults.Count
        $reportContent += "1. **DLLs extract to C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect\**"
        $reportContent += "   - Status: $(if ($dllExtractionPassed) { 'VALIDATED' } else { 'FAILED' })"
        $reportContent += ""
        
        $buildResults = $ValidationResults | Where-Object { $_.Category -eq "BuildProcess" }
        $buildPassed = ($buildResults | Where-Object { $_.Passed }).Count -eq $buildResults.Count
        $reportContent += "2. **Application builds and publishes successfully**"
        $reportContent += "   - Status: $(if ($buildPassed) { 'VALIDATED' } else { 'FAILED' })"
        $reportContent += ""
        
        $packageResults = $ValidationResults | Where-Object { $_.Category -eq "DeploymentPackage" }
        $packagePassed = ($packageResults | Where-Object { $_.Passed }).Count -eq $packageResults.Count
        $reportContent += "3. **Extracted DLL folder is included in deployment package**"
        $reportContent += "   - Status: $(if ($packagePassed) { 'VALIDATED' } else { 'FAILED' })"
        $reportContent += ""
        
        $envResults = $ValidationResults | Where-Object { $_.Category -eq "EnvironmentConfig" }
        $envPassed = ($envResults | Where-Object { $_.Passed }).Count -eq $envResults.Count
        $reportContent += "4. **Environment variable configuration**"
        $reportContent += "   - Status: $(if ($envPassed) { 'VALIDATED' } else { 'FAILED' })"
        $reportContent += ""
        
        $funcResults = $ValidationResults | Where-Object { $_.Category -eq "FunctionalityPreservation" }
        $funcPassed = ($funcResults | Where-Object { $_.Passed }).Count -eq $funcResults.Count
        $reportContent += "5. **Existing functionality remains unchanged**"
        $reportContent += "   - Status: $(if ($funcPassed) { 'VALIDATED' } else { 'FAILED' })"
        $reportContent += ""
        
        $scriptResults = $ValidationResults | Where-Object { $_.Category -eq "ScriptIntegration" }
        $scriptPassed = ($scriptResults | Where-Object { $_.Passed }).Count -eq $scriptResults.Count
        $reportContent += "6. **Build process completes without errors**"
        $reportContent += "   - Status: $(if ($scriptPassed) { 'VALIDATED' } else { 'FAILED' })"
        $reportContent += ""
        
        # Final conclusion
        if ($failedValidations -eq 0) {
            $reportContent += "## Conclusion"
            $reportContent += ""
            $reportContent += "**All validation criteria have been successfully met.**"
            $reportContent += ""
            $reportContent += "The DLL extraction solution is ready for production deployment."
        } else {
            $reportContent += "## Issues Identified"
            $reportContent += ""
            $reportContent += "The following validations failed and require attention:"
            $reportContent += ""
            
            $failedResults = $ValidationResults | Where-Object { -not $_.Passed }
            foreach ($failed in $failedResults) {
                $reportContent += "- **$($failed.Category):** $($failed.Message)"
                if ($failed.Details) {
                    $reportContent += "  - $($failed.Details)"
                }
            }
        }
        
        $reportContent += ""
        $reportContent += "---"
        $reportContent += "*Report generated by Validate-FinalIntegration.ps1*"
        
        # Write report to file
        $reportContent | Out-File -FilePath $ReportFile -Encoding UTF8 -Force
        Write-SuccessMessage "Final integration report generated: $ReportFile"
        
        return $true
    }
    catch {
        Write-ErrorMessage "Failed to generate final report: $($_.Exception.Message)"
        return $false
    }
}

# Main execution
try {
    Write-HeaderMessage "CHROMECONNECT DLL EXTRACTION SOLUTION - FINAL INTEGRATION VALIDATION"
    Write-InfoMessage "Test Version: $TestVersion"
    Write-InfoMessage "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-InfoMessage "Test Output Directory: $TestOutputDir"
    
    # Initialize test environment
    $initSuccess = Initialize-TestEnvironment
    if (-not $initSuccess) {
        Write-ErrorMessage "Failed to initialize test environment. Aborting integration test."
        exit 1
    }
    
    # Run all validation tests
    $validationTests = @(
        @{ Name = "Environment Configuration"; Function = "Test-EnvironmentConfiguration" },
        @{ Name = "Clean Build Process"; Function = "Test-CleanBuildProcess" },
        @{ Name = "DLL Extraction Process"; Function = "Test-DllExtractionProcess" },
        @{ Name = "Deployment Package"; Function = "Test-DeploymentPackage" },
        @{ Name = "Existing Functionality"; Function = "Test-ExistingFunctionality" },
        @{ Name = "Script Integration"; Function = "Test-ScriptIntegration" }
    )
    
    $allTestsPassed = $true
    foreach ($test in $validationTests) {
        try {
            $testResult = & $test.Function
            if (-not $testResult) {
                $allTestsPassed = $false
            }
        }
        catch {
            Write-ErrorMessage "Test '$($test.Name)' failed with exception: $($_.Exception.Message)"
            $allTestsPassed = $false
        }
    }
    
    # Generate final report
    if ($GenerateReport) {
        Generate-FinalReport | Out-Null
    }
    
    # Final summary
    $endTime = Get-Date
    $totalDuration = $endTime - $IntegrationStartTime
    
    Write-HeaderMessage "FINAL INTEGRATION VALIDATION COMPLETE"
    
    $totalValidations = $ValidationResults.Count
    $passedValidations = ($ValidationResults | Where-Object { $_.Passed }).Count
    $failedValidations = $totalValidations - $passedValidations
    $successRate = if ($totalValidations -gt 0) { [math]::Round(($passedValidations / $totalValidations) * 100, 1) } else { 0 }
    
    Write-Host "Duration:           $([math]::Round($totalDuration.TotalMinutes, 2)) minutes" -ForegroundColor White
    Write-Host "Total Validations:  $totalValidations" -ForegroundColor White
    Write-Host "Passed:             $passedValidations" -ForegroundColor $Colors.Success
    Write-Host "Failed:             $failedValidations" -ForegroundColor $(if ($failedValidations -gt 0) { $Colors.Error } else { $Colors.Success })
    Write-Host "Success Rate:       $successRate%" -ForegroundColor $(if ($successRate -eq 100) { $Colors.Success } else { $Colors.Warning })
    
    if ($GenerateReport) {
        Write-Host "Report:             $ReportFile" -ForegroundColor White
    }
    
    Write-Host ""
    if ($allTestsPassed -and $failedValidations -eq 0) {
        Write-SuccessMessage "ALL VALIDATION CRITERIA MET - SOLUTION READY FOR DEPLOYMENT"
        exit 0
    } else {
        Write-ErrorMessage "VALIDATION ISSUES DETECTED - REVIEW REQUIRED BEFORE DEPLOYMENT"
        exit 1
    }
}
catch {
    Write-ErrorMessage "Critical error during integration validation: $($_.Exception.Message)"
    exit 1
} 