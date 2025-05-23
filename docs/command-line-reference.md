# ChromeConnect Command-line Reference

This document provides comprehensive reference information for all ChromeConnect command-line options and usage patterns.

## 📋 Table of Contents

- [Overview](#overview)
- [Syntax](#syntax)
- [Required Parameters](#required-parameters)
- [Optional Parameters](#optional-parameters)
- [Environment Variables](#environment-variables)
- [Usage Patterns](#usage-patterns)
- [Exit Codes](#exit-codes)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

---

## 🔍 Overview

ChromeConnect is invoked from the command line with a set of parameters that control its behavior. All required parameters must be provided for the application to function correctly.

**Basic Syntax:**
```powershell
ChromeConnect.exe [OPTIONS] --USR username --PSW password --URL target_url --DOM domain --INCOGNITO yes|no --KIOSK yes|no --CERT ignore|enforce
```

---

## 📝 Syntax

### Parameter Format
ChromeConnect supports double-dash (`--`) parameter style:

```powershell
--PARAMETER_NAME value
```

### Value Requirements
- **String values**: No quotes required unless value contains spaces
- **Boolean values**: `yes`/`no` or `true`/`false`
- **URLs**: Full URL including protocol (e.g., `https://`)
- **Case sensitivity**: Parameter names are case-insensitive

---

## ✅ Required Parameters

All of the following parameters are **mandatory** and must be provided:

### `--USR` (Username)
- **Type**: String
- **Description**: Username for the login form
- **Example**: `--USR john.doe`
- **Notes**: 
  - Can include domain prefix (e.g., `DOMAIN\username`)
  - Email addresses are supported
  - Special characters are allowed

**Examples:**
```powershell
--USR employee123
--USR john.doe@company.com
--USR CORPORATE\john.doe
```

### `--PSW` (Password)
- **Type**: String (sensitive)
- **Description**: Password for authentication
- **Example**: `--PSW MySecurePassword123!`
- **Security**: 
  - Automatically masked in logs
  - Not displayed in process lists
  - Cleared from memory after use

**Examples:**
```powershell
--PSW SimplePassword
--PSW "Complex Password With Spaces"
--PSW MyP@ssw0rd!2024
```

### `--URL` (Target URL)
- **Type**: URL
- **Description**: Target login page URL
- **Example**: `--URL https://portal.company.com/login`
- **Requirements**:
  - Must include protocol (`http://` or `https://`)
  - Must be a valid URL format
  - Should point to the login page

**Examples:**
```powershell
--URL https://login.example.com
--URL https://portal.company.com/auth/login
--URL http://internal.server:8080/login
```

### `--DOM` (Domain)
- **Type**: String
- **Description**: Domain or tenant identifier
- **Example**: `--DOM CORPORATE`
- **Usage**:
  - Used for domain-based authentication
  - Can represent tenant names in multi-tenant systems
  - May be used as additional credential information

**Examples:**
```powershell
--DOM CORPORATE
--DOM tenant-name
--DOM dev-environment
```

### `--INCOGNITO` (Incognito Mode)
- **Type**: Boolean
- **Description**: Enable Chrome incognito mode
- **Values**: `yes`/`no` or `true`/`false`
- **Example**: `--INCOGNITO yes`
- **Behavior**:
  - `yes`: Launches Chrome with `--incognito` flag
  - `no`: Uses normal Chrome session

**Examples:**
```powershell
--INCOGNITO yes    # Recommended for security
--INCOGNITO no     # Use existing session/cookies
```

### `--KIOSK` (Kiosk Mode)
- **Type**: Boolean
- **Description**: Enable Chrome kiosk mode (fullscreen)
- **Values**: `yes`/`no` or `true`/`false`
- **Example**: `--KIOSK no`
- **Behavior**:
  - `yes`: Launches Chrome in fullscreen kiosk mode
  - `no`: Uses normal windowed mode

**Examples:**
```powershell
--KIOSK yes       # Fullscreen mode
--KIOSK no        # Windowed mode (recommended for debugging)
```

### `--CERT` (Certificate Handling)
- **Type**: Enum
- **Description**: Certificate validation policy
- **Values**: `ignore`/`enforce`
- **Example**: `--CERT ignore`
- **Behavior**:
  - `ignore`: Adds `--ignore-certificate-errors` flag
  - `enforce`: Uses default certificate validation

**Examples:**
```powershell
--CERT ignore     # For development/self-signed certificates
--CERT enforce    # For production environments
```

---

## ⚙️ Optional Parameters

These parameters modify ChromeConnect's behavior but are not required:

### `--debug`
- **Type**: Flag (no value)
- **Description**: Enable debug logging
- **Example**: `--debug`
- **Effect**: 
  - Increases log verbosity
  - Shows detailed execution steps
  - Useful for troubleshooting

### `--version`
- **Type**: Flag (no value)
- **Description**: Display version information and exit
- **Example**: `--version`
- **Output**: Version number and build information

### `--help`
- **Type**: Flag (no value)
- **Description**: Display help information and exit
- **Example**: `--help`
- **Output**: Usage information and parameter list

---

## 🌍 Environment Variables

ChromeConnect respects certain environment variables for configuration:

### `CHROMECONNECT_LOG_LEVEL`
- **Description**: Override default logging level
- **Values**: `Debug`, `Information`, `Warning`, `Error`
- **Default**: `Information`
- **Example**: `set CHROMECONNECT_LOG_LEVEL=Debug`

### `CHROMECONNECT_TIMEOUT`
- **Description**: Default operation timeout in seconds
- **Values**: Positive integer
- **Default**: `30`
- **Example**: `set CHROMECONNECT_TIMEOUT=60`

### `CHROMECONNECT_SCREENSHOT_DIR`
- **Description**: Custom directory for screenshots
- **Values**: Valid directory path
- **Default**: `./screenshots`
- **Example**: `set CHROMECONNECT_SCREENSHOT_DIR=C:\Logs\Screenshots`

---

## 🎯 Usage Patterns

### Standard Corporate Login
```powershell
ChromeConnect.exe --USR john.doe --PSW CompanyPass123 --URL https://portal.company.com --DOM CORPORATE --INCOGNITO yes --KIOSK no --CERT ignore
```

### Development Environment
```powershell
ChromeConnect.exe --USR testuser --PSW devpass --URL https://dev-portal.local --DOM DEV --INCOGNITO no --KIOSK no --CERT ignore --debug
```

### Secure Production
```powershell
ChromeConnect.exe --USR prod.user --PSW SecurePass --URL https://secure-portal.com --DOM PROD --INCOGNITO yes --KIOSK yes --CERT enforce
```

### Automated Testing
```powershell
ChromeConnect.exe --USR automation --PSW AutoPass --URL https://test.portal.com --DOM TEST --INCOGNITO yes --KIOSK no --CERT ignore --debug
```

---

## 🔄 Exit Codes

ChromeConnect returns specific exit codes to indicate execution results:

| Code | Status | Description | Common Causes |
|------|--------|-------------|---------------|
| `0` | Success | Login completed successfully | Valid credentials, successful authentication |
| `1` | Login Failed | Authentication failed | Invalid credentials, form not found, site issues |
| `2` | Application Error | Runtime error occurred | Browser issues, network problems, configuration errors |
| `3` | Parameter Error | Invalid parameters provided | Missing required parameters, invalid values |
| `4` | Timeout | Operation timed out | Slow network, unresponsive site |

### Exit Code Usage in Scripts

**Batch Script:**
```batch
@echo off
ChromeConnect.exe --USR user --PSW pass --URL https://site.com --DOM domain --INCOGNITO yes --KIOSK no --CERT ignore
if %ERRORLEVEL% EQU 0 (
    echo Login successful
) else (
    echo Login failed with code %ERRORLEVEL%
)
```

**PowerShell Script:**
```powershell
$result = & ChromeConnect.exe --USR user --PSW pass --URL https://site.com --DOM domain --INCOGNITO yes --KIOSK no --CERT ignore
switch ($LASTEXITCODE) {
    0 { Write-Host "Login successful" -ForegroundColor Green }
    1 { Write-Host "Login failed" -ForegroundColor Red }
    2 { Write-Host "Application error" -ForegroundColor Red }
    default { Write-Host "Unknown error: $LASTEXITCODE" -ForegroundColor Yellow }
}
```

---

## 📋 Examples

### Example 1: Basic Corporate Portal
```powershell
ChromeConnect.exe ^
  --USR john.doe ^
  --PSW MySecretPassword ^
  --URL https://portal.company.com/login ^
  --DOM COMPANY ^
  --INCOGNITO yes ^
  --KIOSK no ^
  --CERT ignore
```

### Example 2: Multi-line for Readability (PowerShell)
```powershell
ChromeConnect.exe `
  --USR "john.doe@company.com" `
  --PSW "Complex Password 123!" `
  --URL "https://sso.company.com/auth/login" `
  --DOM "CORPORATE" `
  --INCOGNITO yes `
  --KIOSK no `
  --CERT ignore `
  --debug
```

### Example 3: Batch File Integration
```batch
@echo off
set USERNAME=automation.user
set PASSWORD=AutomationPass123
set TARGET_URL=https://test-portal.example.com
set DOMAIN=TEST

ChromeConnect.exe --USR %USERNAME% --PSW %PASSWORD% --URL %TARGET_URL% --DOM %DOMAIN% --INCOGNITO yes --KIOSK no --CERT ignore

if %ERRORLEVEL% EQU 0 (
    echo Authentication successful - Continue with automation
    rem Add your automation commands here
) else (
    echo Authentication failed - Stopping automation
    exit /b %ERRORLEVEL%
)
```

### Example 4: PowerShell with Error Handling
```powershell
param(
    [Parameter(Mandatory)]
    [string]$Username,
    
    [Parameter(Mandatory)]
    [SecureString]$Password,
    
    [Parameter(Mandatory)]
    [string]$Url,
    
    [string]$Domain = "DEFAULT"
)

# Convert SecureString to plain text for ChromeConnect
$PlainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
)

try {
    Write-Host "Attempting login..." -ForegroundColor Yellow
    
    $process = Start-Process -FilePath "ChromeConnect.exe" -ArgumentList @(
        "--USR", $Username,
        "--PSW", $PlainPassword,
        "--URL", $Url,
        "--DOM", $Domain,
        "--INCOGNITO", "yes",
        "--KIOSK", "no",
        "--CERT", "ignore"
    ) -Wait -PassThru -NoNewWindow
    
    switch ($process.ExitCode) {
        0 { 
            Write-Host "✅ Login successful!" -ForegroundColor Green
            return $true
        }
        1 { 
            Write-Host "❌ Login failed - Check credentials" -ForegroundColor Red
            return $false
        }
        2 { 
            Write-Host "⚠️ Application error - Check logs" -ForegroundColor Yellow
            return $false
        }
        default { 
            Write-Host "❓ Unexpected exit code: $($process.ExitCode)" -ForegroundColor Magenta
            return $false
        }
    }
}
finally {
    # Clear password from memory
    if ($PlainPassword) {
        $PlainPassword = $null
        [System.GC]::Collect()
    }
}
```

---

## 🛠️ Troubleshooting

### Common Parameter Issues

#### Missing Required Parameters
**Error**: "Required parameter missing"
**Solution**: Ensure all required parameters (USR, PSW, URL, DOM, INCOGNITO, KIOSK, CERT) are provided

#### Invalid URL Format
**Error**: "Invalid URL format"
**Solution**: Ensure URL includes protocol (`https://` or `http://`)

#### Invalid Boolean Values
**Error**: "Invalid boolean value"
**Solution**: Use `yes`/`no` or `true`/`false` for INCOGNITO and KIOSK parameters

### Parameter Validation

To validate your parameters before execution, use:
```powershell
ChromeConnect.exe --help
```

This displays the current parameter format and requirements.

### Debug Mode
For detailed troubleshooting, always use the `--debug` flag:
```powershell
ChromeConnect.exe --debug --USR user --PSW pass --URL https://site.com --DOM domain --INCOGNITO yes --KIOSK no --CERT ignore
```

This provides detailed logging that can help identify configuration issues.

---

## 📞 Support

For additional help with command-line usage:

- **Help Command**: `ChromeConnect.exe --help`
- **Documentation**: [Full Documentation](../README.md)
- **Issues**: [GitHub Issues](https://github.com/yourorg/chromeconnect/issues)
- **Examples**: [Usage Examples](usage-examples.md)

---

*Last updated: November 2024* 