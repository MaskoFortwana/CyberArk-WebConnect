# Configuration and Troubleshooting Guide

This comprehensive guide covers ChromeConnect configuration options, common issues, and troubleshooting procedures.

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

ChromeConnect uses a hierarchical configuration system that prioritizes settings in the following order:

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
ChromeConnect.exe --USR john.doe --PSW password --URL https://site.com --DOM domain --INCOGNITO yes --KIOSK no --CERT ignore --debug
```

### 2. Environment Variables
For system-wide or deployment-specific settings:
```powershell
# PowerShell
$env:CHROMECONNECT_LOG_LEVEL = "Debug"
$env:CHROMECONNECT_TIMEOUT = "60"

# CMD
set CHROMECONNECT_LOG_LEVEL=Debug
set CHROMECONNECT_TIMEOUT=60
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
  "ChromeConnect": {
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

#### `ChromeConnect` Section
```json
{
  "ChromeConnect": {
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
| `CHROMECONNECT_LOG_LEVEL` | Override logging level | `Information` | `Debug` |
| `CHROMECONNECT_TIMEOUT` | Default timeout (seconds) | `30` | `60` |
| `CHROMECONNECT_SCREENSHOT_DIR` | Screenshot directory | `./screenshots` | `C:\Logs\Screenshots` |
| `CHROMECONNECT_LOG_DIR` | Log file directory | `./logs` | `C:\Logs\ChromeConnect` |
| `CHROMECONNECT_RETRY_ATTEMPTS` | Maximum retry attempts | `3` | `5` |
| `CHROMECONNECT_TYPING_DELAY` | Typing delay (ms) | `100` | `200` |
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
| Antivirus blocking | Add ChromeConnect to exclusions |
| Corrupted Chrome installation | Reinstall Chrome |
| Missing dependencies | Install [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) |

**Diagnostic Commands:**
```powershell
# Test Chrome launch manually
& "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe" --version

# Check Chrome processes
Get-Process chrome -ErrorAction SilentlyContinue

# Test with debug mode
ChromeConnect.exe --debug --USR test --PSW test --URL https://google.com --DOM test --INCOGNITO yes --KIOSK no --CERT ignore
```

### 2. ChromeDriver Issues

#### Issue: "ChromeDriver not found" or "Version mismatch"
**Symptoms:**
- Error about driver compatibility
- Browser launches but automation fails
- Version mismatch messages

**Solutions:**

1. **Automatic Download (Recommended):**
   ```powershell
   # ChromeConnect automatically downloads compatible drivers
   # Ensure internet connectivity
   Test-NetConnection -ComputerName www.google.com -Port 443
   ```

2. **Manual Driver Management:**
   ```powershell
   # Download ChromeDriver manually
   # 1. Check Chrome version
   & "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe" --version
   
   # 2. Download matching driver from https://chromedriver.chromium.org/
   # 3. Extract to PATH or set CHROMEDRIVER_PATH environment variable
   $env:CHROMEDRIVER_PATH = "C:\path\to\chromedriver.exe"
   ```

### 3. Login Form Detection Issues

#### Issue: "Login form not detected"
**Symptoms:**
- Error: "Could not find login form"
- Screenshot shows the page but no interaction
- Process exits with code 1

**Diagnostic Steps:**

1. **Verify URL:**
   ```powershell
   # Test URL accessibility
   Invoke-WebRequest -Uri "https://your-target-site.com" -Method Head
   ```

2. **Check Page Structure:**
   - Review screenshot in `./screenshots/` directory
   - Ensure you're pointing to the login page, not a landing page
   - Check if the site requires specific browser settings

3. **Enable Debug Mode:**
   ```powershell
   ChromeConnect.exe --debug --USR test --PSW test --URL https://your-site.com --DOM test --INCOGNITO no --KIOSK no --CERT ignore
   ```

4. **Site-Specific Issues:**

| Site Type | Common Issue | Solution |
|-----------|--------------|----------|
| SPA (Single Page App) | Dynamic form loading | Wait longer, use `--debug` to see timing |
| iframe-based login | Form in iframe | Check if popup handler is working |
| JavaScript-heavy | Form created dynamically | Verify JavaScript execution is enabled |
| Multi-step login | Multiple pages | May need manual handling |

### 4. Authentication Failures

#### Issue: "Login failed" with valid credentials
**Symptoms:**
- Credentials are correct but login fails
- Process exits with code 1
- No error messages visible

**Investigation Steps:**

1. **Check Screenshots:**
   ```powershell
   # Review latest screenshots
   Get-ChildItem -Path ".\screenshots" -Filter "*.png" | Sort-Object LastWriteTime -Descending | Select-Object -First 5
   ```

2. **Manual Verification:**
   - Try logging in manually with the same credentials
   - Check for CAPTCHAs or additional security measures
   - Verify domain field requirements

3. **Common Causes:**

| Cause | Solution |
|-------|----------|
| CAPTCHA required | Use session management to reduce CAPTCHAs |
| Two-factor authentication | Handle 2FA flow (may need customization) |
| Rate limiting | Add delays between attempts |
| Session requirements | Enable incognito mode or session management |
| Special characters | Ensure proper encoding/escaping |

### 5. Network and Connectivity Issues

#### Issue: Timeouts and network errors
**Symptoms:**
- "Navigation timeout" errors
- Slow performance
- Intermittent failures

**Solutions:**

1. **Increase Timeouts:**
   ```json
   {
     "ChromeConnect": {
       "DefaultTimeout": 60,
       "BrowserOptions": {
         "PageLoadTimeout": 60
       }
     }
   }
   ```

2. **Network Diagnostics:**
   ```powershell
   # Test connectivity
   Test-NetConnection -ComputerName target-domain.com -Port 443
   
   # Check DNS resolution
   Resolve-DnsName target-domain.com
   
   # Test with different DNS
   nslookup target-domain.com 8.8.8.8
   ```

3. **Proxy Configuration:**
   ```powershell
   # If behind corporate proxy, configure Chrome
   $env:CHROME_PROXY = "http://proxy.company.com:8080"
   ```

---

## 🔍 Troubleshooting Procedures

### Systematic Troubleshooting Approach

#### Step 1: Enable Debug Mode
```powershell
ChromeConnect.exe --debug --USR your-username --PSW your-password --URL https://your-site.com --DOM your-domain --INCOGNITO yes --KIOSK no --CERT ignore
```

#### Step 2: Review Logs
```powershell
# Check latest log file
Get-Content -Path ".\logs\chromeconnect-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50

# Search for errors
Select-String -Path ".\logs\*.log" -Pattern "ERROR|Exception|Failed" | Select-Object -Last 10
```

#### Step 3: Analyze Screenshots
```powershell
# Open latest screenshot
$latestScreenshot = Get-ChildItem -Path ".\screenshots" -Filter "*.png" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Start-Process $latestScreenshot.FullName
```

#### Step 4: Verify Configuration
```powershell
# Check effective configuration
ChromeConnect.exe --help

# Verify environment variables
Get-ChildItem Env:CHROMECONNECT_*
```

### Advanced Diagnostics

#### Process Monitoring
```powershell
# Monitor Chrome processes
Get-Process | Where-Object {$_.ProcessName -like "*chrome*"} | Format-Table ProcessName, Id, WorkingSet, StartTime

# Monitor ChromeConnect resource usage
$process = Get-Process ChromeConnect -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "Memory Usage: $([math]::Round($process.WorkingSet64/1MB, 2)) MB"
    Write-Host "CPU Time: $($process.TotalProcessorTime)"
}
```

#### Network Analysis
```powershell
# Monitor network connections
Get-NetTCPConnection | Where-Object {$_.OwningProcess -eq (Get-Process chrome).Id}

# Check for blocked connections
Get-WinEvent -FilterHashtable @{LogName='System'; ID=4625} -MaxEvents 10
```

---

## 🔧 Diagnostic Tools

### Built-in Diagnostics

#### Health Check Script
```powershell
# ChromeConnect-HealthCheck.ps1
Write-Host "=== ChromeConnect Health Check ===" -ForegroundColor Green

# Check Chrome installation
$chromePath = Get-Command chrome -ErrorAction SilentlyContinue
if ($chromePath) {
    Write-Host "✅ Chrome: Found at $($chromePath.Source)"
} else {
    Write-Host "❌ Chrome: Not found in PATH"
}

# Check directories
@("logs", "screenshots") | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "✅ Directory '$_': Exists"
    } else {
        Write-Host "⚠️ Directory '$_': Missing (will be created)"
    }
}

# Check network connectivity
try {
    $response = Invoke-WebRequest -Uri "https://www.google.com" -TimeoutSec 10 -UseBasicParsing
    Write-Host "✅ Internet: Connected (Status: $($response.StatusCode))"
} catch {
    Write-Host "❌ Internet: Connection failed"
}

# Check permissions
try {
    $testFile = ".\permission-test.tmp"
    "test" | Out-File -FilePath $testFile
    Remove-Item $testFile
    Write-Host "✅ Permissions: Write access confirmed"
} catch {
    Write-Host "❌ Permissions: No write access"
}

Write-Host "=== Health Check Complete ===" -ForegroundColor Green
```

#### Configuration Validator
```powershell
# Validate-ChromeConnectConfig.ps1
param(
    [string]$ConfigPath = ".\appsettings.json"
)

if (Test-Path $ConfigPath) {
    try {
        $config = Get-Content $ConfigPath | ConvertFrom-Json
        Write-Host "✅ Configuration: Valid JSON" -ForegroundColor Green
        
        # Validate required sections
        if ($config.ChromeConnect) {
            Write-Host "✅ ChromeConnect section: Found" -ForegroundColor Green
        } else {
            Write-Host "⚠️ ChromeConnect section: Missing" -ForegroundColor Yellow
        }
        
        # Validate timeout values
        if ($config.ChromeConnect.DefaultTimeout -gt 0) {
            Write-Host "✅ Timeout configuration: Valid" -ForegroundColor Green
        } else {
            Write-Host "⚠️ Timeout configuration: Invalid or missing" -ForegroundColor Yellow
        }
        
    } catch {
        Write-Host "❌ Configuration: Invalid JSON - $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "⚠️ Configuration: File not found at $ConfigPath" -ForegroundColor Yellow
}
```

### External Tools

#### Performance Monitoring
```powershell
# Monitor ChromeConnect performance
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Run ChromeConnect with timing
$process = Start-Process -FilePath "ChromeConnect.exe" -ArgumentList @(
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
  "ChromeConnect": {
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
  "ChromeConnect": {
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
  "ChromeConnect": {
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
[Environment]::SetEnvironmentVariable("CHROMECONNECT_LOG_LEVEL", "Warning", "User")

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
      "ChromeConnect.Services.LoginPerformer": "Warning"
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

2. **ChromeConnect Configuration:**
   ```powershell
   # Gather configuration (remove sensitive data)
   ChromeConnect.exe --help > config-help.txt
   Get-Content .\appsettings.json > config-file.txt
   Get-ChildItem Env:CHROMECONNECT_* > config-env.txt
   ```

3. **Error Information:**
   ```powershell
   # Gather recent logs and screenshots
   Get-ChildItem .\logs\*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 3
   Get-ChildItem .\screenshots\*.png | Sort-Object LastWriteTime -Descending | Select-Object -First 3
   ```

### Support Channels

- **Documentation**: [ChromeConnect Documentation](../README.md)
- **GitHub Issues**: [Report Issues](https://github.com/yourorg/chromeconnect/issues)
- **Community**: [GitHub Discussions](https://github.com/yourorg/chromeconnect/discussions)
- **Email Support**: support@chromeconnect.com

### Self-Help Resources

- **Command-line Reference**: [Command-line Guide](command-line-reference.md)
- **Architecture Documentation**: [Architecture Overview](architecture.md)
- **Usage Examples**: [Usage Examples](usage-examples.md)
- **FAQ**: [Frequently Asked Questions](faq.md)

---

*This troubleshooting guide is regularly updated. Last revision: November 2024* 