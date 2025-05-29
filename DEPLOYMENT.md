# WebConnect Deployment Guide

This guide provides instructions for building, deploying, and installing WebConnect as a self-contained Windows executable.

## Overview

WebConnect can be built as a self-contained Windows executable that includes all necessary dependencies, eliminating the need for users to install .NET 8.0 or any other dependencies on their systems.

## System Requirements

### For Running WebConnect
- **Operating System**: Windows 7 SP1 or later (Windows 10/11 recommended)
- **Architecture**: x64 (64-bit)
- **Memory**: Minimum 2 GB RAM (4 GB recommended)
- **Storage**: 100 MB free disk space
- **Browser**: Chrome will be automatically downloaded and managed by WebDriverManager

### For Building WebConnect
- **Operating System**: Windows, macOS, or Linux
- **.NET SDK**: .NET 8.0 or later
- **PowerShell**: For running the build scripts (included with Windows)

## Building the Executable

### Method 1: Using PowerShell Script (Recommended)

The repository includes a comprehensive PowerShell script for automated building:

```powershell
# Basic build (Release configuration)
./publish.ps1

# Specify version and configuration
./publish.ps1 -Version "1.0.1" -Configuration Release

# Build for 32-bit Windows
./publish.ps1 -Version "1.0.1" -Configuration Release -RuntimeIdentifier "win-x86"

# Debug build without ZIP package
./publish.ps1 -Version "1.0.0" -Configuration Debug -CreateZip $false

# Advanced options
./publish.ps1 -Version "1.2.0" -Configuration Release -RuntimeIdentifier "win-x64" -OutputDir "./custom-output" -Clean $true
```

#### PowerShell Script Parameters

| Parameter | Description | Default | Example |
|-----------|-------------|---------|---------|
| `Version` | Version number for the build | `1.0.0` | `"1.2.3"` |
| `Configuration` | Build configuration | `Release` | `Release`, `Debug` |
| `RuntimeIdentifier` | Target platform architecture | `win-x64` | `win-x64`, `win-x86` |
| `OutputDir` | Output directory for files | `./publish` | `"./my-build"` |
| `CreateZip` | Create ZIP package | `$true` | `$true`, `$false` |
| `Clean` | Clean output before build | `$true` | `$true`, `$false` |
| `UpdateVersion` | Update project version | `$true` | `$true`, `$false` |

### Method 2: Using Batch Script

For users who prefer Command Prompt:

```cmd
# Basic build
publish.bat

# With version and configuration
publish.bat 1.0.1 Release

# Debug build
publish.bat 1.0.0 Debug
```

### Method 3: Manual dotnet Commands

You can also build manually using .NET CLI:

```bash
# Release build with optimization
dotnet publish src/WebConnect -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish

# Debug build (no optimization for easier debugging)
dotnet publish src/WebConnect -c Debug -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish
```

## Build Output

After a successful build, you will find:

```
./publish/
├── WebConnect.exe            # Main executable (self-contained, ~45MB)
├── ExtractedDLLs/            # Pre-extracted dependencies (AppLocker compatible)
│   └── [hash-directory]/     # Contains all .NET runtime dependencies (~85MB)
│       ├── Microsoft.Extensions.*.dll
│       ├── Selenium.*.dll
│       ├── System.*.dll
│       └── [180+ additional DLL files]
└── README.md                 # Documentation

./WebConnect-1.0.0-win-x64.zip  # Distribution package (if created)
```

### DLL Extraction Solution

The build process now includes **automatic DLL extraction** to ensure compatibility with enterprise environments:

- **AppLocker Compatible**: Pre-extracted DLLs prevent runtime extraction failures
- **Enterprise Ready**: Works in CyberArk PSM and other restricted environments
- **Zero Configuration**: No additional setup required for deployment
- **Fully Automated**: Integrated into standard build pipeline

**Build Process Changes:**
1. **Environment Setup**: Configures `DOTNET_BUNDLE_EXTRACT_BASE_DIR` temporarily
2. **Application Build**: Standard .NET single-file publish
3. **DLL Extraction Simulation**: Runs application to trigger dependency extraction
4. **Package Integration**: Copies extracted DLLs to deployment package
5. **Verification**: Validates package completeness and integrity

**Expected Output:**
```
==================== DLL EXTRACTION SIMULATION ====================
INFO: Target extraction path: C:\temp\WebConnect\extracted
INFO: Running DLL extraction simulation with: ./publish/WebConnect.exe
INFO: Extraction process completed in 2.34 seconds with exit code: 4
SUCCESS: Found extraction directory: 8e89e2c6a5d4... with 189 DLL files (85.68 MB)
SUCCESS: Successfully copied extracted DLLs to deployment package
```

For detailed information about the DLL extraction solution, see [DLL_EXTRACTION_SOLUTION.md](DLL_EXTRACTION_SOLUTION.md).

**Note**: No configuration files are needed! All configuration is embedded in the executable. Logs are automatically written to `%TEMP%\WebConnect\`.

## Installation Instructions

### For End Users

1. **Download the ZIP package** (e.g., `WebConnect-1.0.0-win-x64.zip`)
2. **Extract the ZIP file** to a folder of your choice (e.g., `C:\WebConnect\`)
3. **Run the executable** directly - no installation required!

```cmd
# Navigate to the extracted folder
cd C:\WebConnect

# Run WebConnect
WebConnect.exe --help
```

### Adding to PATH (Optional)

To run WebConnect from anywhere in the command prompt:

1. **Copy the installation path** (e.g., `C:\WebConnect`)
2. **Open System Properties**:
   - Press `Win + X` and select "System"
   - Click "Advanced system settings"
   - Click "Environment Variables"
3. **Edit the PATH variable**:
   - Select "Path" under "User variables" or "System variables"
   - Click "Edit" → "New"
   - Add the installation path
   - Click "OK" to save
4. **Restart Command Prompt** to use the new PATH

Now you can run `WebConnect.exe` from any directory.

## Configuration

### Application Settings

The `appsettings.json` file contains configuration options:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "WebConnect": {
    "DefaultTimeout": 30,
    "MaxRetryAttempts": 3,
    "LoggingLevel": "Information"
  }
}
```

### Command Line Usage

```cmd
WebConnect.exe --USR username --PSW password --URL https://login.example.com --DOM company --INCOGNITO no --KIOSK no --CERT ignore
```

## Troubleshooting

### Common Issues

#### "Application failed to start"
- **Cause**: Missing Visual C++ Redistributable
- **Solution**: Install [Microsoft Visual C++ Redistributable](https://docs.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)

#### "Chrome driver not found"
- **Cause**: WebDriverManager unable to download Chrome driver
- **Solution**: Ensure internet connectivity and check firewall settings

#### "Permission denied" errors
- **Cause**: Insufficient permissions or antivirus blocking
- **Solution**: Run as administrator or add WebConnect to antivirus exclusions

#### Large executable size
- **Cause**: Self-contained deployment includes .NET runtime
- **Solution**: This is normal for self-contained apps. The Release build is optimized and trimmed.

### Logging

WebConnect creates detailed logs in the `logs/` directory:

```
logs/
├── webconnect-20241123.log      # Daily log files
├── webconnect-20241124.log
└── ...
```

Log levels can be adjusted in `appsettings.json` or via command line arguments.

## Deployment Options

### Enterprise Deployment

For enterprise environments:

1. **Network Share**: Place the executable on a network share accessible to all users
2. **Group Policy**: Deploy via Group Policy Software Installation
3. **SCCM/Intune**: Use Microsoft deployment tools for automated installation
4. **Docker**: Build a Windows container image (requires Windows containers)

#### AppLocker and Restricted Environments

WebConnect is designed to work seamlessly in enterprise environments with strict security policies:

**✅ AppLocker Compatible**
- **No Runtime DLL Extraction**: All dependencies are pre-extracted and included in the deployment package
- **No Temp Directory Access**: Application doesn't require write access to user or system temp directories
- **Pre-approved Paths**: Works with standard application installation paths approved by AppLocker policies

**🔐 CyberArk PSM Integration**
```powershell
# Recommended PSM deployment path
$PSMPath = "C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect"
Expand-Archive -Path "WebConnect-1.0.0-win-x64.zip" -DestinationPath $PSMPath -Force
```

**🏢 Corporate Workstation Deployment**
```powershell
# Standard corporate deployment
$CorporatePath = "C:\Program Files\WebConnect"
Expand-Archive -Path "WebConnect-1.0.0-win-x64.zip" -DestinationPath $CorporatePath -Force

# Verify AppLocker compatibility
& "$CorporatePath\WebConnect.exe" --version
```

**Key Benefits for Enterprise:**
- **Zero Configuration**: No environment variables or special setup required at runtime
- **Policy Compliant**: Respects existing AppLocker and security policies
- **Isolated Dependencies**: All dependencies contained within deployment package
- **Audit Friendly**: Predictable file structure and behavior for security audits

For detailed technical information about the AppLocker compatibility solution, see [DLL_EXTRACTION_SOLUTION.md](DLL_EXTRACTION_SOLUTION.md).

### Automated Deployment

Create a deployment script:

```powershell
# deploy.ps1
param(
    [string]$TargetPath = "C:\Program Files\WebConnect",
    [string]$SourceZip = "WebConnect-1.0.0-win-x64.zip"
)

# Extract to target location
Expand-Archive -Path $SourceZip -DestinationPath $TargetPath -Force

# Add to PATH
$currentPath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
if ($currentPath -notcontains $TargetPath) {
    [Environment]::SetEnvironmentVariable("PATH", "$currentPath;$TargetPath", "Machine")
}

Write-Host "WebConnect deployed successfully to $TargetPath"
```

## Security Considerations

### Antivirus False Positives

Self-contained executables may trigger antivirus warnings. To prevent this:

1. **Code Signing**: Sign the executable with a trusted certificate
2. **Whitelist**: Add WebConnect to antivirus exclusions
3. **Reputation**: Submit to antivirus vendors for reputation building

### Network Security

WebConnect requires:
- **Outbound HTTPS** access for WebDriver downloads
- **Access to target login URLs**
- **Chrome browser permissions** for automation

### Data Security

- Passwords are masked in logs
- No credentials are stored on disk
- All communication uses secure protocols
- Chrome runs in isolated mode

### AppLocker and Security Policies

With the new DLL extraction solution:
- **No Policy Violations**: Application doesn't attempt runtime DLL extraction
- **Predictable Behavior**: All file operations are within deployment directory
- **Audit Compliance**: File structure is consistent and auditable
- **Security Review Friendly**: Clear separation of executable and dependencies

## Performance Optimization

### Startup Time

- **ReadyToRun**: Enabled in Release builds for faster startup
- **Trimming**: Unused code is removed to reduce size
- **Compression**: Single-file compression reduces I/O
- **Pre-extracted DLLs**: No runtime extraction delays

### Memory Usage

- **Trimmed Runtime**: Only necessary .NET components included
- **Native Dependencies**: Included and optimized for single-file deployment
- **Chrome Management**: WebDriverManager handles Chrome lifecycle efficiently

### Package Size Considerations

With DLL extraction solution:
- **Larger Package Size**: ~120-140MB (includes 85MB of extracted DLLs)
- **Trade-off**: Size increase for enterprise compatibility
- **Network Transfer**: Consider compression for network deployments
- **Storage Planning**: Account for larger deployment packages

## Version Management

### Updating WebConnect

1. **Download new version** ZIP package
2. **Stop running instances** of WebConnect
3. **Replace entire directory** (includes new ExtractedDLLs)
4. **Test functionality** with a simple command

### Rollback Procedure

1. **Keep previous version** as backup (entire directory)
2. **Replace with backup directory** if issues occur
3. **Verify ExtractedDLLs integrity** after rollback

## Support and Maintenance

### Log Analysis

Monitor the `%TEMP%\WebConnect\` directory for:
- **Error patterns**: Recurring failures
- **Performance issues**: Slow operations
- **Authentication problems**: Login failures

### Health Checks

Create monitoring scripts:

```cmd
# Basic health check
WebConnect.exe --version
if %ERRORLEVEL% EQU 0 (
    echo WebConnect is healthy
) else (
    echo WebConnect health check failed
)
```

### Backup Strategy

Backup these critical components:
- **Entire deployment directory** (includes WebConnect.exe and ExtractedDLLs/)
- **Configuration files** (if any custom configurations)
- **Log files** (for troubleshooting historical issues)

### Package Integrity Verification

Verify deployment package integrity:

```powershell
# Verify main executable exists
if (Test-Path "WebConnect.exe") {
    Write-Host "✅ Main executable found"
} else {
    Write-Host "❌ Main executable missing"
}

# Verify ExtractedDLLs directory exists
if (Test-Path "ExtractedDLLs") {
    $dllCount = (Get-ChildItem -Path "ExtractedDLLs" -Recurse -Filter "*.dll").Count
    Write-Host "✅ ExtractedDLLs directory found with $dllCount DLL files"
} else {
    Write-Host "❌ ExtractedDLLs directory missing - AppLocker compatibility may be affected"
}
```

---

## Additional Resources

- **GitHub Repository**: [WebConnect Source Code](https://github.com/MaskoFortwana/webconnect)
- **Issue Tracker**: Report bugs and request features
- **Documentation**: Comprehensive API and usage documentation
- **Support**: Contact information for technical support
- **DLL Extraction Guide**: [DLL_EXTRACTION_SOLUTION.md](DLL_EXTRACTION_SOLUTION.md)

For detailed usage instructions, see the main [README.md](README.md) file.