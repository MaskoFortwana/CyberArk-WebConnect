# run-tests.ps1
# Wrapper script to run ChromeConnect testing procedures with predefined configurations

param(
    [ValidateSet("Quick", "Full", "BuildOnly", "EnvironmentOnly")]
    [string]$TestSuite = "Quick",
    [bool]$VerboseOutput = $false,
    [bool]$SkipCleanup = $false
)

# Test suite configurations
$TestConfigurations = @{
    "Quick" = @{
        SkipBuildTest = $true
        CleanupAfterTest = $true
        Description = "Quick test suite - skips build process for faster execution"
    }
    "Full" = @{
        SkipBuildTest = $false
        CleanupAfterTest = $true
        Description = "Complete test suite - includes full build process"
    }
    "BuildOnly" = @{
        SkipBuildTest = $false
        CleanupAfterTest = $true
        Description = "Build-focused test suite - primarily tests build and compilation"
    }
    "EnvironmentOnly" = @{
        SkipBuildTest = $true
        CleanupAfterTest = $true
        Description = "Environment-focused test suite - tests setup and configuration"
    }
}

function Write-InfoMessage {
    param([string]$Message)
    Write-Host "INFO: $Message" -ForegroundColor Cyan
}

function Write-SuccessMessage {
    param([string]$Message)
    Write-Host "SUCCESS: $Message" -ForegroundColor Green
}

function Write-ErrorMessage {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

try {
    $config = $TestConfigurations[$TestSuite]
    if (-not $config) {
        Write-ErrorMessage "Unknown test suite: $TestSuite"
        Write-InfoMessage "Available test suites: $($TestConfigurations.Keys -join ', ')"
        exit 1
    }
    
    Write-InfoMessage "Running $TestSuite test suite"
    Write-InfoMessage $config.Description
    
    # Prepare test arguments
    $testArgs = @(
        "-VerboseOutput", $VerboseOutput,
        "-SkipBuildTest", $config.SkipBuildTest,
        "-CleanupAfterTest", (-not $SkipCleanup)
    )
    
    # Add suite-specific configurations
    switch ($TestSuite) {
        "BuildOnly" {
            # For build-only tests, we might want different settings
            $testArgs += @("-TestOutputDir", "./test-results/build-only")
        }
        "EnvironmentOnly" {
            # For environment-only tests
            $testArgs += @("-TestOutputDir", "./test-results/environment-only")
        }
        default {
            $testArgs += @("-TestOutputDir", "./test-results/$($TestSuite.ToLower())")
        }
    }
    
    Write-InfoMessage "Executing Test-BuildAndExtraction.ps1 with arguments: $($testArgs -join ' ')"
    
    # Run the main testing script
    $testResult = & "./Test-BuildAndExtraction.ps1" @testArgs
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -eq 0) {
        Write-SuccessMessage "$TestSuite test suite completed successfully!"
    } else {
        Write-ErrorMessage "$TestSuite test suite failed with exit code $exitCode"
    }
    
    exit $exitCode
}
catch {
    Write-ErrorMessage "Failed to run test suite: $($_.Exception.Message)"
    exit 1
} 