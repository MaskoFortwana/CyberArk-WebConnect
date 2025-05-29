# ChromeConnect DLL Extraction Solution

## Overview

ChromeConnect includes a specialized DLL extraction solution designed to address AppLocker compatibility issues in enterprise environments. This feature pre-extracts embedded DLLs during the build process and packages them with the deployment, eliminating runtime extraction failures in restricted environments.

## Table of Contents

1. [Problem Statement](#problem-statement)
2. [Solution Overview](#solution-overview)
3. [Implementation Details](#implementation-details)
4. [Environment Configuration](#environment-configuration)
5. [Build Process Changes](#build-process-changes)
6. [Directory Structure](#directory-structure)
7. [Testing and Validation](#testing-and-validation)
8. [Deployment Instructions](#deployment-instructions)
9. [Troubleshooting](#troubleshooting)

## Problem Statement

### AppLocker DLL Blocking Issue

In enterprise environments with AppLocker enabled, .NET applications published as single-file executables encounter critical issues:

- **Runtime Extraction Failures**: Applications fail to extract embedded DLLs to temporary directories
- **Access Denied Errors**: AppLocker blocks DLL extraction to non-whitelisted locations
- **Application Startup Failures**: Missing dependencies cause crashes during initialization
- **Security Policy Violations**: .NET runtime attempts to write to restricted directories

### Business Impact

These issues prevent ChromeConnect from running in secure enterprise environments, particularly:

- **CyberArk PSM Environments**: Where applications run in isolated, restricted containers
- **Corporate Workstations**: With strict AppLocker policies
- **Zero-Trust Networks**: Where file system access is heavily controlled

## Solution Overview

ChromeConnect addresses these challenges through a **pre-extraction strategy**:

1. **Build-Time DLL Extraction**: Simulates the .NET runtime extraction process during build
2. **Controlled Extraction Path**: Uses `DOTNET_BUNDLE_EXTRACT_BASE_DIR` environment variable
3. **Package Integration**: Includes extracted DLLs in deployment packages
4. **Runtime Path Resolution**: Ensures .NET runtime finds pre-extracted dependencies

### Key Benefits

- ✅ **AppLocker Compatible**: No runtime extraction to restricted directories
- ✅ **Zero Configuration**: Works out-of-the-box in restricted environments
- ✅ **Backward Compatible**: Maintains functionality in unrestricted environments
- ✅ **Automated**: Fully integrated into existing build pipeline
- ✅ **Validated**: Comprehensive testing ensures reliability

## Implementation Details

### Environment Variable Configuration

The solution relies on the `DOTNET_BUNDLE_EXTRACT_BASE_DIR` environment variable:

```powershell
# Set extraction path
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "C:\temp\ChromeConnect\extracted"
```

**Important**: This variable must be set during the build process, not at runtime.

### Build Process Integration

The DLL extraction is seamlessly integrated into the build pipeline:

1. **Environment Setup**: Build script configures extraction path
2. **Application Build**: Standard .NET single-file publish
3. **Extraction Simulation**: Execute application to trigger DLL extraction
4. **Directory Discovery**: Locate extracted DLL directories
5. **Package Integration**: Copy extracted DLLs to deployment package
6. **Validation**: Verify extraction completeness and package integrity

## Environment Configuration

### Required Environment Variable

```powershell
# Temporary extraction path for build process
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "C:\temp\ChromeConnect\extracted"
```

### Recommended Paths by Environment

| Environment Type | Recommended Path | Rationale |
|------------------|------------------|-----------|
| **Development** | `C:\temp\ChromeConnect\extracted` | Easy access, debugging-friendly |
| **Build Server** | `D:\BuildTemp\ChromeConnect\extracted` | Isolated, high-performance storage |
| **Production Deployment** | Pre-extracted (no environment variable needed) | Packages include extracted DLLs |

### Path Selection Criteria

- **Write Permissions**: Build process requires write access
- **Available Space**: Minimum 100MB for extracted dependencies
- **Path Length**: Under 248 characters (Windows limitation)
- **Isolation**: Unique path to avoid conflicts with other applications

## Build Process Changes

### Updated Build Pipeline

The `publish.ps1` script now includes DLL extraction simulation:

```powershell
# 1. Environment Configuration
$extractionPath = "C:\temp\ChromeConnect\extracted"
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $extractionPath

# 2. Application Build
dotnet publish src/ChromeConnect -c Release -r win-x64 --self-contained true

# 3. DLL Extraction Simulation
& "$outputDir\ChromeConnect.exe" --version

# 4. Extract DLL Discovery and Packaging
# (Automatic detection and copying of extracted directories)
```

### Build Script Enhancements

- **Extraction Path Validation**: Ensures write permissions and adequate space
- **Automatic Directory Discovery**: Finds extracted DLL directories by hash pattern
- **Size and Count Reporting**: Provides detailed metrics on extracted dependencies
- **Error Handling**: Comprehensive error detection and reporting
- **Package Verification**: Validates deployment package completeness

### Build Output Metrics

The build process provides detailed extraction metrics:

```
==================== DLL EXTRACTION SIMULATION ====================
INFO: Target extraction path: C:\temp\ChromeConnect\extracted
INFO: Running DLL extraction simulation with: ./publish/ChromeConnect.exe
INFO: Extraction process completed in 2.34 seconds with exit code: 4
SUCCESS: Found extraction directory: 8e89e2c6a5d4... with 189 DLL files (85.68 MB)
SUCCESS: Successfully copied extracted DLLs to deployment package
```

## Directory Structure

### Build-Time Structure

During build, the extraction creates:

```
C:\temp\ChromeConnect\extracted\
└── 8e89e2c6a5d4a3f2e1c9b8a7d6e5f4c3\  # Hash-based directory name
    ├── Microsoft.Extensions.Configuration.dll
    ├── Microsoft.Extensions.DependencyInjection.dll
    ├── Microsoft.Extensions.Logging.dll
    ├── Selenium.WebDriver.dll
    ├── System.Text.Json.dll
    └── [additional 180+ DLL files]
```

### Deployment Package Structure

The final deployment package includes:

```
publish/
├── ChromeConnect.exe                    # Main executable
├── ExtractedDLLs/                      # Pre-extracted dependencies
│   └── 8e89e2c6a5d4a3f2e1c9b8a7d6e5f4c3\
│       ├── Microsoft.Extensions.*.dll
│       ├── Selenium.*.dll
│       ├── System.*.dll
│       └── [all dependencies]
└── README.md                           # Documentation
```

### Directory Naming Convention

- **Hash-Based Names**: .NET uses deterministic hashing for directory names
- **Content Dependent**: Hash changes only when dependencies change
- **Collision Resistant**: Extremely low probability of hash conflicts
- **Version Agnostic**: Same content produces same hash regardless of build environment

## Testing and Validation

### Automated Testing

The solution includes comprehensive testing via `Test-BuildAndExtraction.ps1`:

```powershell
# Run comprehensive build and extraction tests
.\Test-BuildAndExtraction.ps1
```

### Test Coverage

1. **Environment Validation**
   - Environment variable configuration
   - Path accessibility and permissions
   - Disk space requirements

2. **Build Process Testing**
   - Clean builds from scratch
   - Incremental builds
   - Multiple configuration testing (Debug/Release)

3. **Extraction Verification**
   - DLL extraction completion
   - File count and size validation
   - Directory structure verification

4. **Package Integrity**
   - Deployment package completeness
   - Extracted DLL inclusion
   - File integrity checks

5. **Runtime Validation**
   - Application startup in restricted environments
   - Dependency resolution verification
   - Functional testing scenarios

### Test Results Validation

```powershell
# Example test output
Test Results Summary:
✅ Environment Setup: PASSED
✅ Build Process: PASSED (285 files built successfully)
✅ DLL Extraction: PASSED (189 DLLs extracted, 85.68 MB)
✅ Package Creation: PASSED (ChromeConnect-1.0.4-win-x64.zip created)
✅ Integrity Verification: PASSED (All files verified)
```

## Deployment Instructions

### For Development Teams

1. **Use Standard Build Process**: Run `.\publish.ps1` as usual
2. **Verify Extraction**: Check build output for extraction confirmation
3. **Test Package**: Validate deployment package includes `ExtractedDLLs/` directory

### For System Administrators

1. **Deploy Package**: Extract the standard ZIP package
2. **No Additional Configuration**: Application works immediately
3. **Verify Installation**: Run `ChromeConnect.exe --version` to confirm operation

### For Enterprise Environments

1. **AppLocker Compatibility**: No changes to AppLocker policies required
2. **Path Restrictions**: Application doesn't require write access to temp directories
3. **Security Compliance**: All dependencies included in controlled deployment package

## Troubleshooting

### Build-Time Issues

#### Issue: "Failed to create extraction directory"

**Symptoms:**
```
ERROR: Failed to create extraction directory: Access to the path is denied
```

**Solutions:**
1. **Run as Administrator**: Ensure PowerShell has elevated permissions
2. **Change Extraction Path**: Use a writable directory
   ```powershell
   $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "D:\temp\ChromeConnect\extracted"
   ```
3. **Check Disk Space**: Ensure at least 200MB available space

#### Issue: "No extraction directories found"

**Symptoms:**
- Build completes without error
- No `ExtractedDLLs/` directory in output
- Missing extraction metrics in build log

**Investigation Steps:**
1. **Verify Environment Variable**: Ensure `DOTNET_BUNDLE_EXTRACT_BASE_DIR` is set
2. **Check Application Output**: Look for extraction during `--version` execution
3. **Examine Build Logs**: Review complete build output for extraction details

**Common Causes:**
- Application built without single-file publishing
- Environment variable not set during build
- Application doesn't require dependency extraction

### Runtime Issues

#### Issue: Application fails in AppLocker environment

**Symptoms:**
- Application starts but crashes during initialization
- Missing dependency errors
- AppLocker policy violation logs

**Verification Steps:**
1. **Check Package Contents**: Verify `ExtractedDLLs/` directory exists
2. **Validate File Count**: Ensure extracted DLLs are present
3. **Test in Unrestricted Environment**: Confirm package works without AppLocker

#### Issue: Large deployment package size

**Symptoms:**
- ZIP package significantly larger than expected
- Deployment takes longer than usual

**Expected Behavior:**
- Package includes ~85MB of extracted DLLs
- Total package size approximately 120-140MB
- This is normal and expected for the AppLocker-compatible deployment

### Performance Considerations

#### Build Time Impact

- **Additional Time**: ~10-30 seconds for extraction simulation
- **Disk I/O**: Temporary increase during DLL copying
- **Space Requirements**: Additional 100MB during build process

#### Runtime Performance

- **Startup Time**: No significant impact (dependencies pre-extracted)
- **Memory Usage**: Identical to standard deployment
- **File System**: Reduced I/O (no runtime extraction required)

## Technical Notes

### .NET Single-File Publishing

The solution leverages .NET's single-file publishing with specific optimizations:

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <PublishTrimmed>true</PublishTrimmed>
  <PublishReadyToRun>true</PublishReadyToRun>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

### Exit Code Interpretation

The extraction simulation process may return various exit codes:

| Exit Code | Meaning | Action Required |
|-----------|---------|-----------------|
| 0 | Success - version displayed normally | Normal operation |
| 4 | Configuration warning (expected) | Normal - extraction successful |
| -2147450720 | Common .NET config warning | Normal - extraction successful |
| Other | Unexpected error | Review logs, investigate issue |

### Hash Directory Algorithm

.NET uses a deterministic algorithm for extraction directory naming:
- **Input**: Assembly metadata, version, and content hash
- **Algorithm**: SHA-256 based hash generation
- **Output**: 32-character hexadecimal directory name
- **Consistency**: Same content always produces same hash

## Best Practices

### Development Workflow

1. **Always Test Build**: Run `.\publish.ps1` after code changes
2. **Verify Extraction**: Check for extraction confirmation in build output
3. **Test Deployment**: Validate packages in target environments
4. **Monitor Size**: Track deployment package size for significant changes

### Deployment Strategy

1. **Standard Deployment**: Use generated ZIP packages
2. **Version Management**: Include version in deployment path
3. **Rollback Planning**: Keep previous working packages for rollback
4. **Environment Testing**: Test in target environment before production deployment

### Maintenance

1. **Regular Updates**: Update dependencies to get latest security patches
2. **Size Monitoring**: Track extracted DLL size growth over time
3. **Performance Testing**: Verify startup performance after updates
4. **Documentation**: Keep deployment documentation updated with process changes

---

**Note**: This document reflects the current implementation as of the latest build. For build-specific details, refer to the build output logs and the `publish.ps1` script documentation. 