# ChromeConnect Testing Procedures

This document describes the comprehensive testing procedures for the ChromeConnect build process and DLL extraction functionality.

## Overview

The testing system consists of multiple components designed to validate the entire build and deployment pipeline:

- **Test-BuildAndExtraction.ps1**: Main testing script with comprehensive test cases
- **run-tests.ps1**: Wrapper script for running different test suites
- **test-config.json**: Configuration file for test parameters
- **TESTING.md**: This documentation file

## Test Categories

### 1. Environment Variable Tests
- Validates `DOTNET_BUNDLE_EXTRACT_BASE_DIR` environment variable setup
- Checks system-level and user-level environment variables
- Verifies path accessibility and permissions

### 2. Directory Structure Tests
- Validates extraction directory structure creation
- Tests hash-based subdirectory creation
- Verifies directory permissions and inheritance
- Tests cleanup of old extraction directories

### 3. Required Scripts Tests
- Checks existence of all required PowerShell scripts
- Validates project file and configuration files
- Ensures environment setup scripts are available

### 4. .NET Environment Tests
- Validates .NET runtime installation and version
- Tests NuGet package restoration
- Checks project compilation requirements

### 5. Build Process Tests
- Tests complete build process execution
- Validates single-file executable creation
- Checks executable properties and size
- Tests version information

### 6. DLL Extraction Tests
- Tests DLL extraction simulation script
- Validates extraction directory management
- Tests cleanup functionality
- Verifies extracted DLL structure

### 7. Deployment Script Tests
- Tests deployment script dry-run functionality
- Validates environment validation functions
- Tests parameter handling and error conditions

### 8. Clean Machine Simulation Tests
- Simulates behavior on machines without pre-existing setup
- Tests fallback mechanisms and default configurations
- Validates graceful error handling

## Quick Start

### Running Tests

1. **Quick Test Suite** (Recommended for regular testing):
   ```powershell
   .\run-tests.ps1 -TestSuite Quick
   ```

2. **Full Test Suite** (Complete validation):
   ```powershell
   .\run-tests.ps1 -TestSuite Full
   ```

3. **Build-Only Tests**:
   ```powershell
   .\run-tests.ps1 -TestSuite BuildOnly
   ```

4. **Environment-Only Tests**:
   ```powershell
   .\run-tests.ps1 -TestSuite EnvironmentOnly
   ```

### Direct Script Execution

Run the main testing script directly:

```powershell
# Basic execution
.\Test-BuildAndExtraction.ps1

# Skip build process (faster)
.\Test-BuildAndExtraction.ps1 -SkipBuildTest $true

# Verbose output
.\Test-BuildAndExtraction.ps1 -VerboseOutput $true

# Custom output directory
.\Test-BuildAndExtraction.ps1 -TestOutputDir "./my-test-results"
```

## Test Script Parameters

### Test-BuildAndExtraction.ps1 Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `TestOutputDir` | string | "./test-results" | Directory for test outputs and reports |
| `Configuration` | string | "Release" | Build configuration (Debug/Release) |
| `TestVersion` | string | "1.0.0-test" | Version for test builds |
| `CleanupAfterTest` | bool | true | Whether to clean up test resources |
| `SkipBuildTest` | bool | false | Skip the build process test |
| `VerboseOutput` | bool | false | Enable detailed output |

### run-tests.ps1 Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `TestSuite` | string | "Quick" | Test suite to run (Quick/Full/BuildOnly/EnvironmentOnly) |
| `VerboseOutput` | bool | false | Enable verbose output |
| `SkipCleanup` | bool | false | Skip cleanup after tests |

## Test Outputs

### Test Report

Each test run generates a detailed markdown report:
- **Location**: `{TestOutputDir}/test-report.md`
- **Content**: 
  - Test summary with pass/fail statistics
  - Detailed results for each test category
  - Recommendations for failed tests
  - Execution timestamps and duration

### Test Artifacts

- **Build outputs**: `{TestOutputDir}/build-test/`
- **DLL extraction tests**: `{TestOutputDir}/dll-test/`
- **Log files**: Various `.log` files in test output directory

## Expected Test Results

### Successful Test Run

A successful test run should show:
- ✓ Environment variable correctly configured
- ✓ Extraction directories created with proper permissions
- ✓ All required scripts present and functional
- ✓ .NET environment properly configured
- ✓ Build process completes without errors
- ✓ DLL extraction simulation works correctly
- ✓ Deployment scripts function properly
- ✓ Clean machine simulation passes

### Common Issues and Solutions

#### Environment Variable Not Set
**Symptoms**: Environment variable tests fail
**Solution**: Run environment setup scripts:
```powershell
.\scripts\SetSystemEnvironmentVariable.ps1
```

#### Build Process Fails
**Symptoms**: Build process tests fail
**Solution**: 
1. Check .NET installation: `dotnet --version`
2. Restore packages: `dotnet restore .\src\ChromeConnect`
3. Check for compilation errors

#### Permission Issues
**Symptoms**: Directory structure tests fail
**Solution**: 
1. Run PowerShell as Administrator
2. Check folder permissions for extraction directory
3. Verify UAC settings

#### Missing Scripts
**Symptoms**: Required scripts tests fail
**Solution**: 
1. Ensure all project files are present
2. Check git repository completeness
3. Verify script paths in test configuration

## Continuous Integration

### Automated Testing

For CI/CD pipelines, use the following approach:

```powershell
# In CI environment
.\run-tests.ps1 -TestSuite Full -VerboseOutput $true

# Check exit code
if ($LASTEXITCODE -eq 0) {
    Write-Host "All tests passed - ready for deployment"
} else {
    Write-Host "Tests failed - deployment blocked"
    exit 1
}
```

### Test Requirements

- Windows environment (Windows 10/11 or Windows Server)
- PowerShell 5.1 or PowerShell Core 7+
- .NET 8.0 SDK or runtime
- Administrator privileges (for some tests)
- Sufficient disk space for build outputs

## Test Configuration

### Customizing Tests

Modify `test-config.json` to customize:
- Expected extraction paths
- Required file locations
- Test timeouts
- Reporting options

### Environment Variables

The following environment variables affect testing:
- `DOTNET_BUNDLE_EXTRACT_BASE_DIR`: Primary extraction path
- `TEMP`: Temporary file location
- `PATH`: Must include .NET tools

## Troubleshooting

### Debug Mode

Enable debug mode for detailed troubleshooting:

```powershell
$DebugPreference = "Continue"
.\Test-BuildAndExtraction.ps1 -VerboseOutput $true
```

### Manual Verification

If automated tests fail, manually verify:

1. **Environment Setup**:
   ```powershell
   Get-ChildItem Env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
   ```

2. **Directory Permissions**:
   ```powershell
   Get-Acl "C:\Program Files (x86)\CyberArk\PSM\Components\ChromeConnect"
   ```

3. **Build Process**:
   ```powershell
   .\publish.ps1 -Version "test" -CreateZip $false
   ```

## Best Practices

1. **Regular Testing**: Run Quick tests before each commit
2. **Full Testing**: Run Full tests before releases
3. **Clean Environment**: Test on clean VMs periodically
4. **Document Changes**: Update tests when adding features
5. **Monitor Results**: Review test reports for trends

## Support

For issues with the testing procedures:
1. Check this documentation first
2. Review test output and reports
3. Check PowerShell execution policy
4. Verify all prerequisites are met
5. Run individual test functions for debugging

---

*This testing system ensures the reliability and robustness of the ChromeConnect build and deployment process.* 