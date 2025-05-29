# Test-BuildAndExtraction.ps1
# Comprehensive testing procedures for WebConnect build process and DLL extraction
# This script tests environment setup, build process, and DLL extraction functionality

param(
    [string]$TestOutputDir = "./test-results",
    [string]$Configuration = "Release",
    [string]$TestVersion = "1.0.0-test",
    [bool]$CleanupAfterTest = $true,
    [bool]$SkipBuildTest = $false,
    [bool]$VerboseOutput = $false
)

# Test configuration
$ErrorActionPreference = "Continue"  # Continue on errors to collect all test results
$TestResults = @()
$TestStartTime = Get-Date
$ExpectedExtractionPath = "C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect"
$ExpectedHashDir = "BUVKQZGVGMYJUEVNC62UH0NUC1GYHEG="

# Colors for output
$ErrorColor = "Red"
$WarningColor = "Yellow" 
$InfoColor = "Cyan"
$SuccessColor = "Green"
$TestColor = "Magenta"

function Write-TestMessage {
    param([string]$Message)
    Write-Host "TEST: $Message" -ForegroundColor $TestColor
}

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

# Test assertion function
function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message,
        [string]$TestName
    )
    
    $result = @{
        TestName = $TestName
        Message = $Message
        Passed = $Condition
        Timestamp = Get-Date
    }
    
    if ($Condition) {
        Write-SuccessMessage "✓ PASS: $TestName - $Message"
    } else {
        Write-ErrorMessage "✗ FAIL: $TestName - $Message"
    }
    
    $script:TestResults += $result
    return $Condition
}

# Test: Environment Variable Configuration
function Test-EnvironmentVariable {
    Write-TestMessage "Running Test: Environment Variable Configuration"
    
    try {
        # Test 1: Check if environment variable is set
        $envVar = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
        Assert-Condition ($envVar -ne $null) "DOTNET_BUNDLE_EXTRACT_BASE_DIR environment variable exists" "EnvironmentVariable"
        
        if ($envVar) {
            # Test 2: Check if it's set to the expected value
            Assert-Condition ($envVar -eq $ExpectedExtractionPath) "Environment variable set to correct path: $envVar" "EnvironmentVariable"
            
            # Test 3: Check if the path is accessible
            $pathExists = Test-Path $envVar -ErrorAction SilentlyContinue
            Assert-Condition $pathExists "Extraction path is accessible: $envVar" "EnvironmentVariable"
        }
        
        # Test 4: Check system-level environment variable (if running as administrator)
        try {
            $systemEnvVar = [System.Environment]::GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", "Machine")
            if ($systemEnvVar) {
                Assert-Condition ($systemEnvVar -eq $ExpectedExtractionPath) "System-level environment variable set correctly" "EnvironmentVariable"
            } else {
                Write-WarningMessage "System-level environment variable not found (may require administrator privileges)"
            }
        }
        catch {
            Write-WarningMessage "Could not check system-level environment variable: $($_.Exception.Message)"
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "Environment variable test failed: $($_.Exception.Message)"
        Assert-Condition $false "Environment variable test completed without errors" "EnvironmentVariable"
        return $false
    }
}

# Test: Directory Structure and Permissions
function Test-DirectoryStructure {
    Write-TestMessage "Running Test: Directory Structure and Permissions"
    
    try {
        $basePath = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
        if (-not $basePath) {
            $basePath = $ExpectedExtractionPath
        }
        
        # Test 1: Base directory exists
        $baseExists = Test-Path $basePath -ErrorAction SilentlyContinue
        Assert-Condition $baseExists "Base extraction directory exists: $basePath" "DirectoryStructure"
        
        if ($baseExists) {
            # Test 2: Hash-based subdirectory exists
            $hashPath = Join-Path $basePath $ExpectedHashDir
            $hashExists = Test-Path $hashPath -ErrorAction SilentlyContinue
            Assert-Condition $hashExists "Hash-based subdirectory exists: $ExpectedHashDir" "DirectoryStructure"
            
            # Test 3: Write permissions test
            try {
                $testFile = Join-Path $basePath "permission_test_$(Get-Random).tmp"
                "test" | Out-File -FilePath $testFile -ErrorAction Stop
                Remove-Item $testFile -Force -ErrorAction SilentlyContinue
                Assert-Condition $true "Write permissions confirmed for extraction directory" "DirectoryStructure"
            }
            catch {
                Assert-Condition $false "Write permissions test failed: $($_.Exception.Message)" "DirectoryStructure"
            }
            
            # Test 4: Directory permissions inheritance
            try {
                $acl = Get-Acl $basePath -ErrorAction Stop
                $inheritanceEnabled = -not $acl.AreAccessRulesProtected
                Assert-Condition $inheritanceEnabled "Directory inheritance enabled" "DirectoryStructure"
            }
            catch {
                Write-WarningMessage "Could not check directory permissions: $($_.Exception.Message)"
            }
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "Directory structure test failed: $($_.Exception.Message)"
        Assert-Condition $false "Directory structure test completed without errors" "DirectoryStructure"
        return $false
    }
}

# Test: Required Scripts Existence
function Test-RequiredScripts {
    Write-TestMessage "Running Test: Required Scripts Existence"
    
    try {
        # Test 1: Main scripts exist
        $publishScript = "./publish.ps1"
        Assert-Condition (Test-Path $publishScript) "Publish script exists: $publishScript" "RequiredScripts"
        
        $deployScript = "./deploy.ps1"
        Assert-Condition (Test-Path $deployScript) "Deploy script exists: $deployScript" "RequiredScripts"
        
        $extractScript = "./src/WebConnect/ExtractDlls.ps1"
        Assert-Condition (Test-Path $extractScript) "ExtractDlls script exists: $extractScript" "RequiredScripts"
        
        # Test 2: Project file exists
        $projectFile = "./src/WebConnect/WebConnect.csproj"
        Assert-Condition (Test-Path $projectFile) "Project file exists: $projectFile" "RequiredScripts"
        
        # Test 3: Environment setup scripts exist
        $envScripts = @(
            "./scripts/SetEnvironmentVariable.ps1",
            "./scripts/SetSystemEnvironmentVariable.ps1",
            "./scripts/VerifyEnvironmentSetup.ps1"
        )
        
        foreach ($script in $envScripts) {
            $exists = Test-Path $script
            Assert-Condition $exists "Environment script exists: $script" "RequiredScripts"
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "Required scripts test failed: $($_.Exception.Message)"
        Assert-Condition $false "Required scripts test completed without errors" "RequiredScripts"
        return $false
    }
}

# Test: .NET Runtime and Dependencies
function Test-DotnetEnvironment {
    Write-TestMessage "Running Test: .NET Runtime and Dependencies"
    
    try {
        # Test 1: .NET runtime installed
        try {
            $dotnetVersion = dotnet --version 2>$null
            Assert-Condition ($dotnetVersion -ne $null) ".NET runtime is installed: $dotnetVersion" "DotnetEnvironment"
        }
        catch {
            Assert-Condition $false ".NET runtime installation check failed" "DotnetEnvironment"
            return $false
        }
        
        # Test 2: Required .NET version (8.0 or higher)
        if ($dotnetVersion) {
            $versionNumber = [Version]($dotnetVersion -split '-')[0]
            $isValidVersion = $versionNumber.Major -ge 8
            Assert-Condition $isValidVersion ".NET version is 8.0 or higher: $dotnetVersion" "DotnetEnvironment"
        }
        
        # Test 3: Project can be restored
        try {
            Write-InfoMessage "Testing NuGet package restoration..."
            $restoreOutput = dotnet restore "./src/WebConnect" --verbosity quiet 2>&1
            $restoreSuccess = $LASTEXITCODE -eq 0
            Assert-Condition $restoreSuccess "NuGet packages can be restored successfully" "DotnetEnvironment"
        }
        catch {
            Assert-Condition $false "NuGet restore test failed: $($_.Exception.Message)" "DotnetEnvironment"
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage ".NET environment test failed: $($_.Exception.Message)"
        Assert-Condition $false ".NET environment test completed without errors" "DotnetEnvironment"
        return $false
    }
}

# Test: Build Process
function Test-BuildProcess {
    if ($SkipBuildTest) {
        Write-WarningMessage "Skipping build process test (SkipBuildTest=$SkipBuildTest)"
        return $true
    }
    
    Write-TestMessage "Running Test: Build Process"
    
    try {
        # Prepare test output directory
        $testBuildDir = Join-Path $TestOutputDir "build-test"
        if (Test-Path $testBuildDir) {
            Remove-Item $testBuildDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        New-Item -ItemType Directory -Path $testBuildDir -Force | Out-Null
        
        # Test 1: Build process execution
        Write-InfoMessage "Running build process test..."
        try {
            $buildArgs = @(
                "-Version", $TestVersion,
                "-Configuration", $Configuration,
                "-OutputDir", $testBuildDir,
                "-CreateZip", $false,
                "-Clean", $true,
                "-UpdateVersion", $false  # Don't update version for tests
            )
            
            $buildOutput = & "./publish.ps1" @buildArgs 2>&1
            $buildSuccess = $LASTEXITCODE -eq 0
            
            if ($VerboseOutput) {
                Write-InfoMessage "Build output: $buildOutput"
            }
            
            Assert-Condition $buildSuccess "Build process completed successfully" "BuildProcess"
        }
        catch {
            Assert-Condition $false "Build process execution failed: $($_.Exception.Message)" "BuildProcess"
            return $false
        }
        
        # Test 2: Output executable exists
        $executablePath = Join-Path $testBuildDir "WebConnect.exe"
        $executableExists = Test-Path $executablePath
        Assert-Condition $executableExists "Build output executable exists: WebConnect.exe" "BuildProcess"
        
        # Test 3: Executable properties
        if ($executableExists) {
            try {
                $fileInfo = Get-Item $executablePath
                $fileSizeOK = $fileInfo.Length -gt 0
                Assert-Condition $fileSizeOK "Executable has valid file size: $($fileInfo.Length) bytes" "BuildProcess"
                
                # Test if it's a single-file executable (should be large)
                $isSingleFile = $fileInfo.Length -gt 50MB
                Assert-Condition $isSingleFile "Executable appears to be single-file (size > 50MB)" "BuildProcess"
            }
            catch {
                Write-WarningMessage "Could not check executable properties: $($_.Exception.Message)"
            }
        }
        
        # Test 4: Version check (non-blocking)
        if ($executableExists) {
            try {
                $versionOutput = & $executablePath --version 2>&1
                $versionSuccess = $LASTEXITCODE -eq 0
                if ($versionSuccess) {
                    Assert-Condition $true "Executable version check succeeded" "BuildProcess"
                    if ($VerboseOutput) {
                        Write-InfoMessage "Version output: $versionOutput"
                    }
                } else {
                    Write-WarningMessage "Version check failed but executable exists"
                }
            }
            catch {
                Write-WarningMessage "Could not test executable version: $($_.Exception.Message)"
            }
        }
        
        return $buildSuccess
    }
    catch {
        Write-ErrorMessage "Build process test failed: $($_.Exception.Message)"
        Assert-Condition $false "Build process test completed without errors" "BuildProcess"
        return $false
    }
}

# Test: DLL Extraction Simulation
function Test-DllExtractionSimulation {
    Write-TestMessage "Running Test: DLL Extraction Simulation"
    
    try {
        # Test 1: ExtractDlls script execution
        Write-InfoMessage "Testing DLL extraction script..."
        try {
            $extractScript = "./src/WebConnect/ExtractDlls.ps1"
            $extractArgs = @(
                "-OutputPath", (Join-Path $TestOutputDir "dll-test"),
                "-Configuration", $Configuration
            )
            
            $extractOutput = & $extractScript @extractArgs 2>&1
            $extractSuccess = $LASTEXITCODE -eq 0
            
            if ($VerboseOutput) {
                Write-InfoMessage "Extract output: $extractOutput"
            }
            
            Assert-Condition $extractSuccess "DLL extraction script executed successfully" "DllExtraction"
        }
        catch {
            Assert-Condition $false "DLL extraction script execution failed: $($_.Exception.Message)" "DllExtraction"
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "DLL extraction test failed: $($_.Exception.Message)"
        Assert-Condition $false "DLL extraction test completed without errors" "DllExtraction"
        return $false
    }
}

# Test: Deployment Script Functionality
function Test-DeploymentScript {
    Write-TestMessage "Running Test: Deployment Script Functionality"
    
    try {
        # Test 1: Deploy script execution (dry run)
        Write-InfoMessage "Testing deployment script dry run..."
        try {
            $deployScript = "./deploy.ps1"
            $deployArgs = @(
                "-DryRun", $true,
                "-SkipEnvironmentSetup", $true,
                "-SkipValidation", $true
            )
            
            $deployOutput = & $deployScript @deployArgs 2>&1
            $deploySuccess = $LASTEXITCODE -eq 0
            
            if ($VerboseOutput) {
                Write-InfoMessage "Deploy output: $deployOutput"
            }
            
            Assert-Condition $deploySuccess "Deployment script dry run completed successfully" "DeploymentScript"
        }
        catch {
            Assert-Condition $false "Deployment script test failed: $($_.Exception.Message)" "DeploymentScript"
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "Deployment script test failed: $($_.Exception.Message)"
        Assert-Condition $false "Deployment script test completed without errors" "DeploymentScript"
        return $false
    }
}

# Test: Clean Machine Simulation
function Test-CleanMachineSimulation {
    Write-TestMessage "Running Test: Clean Machine Simulation"
    
    try {
        # Test 1: Simulate missing environment variable
        $originalEnvVar = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
        
        try {
            # Temporarily remove environment variable
            $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $null
            
            # Test script behavior without environment variable
            $extractScript = "./src/WebConnect/ExtractDlls.ps1"
            $extractOutput = & $extractScript -OutputPath "dummy" 2>&1
            $extractSuccess = $LASTEXITCODE -eq 0
            
            Assert-Condition $extractSuccess "Scripts handle missing environment variable gracefully" "CleanMachine"
            
            # Check if default path is used
            $usesDefault = $extractOutput -match $ExpectedExtractionPath.Replace('\', '\\')
            Assert-Condition $usesDefault "Default extraction path is used when environment variable is missing" "CleanMachine"
        }
        finally {
            # Restore environment variable
            $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $originalEnvVar
        }
        
        return $true
    }
    catch {
        Write-ErrorMessage "Clean machine simulation test failed: $($_.Exception.Message)"
        Assert-Condition $false "Clean machine simulation test completed without errors" "CleanMachine"
        return $false
    }
}

# Generate Test Report
function Generate-TestReport {
    Write-TestMessage "Generating Test Report"
    
    $testEndTime = Get-Date
    $testDuration = $testEndTime - $TestStartTime
    
    $totalTests = $TestResults.Count
    $passedTests = ($TestResults | Where-Object { $_.Passed }).Count
    $failedTests = $totalTests - $passedTests
    $passRate = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 2) } else { 0 }
    
    # Create report content
    $reportContent = @"
# WebConnect Build and Extraction Test Report
Generated: $($testEndTime.ToString('yyyy-MM-dd HH:mm:ss'))
Duration: $($testDuration.ToString('hh\:mm\:ss'))

## Summary
- Total Tests: $totalTests
- Passed: $passedTests
- Failed: $failedTests
- Pass Rate: $passRate%

## Test Results
"@
    
    # Group results by test category
    $groupedResults = $TestResults | Group-Object TestName
    
    foreach ($group in $groupedResults) {
        $groupPassed = ($group.Group | Where-Object { $_.Passed }).Count
        $groupTotal = $group.Group.Count
        $groupPassRate = if ($groupTotal -gt 0) { [math]::Round(($groupPassed / $groupTotal) * 100, 2) } else { 0 }
        
        $reportContent += "`n`n### $($group.Name) ($groupPassed/$groupTotal - $groupPassRate%)"
        
        foreach ($result in $group.Group) {
            $status = if ($result.Passed) { "✓ PASS" } else { "✗ FAIL" }
            $reportContent += "`n- $status : $($result.Message)"
        }
    }
    
    # Add recommendations
    $reportContent += "`n`n## Recommendations"
    
    $failedCategories = ($TestResults | Where-Object { -not $_.Passed } | Group-Object TestName).Name
    if ($failedCategories.Count -eq 0) {
        $reportContent += "`n- All tests passed! The build and extraction system is working correctly."
    } else {
        $reportContent += "`n- Review and fix the following areas:"
        foreach ($category in $failedCategories) {
            $reportContent += "`n  - $category"
        }
    }
    
    # Save report
    $reportPath = Join-Path $TestOutputDir "test-report.md"
    if (-not (Test-Path $TestOutputDir)) {
        New-Item -ItemType Directory -Path $TestOutputDir -Force | Out-Null
    }
    
    $reportContent | Out-File -FilePath $reportPath -Encoding UTF8
    
    # Display summary
    Write-Host "`n" -NoNewline
    Write-Host "=" * 60 -ForegroundColor Gray
    Write-Host "TEST SUMMARY" -ForegroundColor $TestColor
    Write-Host "=" * 60 -ForegroundColor Gray
    
    if ($failedTests -eq 0) {
        Write-SuccessMessage "All $totalTests tests PASSED! ✓"
    } else {
        Write-ErrorMessage "$failedTests out of $totalTests tests FAILED! ✗"
    }
    
    Write-Host "Pass Rate: $passRate%" -ForegroundColor $(if ($passRate -ge 90) { $SuccessColor } elseif ($passRate -ge 70) { $WarningColor } else { $ErrorColor })
    Write-Host "Duration: $($testDuration.ToString('hh\:mm\:ss'))" -ForegroundColor $InfoColor
    Write-Host "Report saved: $reportPath" -ForegroundColor $InfoColor
    Write-Host "=" * 60 -ForegroundColor Gray
    
    return ($failedTests -eq 0)
}

# Cleanup function
function Cleanup-TestResources {
    if (-not $CleanupAfterTest) {
        Write-InfoMessage "Skipping cleanup (CleanupAfterTest=$CleanupAfterTest)"
        return
    }
    
    Write-InfoMessage "Cleaning up test resources..."
    
    try {
        # Clean up test output directory
        $testBuildDir = Join-Path $TestOutputDir "build-test"
        if (Test-Path $testBuildDir) {
            Remove-Item $testBuildDir -Recurse -Force -ErrorAction SilentlyContinue
            Write-InfoMessage "Cleaned up test build directory"
        }
        
        $dllTestDir = Join-Path $TestOutputDir "dll-test"
        if (Test-Path $dllTestDir) {
            Remove-Item $dllTestDir -Recurse -Force -ErrorAction SilentlyContinue
            Write-InfoMessage "Cleaned up DLL test directory"
        }
        
        Write-SuccessMessage "Test cleanup completed"
    }
    catch {
        Write-WarningMessage "Error during cleanup: $($_.Exception.Message)"
    }
}

# Main execution
try {
    Write-Host "`n" -NoNewline
    Write-Host "=" * 60 -ForegroundColor Gray
    Write-Host "WEBCONNECT BUILD & EXTRACTION TESTING" -ForegroundColor $TestColor
    Write-Host "=" * 60 -ForegroundColor Gray
    Write-InfoMessage "Starting comprehensive testing at $($TestStartTime.ToString('yyyy-MM-dd HH:mm:ss'))"
    Write-InfoMessage "Test output directory: $TestOutputDir"
    Write-InfoMessage "Configuration: $Configuration"
    Write-InfoMessage "Test version: $TestVersion"
    
    if ($VerboseOutput) {
        Write-InfoMessage "Verbose output enabled"
    }
    
    # Create test output directory
    if (-not (Test-Path $TestOutputDir)) {
        New-Item -ItemType Directory -Path $TestOutputDir -Force | Out-Null
    }
    
    # Run all tests
    $testFunctions = @(
        { Test-EnvironmentVariable },
        { Test-DirectoryStructure },
        { Test-RequiredScripts },
        { Test-DotnetEnvironment },
        { Test-BuildProcess },
        { Test-DllExtractionSimulation },
        { Test-DeploymentScript },
        { Test-CleanMachineSimulation }
    )
    
    $allTestsPassed = $true
    
    foreach ($testFunction in $testFunctions) {
        try {
            $testResult = & $testFunction
            if (-not $testResult) {
                $allTestsPassed = $false
            }
        }
        catch {
            Write-ErrorMessage "Test execution error: $($_.Exception.Message)"
            $allTestsPassed = $false
        }
        
        Write-Host ""  # Add spacing between tests
    }
    
    # Generate final report
    $reportSuccess = Generate-TestReport
    
    # Cleanup
    Cleanup-TestResources
    
    # Exit with appropriate code
    if ($allTestsPassed -and $reportSuccess) {
        Write-SuccessMessage "All testing procedures completed successfully!"
        exit 0
    } else {
        Write-ErrorMessage "Some tests failed. Please review the test report."
        exit 1
    }
}
catch {
    Write-ErrorMessage "Fatal error during testing: $($_.Exception.Message)"
    Write-ErrorMessage "Stack trace: $($_.ScriptStackTrace)"
    exit 1
} 