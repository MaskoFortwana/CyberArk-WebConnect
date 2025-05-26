# ChromeConnect Deployment Guide

This guide provides instructions for building, deploying, and installing ChromeConnect as a self-contained Windows executable.

## Overview

ChromeConnect can be built as a self-contained Windows executable that includes all necessary dependencies, eliminating the need for users to install .NET 8.0 or any other dependencies on their systems.

## System Requirements

### For Running ChromeConnect
- **Operating System**: Windows 7 SP1 or later (Windows 10/11 recommended)
- **Architecture**: x64 (64-bit)
- **Memory**: Minimum 2 GB RAM (4 GB recommended)
- **Storage**: 100 MB free disk space
- **Browser**: Chrome will be automatically downloaded and managed by WebDriverManager

### For Building ChromeConnect
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
dotnet publish src/ChromeConnect -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish

# Debug build (no optimization for easier debugging)
dotnet publish src/ChromeConnect -c Debug -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish
```

## Build Output

After a successful build, you will find:

```
./publish/
├── ChromeConnect.exe          # Main executable (self-contained, ~45MB)
└── README.md                 # Documentation

./ChromeConnect-1.0.0-win-x64.zip  # Distribution package (if created)
```

**Note**: No configuration files are needed! All configuration is embedded in the executable. Logs are automatically written to `%TEMP%\ChromeConnect\`.

## Installation Instructions

### For End Users

1. **Download the ZIP package** (e.g., `ChromeConnect-1.0.0-win-x64.zip`)
2. **Extract the ZIP file** to a folder of your choice (e.g., `C:\ChromeConnect\`)
3. **Run the executable** directly - no installation required!

```cmd
# Navigate to the extracted folder
cd C:\ChromeConnect

# Run ChromeConnect
ChromeConnect.exe --help
```

### Adding to PATH (Optional)

To run ChromeConnect from anywhere in the command prompt:

1. **Copy the installation path** (e.g., `C:\ChromeConnect`)
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

Now you can run `ChromeConnect.exe` from any directory.

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
  "ChromeConnect": {
    "DefaultTimeout": 30,
    "MaxRetryAttempts": 3,
    "LoggingLevel": "Information"
  }
}
```

### Command Line Usage

```cmd
ChromeConnect.exe --USR username --PSW password --URL https://login.example.com --DOM company --INCOGNITO no --KIOSK no --CERT ignore
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
- **Solution**: Run as administrator or add ChromeConnect to antivirus exclusions

#### Large executable size
- **Cause**: Self-contained deployment includes .NET runtime
- **Solution**: This is normal for self-contained apps. The Release build is optimized and trimmed.

### Logging

ChromeConnect creates detailed logs in the `logs/` directory:

```
logs/
├── chromeconnect-20241123.log    # Daily log files
├── chromeconnect-20241124.log
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

### Automated Deployment

Create a deployment script:

```powershell
# deploy.ps1
param(
    [string]$TargetPath = "C:\Program Files\ChromeConnect",
    [string]$SourceZip = "ChromeConnect-1.0.0-win-x64.zip"
)

# Extract to target location
Expand-Archive -Path $SourceZip -DestinationPath $TargetPath -Force

# Add to PATH
$currentPath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
if ($currentPath -notcontains $TargetPath) {
    [Environment]::SetEnvironmentVariable("PATH", "$currentPath;$TargetPath", "Machine")
}

Write-Host "ChromeConnect deployed successfully to $TargetPath"
```

## Security Considerations

### Antivirus False Positives

Self-contained executables may trigger antivirus warnings. To prevent this:

1. **Code Signing**: Sign the executable with a trusted certificate
2. **Whitelist**: Add ChromeConnect to antivirus exclusions
3. **Reputation**: Submit to antivirus vendors for reputation building

### Network Security

ChromeConnect requires:
- **Outbound HTTPS** access for WebDriver downloads
- **Access to target login URLs**
- **Chrome browser permissions** for automation

### Data Security

- Passwords are masked in logs
- No credentials are stored on disk
- All communication uses secure protocols
- Chrome runs in isolated mode

## Performance Optimization

### Startup Time

- **ReadyToRun**: Enabled in Release builds for faster startup
- **Trimming**: Unused code is removed to reduce size
- **Compression**: Single-file compression reduces I/O

### Memory Usage

- **Trimmed Runtime**: Only necessary .NET components included
- **Native Dependencies**: Included and optimized for single-file deployment
- **Chrome Management**: WebDriverManager handles Chrome lifecycle efficiently

## Version Management

### Updating ChromeConnect

1. **Download new version** ZIP package
2. **Stop running instances** of ChromeConnect
3. **Replace executable** and configuration files
4. **Test functionality** with a simple command

### Rollback Procedure

1. **Keep previous version** as backup
2. **Replace with backup executable** if issues occur
3. **Restore previous configuration** if needed

## Support and Maintenance

### Log Analysis

Monitor the `logs/` directory for:
- **Error patterns**: Recurring failures
- **Performance issues**: Slow operations
- **Authentication problems**: Login failures

### Health Checks

Create monitoring scripts:

```cmd
# Basic health check
ChromeConnect.exe --version
if %ERRORLEVEL% EQU 0 (
    echo ChromeConnect is healthy
) else (
    echo ChromeConnect health check failed
)
```

### Backup Strategy

Backup these critical files:
- `ChromeConnect.exe` (the main executable)
- `appsettings.json` (configuration)
- `logs/` directory (for troubleshooting)

---

## Additional Resources

- **GitHub Repository**: [ChromeConnect Source Code](https://github.com/your-org/chromeconnect)
- **Issue Tracker**: Report bugs and request features
- **Documentation**: Comprehensive API and usage documentation
- **Support**: Contact information for technical support

For detailed usage instructions, see the main [README.md](README.md) file. 