# WebConnect Configuration and Troubleshooting Guide

This comprehensive guide covers WebConnect configuration options, common issues, and troubleshooting procedures.

## 📋 Table of Contents

- [Configuration Overview](#configuration-overview)
- [Configuration Methods](#configuration-methods)
- [Configuration Reference](#configuration-reference)
- [Environment Setup](#environment-setup)
- [Common Issues](#common-issues)
- [Troubleshooting Procedures](#troubleshooting-procedures)
- [Diagnostic Tools](#diagnostic-tools)
- [Performance Optimization](#performance-optimization)
- [Security Configuration](#security-configuration)
- [Support and Resources](#support-and-resources)

---

## 🔧 Configuration Overview

WebConnect uses a hierarchical configuration system that prioritizes settings in the following order:

1. **Command-line arguments** (highest priority)
2. **Environment variables**
3. **appsettings.json file**
4. **Default values** (lowest priority)

This allows for flexible deployment scenarios while maintaining sensible defaults.

---

## ⚙️ Configuration Methods

### 1. Command-line Arguments
Primary method for runtime configuration:
```powershell
WebConnect.exe --USR john.doe --PSW password --URL https://site.com --DOM domain --INCOGNITO yes --KIOSK no --CERT ignore --debug
```

### 2. Environment Variables
For system-wide or deployment-specific settings:
```powershell
# PowerShell
$env:WEBCONNECT_LOG_LEVEL = "Debug"
$env:WEBCONNECT_TIMEOUT = "60"

# CMD
set WEBCONNECT_LOG_LEVEL=Debug
set WEBCONNECT_TIMEOUT=60
```

### 3. Configuration File (appsettings.json)
For persistent application settings:
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
    "ScreenshotOnError": true,
    "LoggingLevel": "Information"
  }
}
```

---

## 📖 Configuration Reference

### Core Application Settings

#### `WebConnect` Section
```json
{
  "WebConnect": {
    "DefaultTimeout": 30,              // Default operation timeout (seconds)
    "MaxRetryAttempts": 3,             // Maximum retry attempts for operations
    "ScreenshotOnError": true,         // Capture screenshots on errors
    "LoggingLevel": "Information",     // Application log level
    "ScreenshotDirectory": "./screenshots", // Screenshot output directory
    "LogDirectory": "./logs",          // Log file directory
    "BrowserOptions": {
      "DefaultWindowSize": "1280x1024",   // Browser window size
      "PageLoadTimeout": 30,              // Page load timeout (seconds)
      "ImplicitWaitTimeout": 10,          // Implicit wait timeout (seconds)
      "DriverDownloadTimeout": 120        // Driver download timeout (seconds)
    },
    "LoginDetection": {
      "MaxDetectionAttempts": 5,       // Maximum form detection attempts
      "DetectionTimeout": 15,          // Form detection timeout (seconds)
      "RetryDelay": 2000              // Delay between detection attempts (ms)
    },
    "Authentication": {
      "TypingDelay": 100,             // Delay between keystrokes (ms)
      "SubmissionTimeout": 30,        // Form submission timeout (seconds)
      "VerificationTimeout": 15       // Login verification timeout (seconds)
    }
  }
}
```

### Environment Variables

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `WEBCONNECT_LOG_LEVEL` | Override logging level | `Information` | `Debug` |
| `WEBCONNECT_TIMEOUT` | Default timeout (seconds) | `30` | `60` |
| `WEBCONNECT_SCREENSHOT_DIR` | Screenshot directory | `./screenshots` | `C:\Logs\Screenshots` |
| `WEBCONNECT_LOG_DIR` | Log file directory | `./logs` | `C:\Logs\WebConnect` |
| `WEBCONNECT_RETRY_ATTEMPTS` | Maximum retry attempts | `3` | `5` |
| `WEBCONNECT_TYPING_DELAY` | Typing delay (ms) | `100` | `200` |
| `CHROME_PATH` | Custom Chrome executable path | Auto-detect | `C:\Chrome\chrome.exe` |
| `CHROMEDRIVER_PATH` | Custom ChromeDriver path | Auto-download | `C:\Drivers\chromedriver.exe` |

---

## 🌍 Environment Setup

### Windows Environment Configuration

#### System Requirements Verification
```powershell
# Check Windows version
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion

# Check available memory
Get-WmiObject -Class Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum

# Check disk space
Get-WmiObject -Class Win32_LogicalDisk | Select-Object DeviceID, @{Name="Size(GB)";Expression={[math]::round($_.Size/1GB,2)}}, @{Name="FreeSpace(GB)";Expression={[math]::round($_.FreeSpace/1GB,2)}}
```

#### Chrome Installation Verification
```powershell
# Check if Chrome is installed
$chromePath = @(
    "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "${env:LOCALAPPDATA}\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($chromePath) {
    Write-Host "Chrome found at: $chromePath"
    & $chromePath --version
} else {
    Write-Host "Chrome not found. Please install Chrome."
}
```

### Directory Setup
```powershell
# Create necessary directories
New-Item -ItemType Directory -Force -Path @(
    ".\logs",
    ".\screenshots",
    ".\config"
)

# Set appropriate permissions (if needed)
icacls ".\logs" /grant "${env:USERNAME}:(OI)(CI)F"
icacls ".\screenshots" /grant "${env:USERNAME}:(OI)(CI)F"
```

---

## ❗ Common Issues

### 1. Browser Launch Issues

#### Issue: "Chrome failed to start"
**Symptoms:**
- Error message: "Browser launch failed"
- No Chrome window appears
- Process exits with code 2

**Causes & Solutions:**

| Cause | Solution |
|-------|----------|
| Chrome not installed | Install [Google Chrome](https://www.google.com/chrome/) |
| Insufficient permissions | Run as administrator |
| Antivirus blocking | Add WebConnect to exclusions |
| Corrupted Chrome installation | Reinstall Chrome |
| Missing dependencies | Install [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) |

**Diagnostic Commands:**
```powershell
# Test Chrome launch manually
& "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe" --version

# Check Chrome processes
Get-Process chrome -ErrorAction SilentlyContinue

# Test WebConnect browser launch
WebConnect.exe --USR test --PSW test --URL about:blank --DOM test --INCOGNITO yes --KIOSK no --CERT ignore
```

### 2. ChromeDriver Issues

#### Issue: "ChromeDriver version mismatch"
**Symptoms:**
- Error message: "This version of ChromeDriver only supports Chrome version X"
- Browser launches but fails to respond
- Selenium exceptions

**Solutions:**
```powershell
# Force ChromeDriver re-download
Remove-Item -Path ".\drivers\chromedriver.exe" -Force -ErrorAction SilentlyContinue

# Set custom ChromeDriver path
$env:CHROMEDRIVER_PATH = "C:\CustomDrivers\chromedriver.exe"

# Manual ChromeDriver download
Invoke-WebRequest -Uri "https://chromedriver.storage.googleapis.com/LATEST_RELEASE" -OutFile ".\temp_version.txt"
$version = Get-Content ".\temp_version.txt"
Invoke-WebRequest -Uri "https://chromedriver.storage.googleapis.com/$version/chromedriver_win32.zip" -OutFile ".\chromedriver.zip"
```

### 3. Login Detection Issues

#### Issue: "Login form not found"
**Symptoms:**
- Error message: "Unable to find login form elements"
- Browser opens but no interaction occurs
- Timeout waiting for elements

**Troubleshooting Steps:**
```powershell
# Enable debug logging
WebConnect.exe --USR test --PSW test --URL https://example.com --DOM test --INCOGNITO yes --KIOSK no --CERT ignore --debug

# Increase detection timeout
$env:WEBCONNECT_TIMEOUT = "60"

# Test manual navigation
WebConnect.exe --USR test --PSW test --URL https://example.com --DOM test --INCOGNITO no --KIOSK no --CERT ignore
# Then manually inspect the page
```

### 4. Authentication Issues

#### Issue: "Login verification failed"
**Symptoms:**
- Credentials entered successfully
- Form submitted but verification fails
- Appears to be logged in manually but WebConnect reports failure

**Solutions:**
```powershell
# Enable screenshots for debugging
WebConnect.exe --USR user --PSW pass --URL https://site.com --DOM domain --INCOGNITO yes --KIOSK no --CERT ignore

# Check screenshots directory
Get-ChildItem .\screenshots\*.png | Sort-Object LastWriteTime -Descending

# Increase verification timeout
$env:WEBCONNECT_TIMEOUT = "45"
```

### 5. Network and Certificate Issues

#### Issue: "Certificate validation failed"
**Symptoms:**
- SSL/TLS handshake errors
- "Certificate not trusted" messages
- Connection timeouts

**Solutions:**
```powershell
# Ignore certificate validation (development only)
WebConnect.exe --USR user --PSW pass --URL https://site.com --DOM domain --INCOGNITO yes --KIOSK no --CERT ignore

# Add certificate to Windows certificate store
# (Requires administrator privileges)
Import-Certificate -FilePath ".\certificate.crt" -CertStoreLocation Cert:\LocalMachine\Root

# Test connectivity
Test-NetConnection -ComputerName "example.com" -Port 443
```

---

## 🔍 Troubleshooting Procedures

### Systematic Diagnostics

#### Step 1: Basic Environment Check
```powershell
# Run environment diagnostics
$diagnostics = @{
    "OS" = (Get-ComputerInfo).WindowsProductName
    "PowerShell" = $PSVersionTable.PSVersion
    "Chrome" = if (Test-Path "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe") { "Installed" } else { "Not Found" }
    "DotNet" = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    "WorkingDirectory" = Get-Location
    "ExecutionPolicy" = Get-ExecutionPolicy
}

$diagnostics | ConvertTo-Json -Depth 2
```

#### Step 2: Configuration Validation
```powershell
# Validate configuration file
if (Test-Path ".\appsettings.json") {
    try {
        $config = Get-Content ".\appsettings.json" | ConvertFrom-Json
        Write-Host "✓ Configuration file is valid JSON"
        $config.WebConnect | ConvertTo-Json
    } catch {
        Write-Host "✗ Configuration file has syntax errors: $($_.Exception.Message)"
    }
} else {
    Write-Host "⚠ No appsettings.json found (using defaults)"
}
```

#### Step 3: Connectivity Test
```powershell
# Test network connectivity to target URL
param([string]$TargetUrl = "https://example.com")

$uri = [System.Uri]$TargetUrl
$connection = Test-NetConnection -ComputerName $uri.Host -Port $uri.Port -InformationLevel Detailed

@{
    "Host" = $uri.Host
    "Port" = $uri.Port
    "Connected" = $connection.TcpTestSucceeded
    "Latency" = $connection.PingReplyDetails.RoundtripTime
    "DNS_Resolution" = $connection.ResolvedAddresses
} | ConvertTo-Json
```

#### Step 4: Minimal Test Run
```powershell
# Perform minimal test to isolate issues
WebConnect.exe --USR "test" --PSW "test" --URL "about:blank" --DOM "test" --INCOGNITO yes --KIOSK no --CERT ignore --debug

# Check exit code
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Basic browser launch successful"
} else {
    Write-Host "✗ Basic browser launch failed with exit code: $LASTEXITCODE"
}
```

### Advanced Debugging

#### Enable Verbose Logging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "System": "Information",
      "WebConnect": "Trace"
    }
  }
}
```

#### Performance Profiling
```powershell
# Profile WebConnect execution
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$process = Start-Process -FilePath "WebConnect.exe" -ArgumentList @(
    "--USR", "user",
    "--PSW", "password",
    "--URL", "https://example.com",
    "--DOM", "domain",
    "--INCOGNITO", "yes",
    "--KIOSK", "no",
    "--CERT", "ignore"
) -Wait -PassThru -NoNewWindow

$stopwatch.Stop()

@{
    "ExecutionTime" = $stopwatch.Elapsed.ToString()
    "ExitCode" = $process.ExitCode
    "PeakMemoryMB" = [math]::Round($process.PeakWorkingSet64/1MB, 2)
    "ProcessorTime" = $process.TotalProcessorTime.ToString()
} | ConvertTo-Json
```

---

## 🛠 Diagnostic Tools

### Built-in Diagnostics

#### System Information Collection
```powershell
# Comprehensive system report
function Get-WebConnectDiagnostics {
    @{
        "Timestamp" = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        "System" = @{
            "OS" = (Get-ComputerInfo).WindowsProductName
            "Version" = (Get-ComputerInfo).WindowsVersion
            "Memory_GB" = [math]::Round((Get-WmiObject Win32_ComputerSystem).TotalPhysicalMemory/1GB, 2)
            "Processor" = (Get-WmiObject Win32_Processor).Name
            "Architecture" = (Get-WmiObject Win32_Processor).Architecture
        }
        "Software" = @{
            "PowerShell" = $PSVersionTable.PSVersion.ToString()
            "DotNet" = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
            "Chrome" = if ($chromePath = Get-ChildItem "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe" -ErrorAction SilentlyContinue) { 
                & $chromePath.FullName --version 
            } else { "Not Found" }
        }
        "Environment" = @{
            "WorkingDirectory" = Get-Location
            "TempDirectory" = $env:TEMP
            "UserProfile" = $env:USERPROFILE
            "ExecutionPolicy" = Get-ExecutionPolicy
        }
        "WebConnect" = @{
            "ConfigExists" = Test-Path ".\appsettings.json"
            "LogsDirectory" = if (Test-Path ".\logs") { (Get-ChildItem ".\logs").Count } else { "Not Found" }
            "ScreenshotsDirectory" = if (Test-Path ".\screenshots") { (Get-ChildItem ".\screenshots").Count } else { "Not Found" }
        }
    } | ConvertTo-Json -Depth 3
}

# Save diagnostics report
Get-WebConnectDiagnostics | Out-File "webconnect-diagnostics.json"
```

### External Tools

#### Performance Monitoring
```powershell
# Monitor WebConnect performance
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Run WebConnect with timing
$process = Start-Process -FilePath "WebConnect.exe" -ArgumentList @(
    "--USR", "test",
    "--PSW", "test", 
    "--URL", "https://example.com",
    "--DOM", "test",
    "--INCOGNITO", "yes",
    "--KIOSK", "no", 
    "--CERT", "ignore"
) -Wait -PassThru

$stopwatch.Stop()

Write-Host "Execution Time: $($stopwatch.Elapsed)"
Write-Host "Exit Code: $($process.ExitCode)"
Write-Host "Peak Memory: $([math]::Round($process.PeakWorkingSet64/1MB, 2)) MB"
```

---

## ⚡ Performance Optimization

### Configuration Tuning

#### High-Performance Configuration
```json
{
  "WebConnect": {
    "DefaultTimeout": 20,
    "MaxRetryAttempts": 2,
    "ScreenshotOnError": false,
    "BrowserOptions": {
      "PageLoadTimeout": 20,
      "ImplicitWaitTimeout": 5
    },
    "LoginDetection": {
      "MaxDetectionAttempts": 3,
      "DetectionTimeout": 10,
      "RetryDelay": 1000
    },
    "Authentication": {
      "TypingDelay": 50,
      "SubmissionTimeout": 15,
      "VerificationTimeout": 10
    }
  }
}
```

#### Memory Optimization
```json
{
  "WebConnect": {
    "ScreenshotOnError": false,
    "LoggingLevel": "Warning",
    "BrowserOptions": {
      "AdditionalOptions": [
        "--memory-pressure-off",
        "--disable-background-timer-throttling",
        "--disable-features=VizDisplayCompositor"
      ]
    }
  }
}
```

### System Optimization

#### Windows Performance Settings
```powershell
# Increase Chrome process priority
$chromeProcesses = Get-Process chrome -ErrorAction SilentlyContinue
$chromeProcesses | ForEach-Object { $_.PriorityClass = "High" }

# Clear temporary files
Remove-Item -Path "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue

# Optimize virtual memory
# Note: This requires administrator privileges
# [System.Environment]::SetEnvironmentVariable("CHROME_NO_SANDBOX", "1", "Machine")
```

---

## 🔒 Security Configuration

### Security Hardening

#### Secure Configuration Template
```json
{
  "WebConnect": {
    "ScreenshotOnError": false,
    "LoggingLevel": "Warning",
    "BrowserOptions": {
      "AdditionalOptions": [
        "--disable-extensions",
        "--disable-plugins",
        "--disable-javascript-harmony-shipping",
        "--disable-background-networking",
        "--disable-sync"
      ]
    }
  }
}
```

#### Environment Variable Security
```powershell
# Use secure environment variable storage
# Store sensitive configuration in User scope instead of Machine scope
[Environment]::SetEnvironmentVariable("WEBCONNECT_LOG_LEVEL", "Warning", "User")

# Clear sensitive variables after use
Remove-Item Env:TEMP_PASSWORD -ErrorAction SilentlyContinue
```

### Audit and Compliance

#### Log Security Settings
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "WebConnect.Services.LoginPerformer": "Warning"
    }
  }
}
```

#### Compliance Checklist
- [ ] Passwords are masked in all log outputs
- [ ] Screenshots are disabled or sanitized for production
- [ ] Log retention policy is configured
- [ ] Browser runs in incognito mode for sensitive operations
- [ ] Temporary files are automatically cleaned up
- [ ] Certificate validation is enabled for production

---

## 📞 Support and Resources

### Getting Help

#### Information to Gather Before Seeking Support

1. **System Information:**
   ```powershell
   # Generate system info report
   @{
       "OS" = (Get-ComputerInfo).WindowsProductName
       "Version" = (Get-ComputerInfo).WindowsVersion
       "Chrome" = & "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe" --version
       "Memory" = [math]::Round((Get-WmiObject Win32_ComputerSystem).TotalPhysicalMemory/1GB, 2)
       "DiskSpace" = (Get-WmiObject Win32_LogicalDisk | Where-Object DeviceID -eq "C:").FreeSpace / 1GB
   } | ConvertTo-Json
   ```

2. **WebConnect Configuration:**
   ```powershell
   # Gather configuration (remove sensitive data)
   WebConnect.exe --help > config-help.txt
   Get-Content .\appsettings.json > config-file.txt
   Get-ChildItem Env:WEBCONNECT_* > config-env.txt
   ```

3. **Error Information:**
   ```powershell
   # Gather recent logs and screenshots
   Get-ChildItem .\logs\*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 3
   Get-ChildItem .\screenshots\*.png | Sort-Object LastWriteTime -Descending | Select-Object -First 3
   ```

### Support Channels

- **Documentation**: [WebConnect Documentation](../README.md)
- **GitHub Issues**: [Report Issues](https://github.com/MaskoFortwana/webconnect/issues)
- **Community**: [GitHub Discussions](https://github.com/MaskoFortwana/webconnect/discussions)
- **Email Support**: support@webconnect.com

### Self-Help Resources

- **Command-line Reference**: [Command-line Guide](command-line-reference.md)
- **Architecture Documentation**: [Architecture Overview](architecture.md)
- **Usage Examples**: [Usage Examples](usage-examples.md)
- **FAQ**: [Frequently Asked Questions](faq.md)

---

*This troubleshooting guide is regularly updated. Last revision: November 2024* 