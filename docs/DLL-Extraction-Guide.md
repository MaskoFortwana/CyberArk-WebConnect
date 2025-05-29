# WebConnect DLL Extraction Guide

> **📋 Note**: This document provides technical details for the DLL extraction feature. For the complete solution overview including implementation details, testing procedures, and enterprise deployment guidance, see the main [DLL_EXTRACTION_SOLUTION.md](../DLL_EXTRACTION_SOLUTION.md) document.

## Overview

The WebConnect application includes a specialized DLL extraction feature designed to address AppLocker compatibility issues in enterprise environments. This feature simulates the .NET runtime's DLL extraction behavior, allowing verification that DLLs will extract to the intended directory before deployment.

## Table of Contents

1. [Problem Statement](#problem-statement)
2. [Solution Overview](#solution-overview)
3. [Environment Configuration](#environment-configuration)
4. [Usage Instructions](#usage-instructions)
5. [Understanding the Process](#understanding-the-process)
6. [Troubleshooting](#troubleshooting)
7. [Technical Details](#technical-details)
8. [Best Practices](#best-practices)

## Problem Statement

### AppLocker DLL Extraction Issues

In enterprise environments with AppLocker enabled, .NET applications published as single-file executables may encounter permission issues when attempting to extract embedded DLLs to temporary directories. This typically manifests as:

- **Access Denied Errors**: Applications fail to extract DLLs to user temp folders
- **Startup Failures**: Applications crash during initialization due to missing dependencies
- **Security Policy Violations**: AppLocker blocks DLL extraction to non-whitelisted locations

### Traditional Approaches and Limitations

- **Whitelisting temp directories**: Security risk and difficult to manage
- **Running with elevated permissions**: Not always feasible in enterprise environments
- **Disabling single-file publishing**: Increases deployment complexity

## Solution Overview

WebConnect addresses these issues by:

1. **Pre-extracting DLLs**: Simulating the extraction process during build time
2. **Controlled Extraction Path**: Using a configurable, AppLocker-friendly extraction directory
3. **Packaging Extracted DLLs**: Including extracted dependencies in deployment packages
4. **Runtime Redirection**: Configuring the .NET runtime to use the pre-approved extraction path

## Environment Configuration

### Required Environment Variable

The DLL extraction feature relies on the `DOTNET_BUNDLE_EXTRACT_BASE_DIR` environment variable:

```powershell
# Set the environment variable (adjust path as needed)
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "C:\temp\WebConnect\extracted"

# For persistent configuration (system-wide)
[Environment]::SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", "C:\temp\WebConnect\extracted", "Machine")

# For persistent configuration (user-specific)
[Environment]::SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", "C:\temp\WebConnect\extracted", "User")
```

### Choosing the Extraction Path

Consider these factors when selecting an extraction path:

- **Write Permissions**: Ensure the application has write access to the directory
- **AppLocker Compatibility**: Choose a path that's whitelisted in AppLocker policies
- **Disk Space**: Ensure sufficient space for extracted DLLs (typically 50-100 MB)
- **Path Length**: Keep path under 248 characters to avoid Windows limitations

### Recommended Paths

| Environment | Recommended Path | Notes |
|-------------|------------------|-------|
| Development | `C:\temp\WebConnect\extracted` | Easy to access and debug |
| Production (CyberArk PSM) | `C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect` | Standard PSM component location |
| Generic Enterprise | `C:\ProgramData\WebConnect\extracted` | Accessible to all users |
| User-specific | `%LOCALAPPDATA%\WebConnect\extracted` | Per-user isolation |

## Usage Instructions

### Basic Build with DLL Extraction

```powershell
# Basic build (uses default extraction simulation)
.\publish.ps1 -Version "1.0.0"

# Build with specific configuration
.\publish.ps1 -Version "1.1.0" -Configuration Release -RuntimeIdentifier win-x64
```

### Build Process Steps

The build script automatically performs the following steps:

1. **Environment Validation**: Checks for .NET SDK and required tools
2. **Version Management**: Updates project version numbers
3. **Dependency Restoration**: Downloads and caches NuGet packages
4. **Application Build**: Compiles the single-file executable
5. **DLL Extraction Simulation**: Runs the extraction process
6. **Package Creation**: Includes extracted DLLs in deployment package

### Monitoring the Process

The script provides detailed output during execution:

```
==================== DLL EXTRACTION SIMULATION ====================
INFO: Target extraction path: C:\temp\WebConnect\extracted
INFO: Running DLL extraction simulation with: ./publish/WebConnect.exe
INFO: Extraction process completed in 2.34 seconds with exit code: 4
SUCCESS: Found extraction directory: 8e89e2c6a5d4... with 189 DLL files (85.68 MB)
SUCCESS: Successfully copied extracted DLLs to deployment package
```

## Understanding the Process

### Extraction Simulation Process

1. **Path Validation**: Verifies extraction directory accessibility and permissions
2. **Disk Space Check**: Ensures sufficient space for extraction
3. **Directory Preparation**: Creates extraction directory if needed
4. **Process Execution**: Runs the application with `--version` flag to trigger extraction
5. **Directory Enumeration**: Discovers extracted DLL directories
6. **DLL Cataloging**: Counts and measures extracted dependencies
7. **Package Integration**: Copies extracted DLLs to deployment package

### Output Structure

After successful extraction, the deployment package includes:

```
publish/
├── WebConnect.exe                 # Main executable
├── ExtractedDLLs/                   # Extracted dependencies
│   └── 8e89e2c6a5d4.../            # Hash-based directory name
│       ├── Microsoft.Extensions.*.dll
│       ├── System.*.dll
│       └── [other dependencies]
└── [other deployment files]
```

### Exit Codes and Interpretation

| Exit Code | Meaning | Action Required |
|-----------|---------|-----------------|
| 0 | Success (version info displayed) | Normal - extraction successful |
| 4 | Configuration error (expected) | Normal - extraction still occurred |
| -2147450720 | Common .NET configuration warning | Normal - extraction still occurred |
| Other | Unexpected error | Review logs and troubleshoot |

## Troubleshooting

### Common Issues and Solutions

#### Issue: "Failed to create extraction directory"

**Symptoms:**
```
ERROR: Failed to create extraction directory: Access to the path 'C:\Program Files...' is denied.
```

**Solutions:**
1. **Change Extraction Path**: Use a writable directory
   ```powershell
   $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "C:\temp\WebConnect\extracted"
   ```

2. **Run with Elevated Permissions**: Run PowerShell as Administrator

3. **Use Alternative Path**: Choose a path with appropriate permissions
   ```powershell
   $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "$env:LOCALAPPDATA\WebConnect\extracted"
   ```

#### Issue: "No DLL extraction directories found"

**Symptoms:**
- Build completes without errors
- No extracted DLL directories in output
- Application works normally

**Possible Causes:**
1. **Application doesn't require extraction**: Some .NET applications embed all dependencies
2. **Different extraction path**: DLLs extracted to unexpected location
3. **Extraction disabled**: Application configuration prevents extraction

**Investigation Steps:**
1. Check if `DOTNET_BUNDLE_EXTRACT_BASE_DIR` is set correctly
2. Look for extraction directories in standard temp locations
3. Verify application is built as single-file executable

#### Issue: "Insufficient disk space"

**Symptoms:**
```
WARNING: Low disk space on extraction drive: 0.45 GB
```

**Solutions:**
1. **Free up disk space**: Remove unnecessary files from the target drive
2. **Change extraction path**: Use a drive with more available space
3. **Clean up previous extractions**: Remove old extraction directories

#### Issue: "Path too long"

**Symptoms:**
```
ERROR: Extraction path is too long (>248 characters): 267
```

**Solutions:**
1. **Shorten the path**: Use a shorter base directory
2. **Use path mapping**: Create a junction or symbolic link to a shorter path
3. **Enable long path support**: Configure Windows to support long paths (Windows 10+)

### Diagnostic Information

The script provides comprehensive diagnostic information:

#### Extraction Summary

```
==================== DLL EXTRACTION SUMMARY ====================
Status:             SUCCESS
Duration:           8.86 seconds
Extraction Path:    C:\temp\WebConnect\extracted
Directories Found:  1
Total DLLs Found:   189
Errors:             0
Warnings:           0
Extracted Directories:
  - 8e89e2c6a5d4b3f7a1e2c9d8f5a3b7e4: 189 DLLs (85.68 MB)
=================================================================
```

#### Build Summary

```
==================== BUILD SUMMARY ====================
Version:        1.0.3
Configuration:  Release
Runtime:        win-x64
Output Dir:     ./publish
Executable:     WebConnect.exe (127.45 MB)
Status:         SUCCESS
=======================================================
```

## Technical Details

### Script Implementation

The DLL extraction functionality is implemented in the `Simulate-DllExtraction` function within `publish.ps1`. Key features include:

#### Comprehensive Error Handling

- **Input Validation**: Path length, accessibility, and format validation
- **Permission Checking**: Write permission verification before extraction
- **Disk Space Monitoring**: Available space checking and warnings
- **Process Monitoring**: Exit code analysis and timing measurement
- **Copy Operation Verification**: Post-copy validation and cleanup on failure

#### Detailed Logging

- **Extraction Summary**: Comprehensive reporting with timing and statistics
- **Color-coded Output**: Visual status indicators (success, warning, error)
- **Progress Tracking**: Step-by-step process information
- **Error Details**: Specific error messages with context

#### Recovery Mechanisms

- **Graceful Degradation**: Continues operation when possible despite minor errors
- **Automatic Cleanup**: Removes partial files on operation failure
- **Alternative Path Detection**: Looks for extraction in standard temp locations

### Integration Points

#### Project File Configuration

The build process uses these key settings in the `.csproj` file:

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<PublishTrimmed>true</PublishTrimmed>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
```

#### Environment Variable Processing

The script processes the `DOTNET_BUNDLE_EXTRACT_BASE_DIR` environment variable with fallback logic:

1. **Primary**: Uses explicitly set environment variable
2. **Fallback**: Uses CyberArk PSM default path
3. **Warning**: Notifies when fallback is used

## Best Practices

### Development Environment

1. **Set Extraction Path Early**: Configure `DOTNET_BUNDLE_EXTRACT_BASE_DIR` in development
2. **Use Consistent Paths**: Maintain the same extraction path across team members
3. **Regular Cleanup**: Periodically clean extraction directories to save disk space
4. **Version Control**: Don't commit extraction directories to version control

### Production Deployment

1. **Pre-configure Environment**: Set environment variables before deployment
2. **Test Extraction Path**: Verify write permissions and AppLocker compatibility
3. **Monitor Disk Space**: Ensure adequate space for extraction
4. **Include Extracted DLLs**: Always include the `ExtractedDLLs` folder in deployments

### Security Considerations

1. **Path Whitelisting**: Ensure extraction paths are whitelisted in AppLocker
2. **Permission Validation**: Verify minimum required permissions
3. **Regular Audits**: Monitor extraction directories for security compliance
4. **Documentation**: Document extraction paths in security documentation

### Performance Optimization

1. **SSD Storage**: Use SSD storage for extraction directories when possible
2. **Network Locations**: Avoid network drives for extraction paths
3. **Antivirus Exclusions**: Consider excluding extraction paths from real-time scanning
4. **Cleanup Automation**: Implement automated cleanup of old extraction directories

## Support and Maintenance

### Log Files and Debugging

The script outputs comprehensive information to the console. For debugging:

1. **Capture Output**: Redirect script output to log files
   ```powershell
   .\publish.ps1 -Version "1.0.0" | Tee-Object -FilePath "build.log"
   ```

2. **Enable Verbose Logging**: Use PowerShell's built-in verbose preference
   ```powershell
   $VerbosePreference = "Continue"
   .\publish.ps1 -Version "1.0.0"
   ```

### Regular Maintenance

1. **Clean Extraction Directories**: Remove old extraction folders periodically
2. **Update Documentation**: Keep this guide current with script changes
3. **Test New Environments**: Validate extraction in new deployment environments
4. **Monitor Performance**: Track extraction times and optimize as needed

### Version Compatibility

This DLL extraction feature is compatible with:

- **.NET 6.0 and later**: Full single-file publishing support
- **Windows 10/11**: Native long path support
- **Windows Server 2016+**: Enterprise deployment compatibility
- **PowerShell 5.1+**: Script execution environment 