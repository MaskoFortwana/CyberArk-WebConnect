# WebConnect DLL Extraction Environment Setup

## Overview

This documentation explains how to configure the environment variable `DOTNET_BUNDLE_EXTRACT_BASE_DIR` to resolve AppLocker DLL blocking issues with WebConnect application.

## Problem Background

WebConnect uses .NET single-file deployment which extracts DLLs to user temp folders during runtime:
- Default location: `%OSDRIVE%\USERS\USERNAME\APPDATA\LOCAL\TEMP\.NET\WEBCONNECT\`
- **Issue**: AppLocker blocks DLL execution from user temp folders in production environments
- **Solution**: Redirect DLL extraction to approved directory using environment variable

## Target Directory

**Approved extraction directory**: `C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\`

This directory is whitelisted in AppLocker policies and allows DLL execution.

## Setup Instructions

### Option 1: Session-Only Environment Variable

For temporary setup or development environments:

```powershell
# Run the provided script
PowerShell -ExecutionPolicy Bypass -File "scripts\SetEnvironmentVariable.ps1"
```

**What it does:**
- Sets `DOTNET_BUNDLE_EXTRACT_BASE_DIR` for current PowerShell session
- Creates the target directory if it doesn't exist
- Verifies environment variable is set correctly
- Tests directory permissions

### Option 2: System-Level Environment Variable (Recommended for Production)

For persistent setup across all sessions and reboots:

```powershell
# Run as Administrator
PowerShell -ExecutionPolicy Bypass -File "scripts\SetSystemEnvironmentVariable.ps1"
```

**Requirements:**
- **Must run as Administrator**
- Sets system-level environment variable
- Persists across reboots and user sessions

### Option 3: Application Startup Wrapper

Use the wrapper script to ensure environment variable is set before each application launch:

```powershell
# Launch WebConnect with environment setup
PowerShell -ExecutionPolicy Bypass -File "scripts\StartApplication.ps1" -ApplicationPath "C:\path\to\WebConnect.exe"

# Launch and wait for completion
PowerShell -ExecutionPolicy Bypass -File "scripts\StartApplication.ps1" -Wait

# Launch with arguments
PowerShell -ExecutionPolicy Bypass -File "scripts\StartApplication.ps1" -Arguments @("--param1", "value1")
```

## Verification Steps

### 1. Check Environment Variable

```powershell
# Check if variable is set in current session
echo $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR

# Check system-level variable
[System.Environment]::GetEnvironmentVariable('DOTNET_BUNDLE_EXTRACT_BASE_DIR', [System.EnvironmentVariableTarget]::Machine)
```

**Expected output:** `C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect`

### 2. Verify Directory Exists

```powershell
Test-Path "C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect"
```

**Expected output:** `True`

### 3. Test DLL Extraction

1. Launch WebConnect with environment variable set
2. Check for DLL extraction in target directory:
   ```powershell
   Get-ChildItem "C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect" -Recurse
   ```
3. Look for subdirectory with hash name (e.g., `BUVKQZGVGMYJUEVNC62UH0NUC1GYHEG=`)
4. Verify DLL files are extracted to this location

## Troubleshooting

### Error: "Access is denied" during directory creation

**Cause:** Insufficient permissions to create directory in Program Files
**Solution:** 
- Run script as Administrator
- Or manually create directory with proper permissions

### Error: "This script requires administrative privileges"

**Cause:** Trying to set system environment variable without admin rights
**Solution:**
- Run PowerShell as Administrator
- Or use session-only script: `SetEnvironmentVariable.ps1`

### Environment variable not persisting

**Cause:** Variable was set only for current session
**Solution:**
- Use `SetSystemEnvironmentVariable.ps1` as Administrator
- Or restart PowerShell session after setting system variable

### Application still extracting to user temp

**Possible causes:**
1. Environment variable not set correctly
2. Application cached previous extraction location
3. Variable not visible to application process

**Solutions:**
1. Verify environment variable: `echo $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR`
2. Clear existing temp extractions:
   ```powershell
   Remove-Item "$env:TEMP\.NET\WebConnect" -Recurse -Force -ErrorAction SilentlyContinue
   ```
3. Use startup wrapper script to ensure variable is set before launch

### DLL files not found in target directory

**Check:**
1. Environment variable is set correctly
2. Application was launched with variable in scope
3. Target directory exists and has write permissions
4. No previous extraction conflicts

## Production Deployment

### Recommended Setup Process

1. **Pre-deployment:**
   - Create target directory: `C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\`
   - Set appropriate permissions for CyberArk PSM service account

2. **Environment Configuration:**
   - Run `SetSystemEnvironmentVariable.ps1` as Administrator on target machines
   - Verify system environment variable is set

3. **Application Deployment:**
   - Deploy WebConnect.exe to target location
   - Include all PowerShell scripts for future maintenance
   - Test application launch with DLL extraction verification

4. **Validation:**
   - Launch application and verify DLL extraction location
   - Confirm AppLocker allows DLL execution from target directory
   - Test application functionality

## AppLocker Configuration

Ensure AppLocker rule allows DLL execution from extraction directory:

```xml
<FilePathRule Id="[GUID]" Name="WebConnect Components" Description="" UserOrGroupSid="S-1-1-0" Action="Allow">
  <FilePath Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\*" />
</FilePathRule>
```

## File Structure

After successful setup, you should have:

```
C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\
├── [HASH_DIRECTORY]\           # Hash-based subdirectory created by .NET runtime
│   ├── System.Private.CoreLib.dll
│   ├── WebConnect.dll
│   ├── Selenium.WebDriver.dll
│   └── ... (other extracted DLLs)
```

## Support

For issues with this setup:
1. Check PowerShell execution policy: `Get-ExecutionPolicy`
2. Verify administrative privileges when required
3. Check Windows Event Logs for AppLocker-related errors
4. Ensure CyberArk PSM service account has necessary permissions

## Notes

- Hash directory name changes with each application build/version
- Multiple application versions can coexist in separate hash directories
- Old hash directories can be cleaned up periodically to save disk space
- Environment variable affects ALL .NET single-file applications on the system 