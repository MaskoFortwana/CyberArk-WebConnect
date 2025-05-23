# ChromeConnect Error Codes and Messages Reference

This document provides a comprehensive reference for all error codes, messages, and their resolutions in ChromeConnect. It covers exit codes, exception error codes, log messages, and troubleshooting guidance organized by category and severity.

## 📋 Table of Contents

- [Exit Codes](#exit-codes)
- [Exception Error Codes](#exception-error-codes)
- [Browser-Related Errors](#browser-related-errors)
- [Login-Related Errors](#login-related-errors)
- [Network-Related Errors](#network-related-errors)
- [System-Related Errors](#system-related-errors)
- [Configuration Errors](#configuration-errors)
- [Service-Specific Errors](#service-specific-errors)
- [Warning Messages](#warning-messages)
- [Troubleshooting Guide](#troubleshooting-guide)
- [Error Severity Levels](#error-severity-levels)

---

## 🚀 Exit Codes

ChromeConnect uses standardized exit codes to indicate the overall result of the authentication process:

### Standard Exit Codes

| Exit Code | Status | Description | Resolution |
|-----------|--------|-------------|------------|
| **0** | Success | Authentication completed successfully | No action required |
| **1** | Login Failure | Authentication failed due to credentials or login form issues | Check credentials, verify login form detection |
| **2** | System Error | General application or system error | Check logs for specific exception details |
| **3** | Configuration Error | Invalid or missing configuration parameters | Review command-line arguments and configuration files |

### Exit Code Details

#### Exit Code 0: Success
```powershell
# Example successful execution
ChromeConnect.exe --USR user@domain.com --PSW password123 --URL https://portal.com --DOM CORPORATE --INCOGNITO yes --KIOSK no --CERT ignore
# Exit Code: 0
```
**Meaning**: Authentication completed successfully and the user is logged in.

#### Exit Code 1: Login Failure
```powershell
# Example failed execution
ChromeConnect.exe --USR wronguser@domain.com --PSW wrongpass --URL https://portal.com --DOM CORPORATE --INCOGNITO yes --KIOSK no --CERT ignore
# Exit Code: 1
```
**Common Causes**:
- Invalid credentials
- Login form not detected
- Form elements changed
- Two-factor authentication required
- Account locked or disabled

**Resolution Steps**:
1. Verify credentials are correct
2. Check if login form structure has changed
3. Enable debug mode for detailed logging
4. Review screenshot for visual debugging

#### Exit Code 2: System Error
**Common Causes**:
- Browser launch failure
- ChromeDriver not found
- Network connectivity issues
- Unexpected system exceptions

**Resolution Steps**:
1. Check browser installation
2. Verify ChromeDriver installation
3. Review system logs
4. Check network connectivity

#### Exit Code 3: Configuration Error
**Common Causes**:
- Missing required parameters
- Invalid URL format
- Malformed configuration values

**Resolution Steps**:
1. Review command-line syntax
2. Validate all required parameters
3. Check URL format and accessibility

---

## 🔧 Exception Error Codes

ChromeConnect uses structured error codes in exceptions for detailed error tracking and handling:

### Browser Exception Codes

| Code | Exception | Description | Resolution |
|------|-----------|-------------|------------|
| **BROWSER_INIT_001** | BrowserInitializationException | Browser failed to initialize | Install/update Chrome, check ChromeDriver |
| **CHROME_DRIVER_001** | ChromeDriverMissingException | ChromeDriver executable not found | Install ChromeDriver or check PATH |
| **BROWSER_NAV_001** | BrowserNavigationException | Navigation to URL failed | Check URL validity and network connection |
| **BROWSER_TIMEOUT_001** | BrowserTimeoutException | Browser operation timed out | Increase timeout settings or check performance |
| **ELEMENT_NOT_FOUND_001** | ElementNotFoundException | Required page element not found | Check page structure and selectors |

### Login Exception Codes

| Code | Exception | Description | Resolution |
|------|-----------|-------------|------------|
| **LOGIN_FORM_001** | LoginFormNotFoundException | Login form not detected on page | Verify page URL and form structure |
| **CREDENTIAL_ENTRY_001** | CredentialEntryException | Failed to enter credentials | Check form fields and element detection |
| **LOGIN_VERIFY_001** | LoginVerificationException | Login result verification failed | Review success/failure indicators |
| **INVALID_CREDS_001** | InvalidCredentialsException | Authentication rejected by server | Verify credentials and account status |

### Network Exception Codes

| Code | Exception | Description | Resolution |
|------|-----------|-------------|------------|
| **CONNECTION_FAILED_001** | ConnectionFailedException | Cannot establish network connection | Check internet connection and URL |
| **CERTIFICATE_001** | CertificateException | SSL certificate validation failed | Use --CERT ignore or fix certificate |
| **REQUEST_TIMEOUT_001** | RequestTimeoutException | HTTP request timed out | Check network speed and timeout settings |

### System Exception Codes

| Code | Exception | Description | Resolution |
|------|-----------|-------------|------------|
| **CONFIG_001** | ConfigurationException | Configuration parameter error | Review parameter values and format |
| **FILE_OP_001** | FileOperationException | File operation failed | Check file permissions and paths |
| **RESOURCE_NOT_FOUND_001** | ResourceNotFoundException | Required resource not available | Ensure all dependencies are installed |
| **OPERATION_CANCELED_001** | AppOperationCanceledException | Operation was canceled | Check for user interruption or timeouts |

---

## 🌐 Browser-Related Errors

### Browser Initialization Errors

#### BROWSER_INIT_001: Browser Initialization Failed
```
Error Message: "Browser initialization failed. Ensure Google Chrome is installed and up-to-date."
Exception: BrowserInitializationException
Log Level: Error
```

**Common Causes**:
- Google Chrome not installed
- Chrome version incompatible with ChromeDriver
- Insufficient system resources
- Conflicting browser processes

**Resolution Steps**:
1. **Install Chrome**: Download and install latest Google Chrome
2. **Update ChromeDriver**: Ensure ChromeDriver matches Chrome version
3. **Check Resources**: Ensure sufficient RAM and CPU
4. **Kill Processes**: Terminate existing Chrome processes
5. **Permissions**: Run with appropriate permissions

**PowerShell Diagnostic**:
```powershell
# Check Chrome installation
Get-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object {$_.DisplayName -like "*Google Chrome*"}

# Check running Chrome processes
Get-Process chrome -ErrorAction SilentlyContinue

# Check ChromeDriver
where.exe chromedriver
```

#### CHROME_DRIVER_001: ChromeDriver Missing
```
Error Message: "ChromeDriver executable not found at specified path."
Exception: ChromeDriverMissingException
Context: Driver Path: {specified_path}
```

**Resolution Steps**:
1. **Download ChromeDriver**: Get from https://chromedriver.chromium.org/
2. **Add to PATH**: Place ChromeDriver in system PATH
3. **Specify Path**: Use absolute path in configuration
4. **Version Match**: Ensure ChromeDriver version matches Chrome

### Browser Navigation Errors

#### BROWSER_NAV_001: Navigation Failed
```
Error Message: "Browser navigation failed. Please check the URL and internet connection."
Exception: BrowserNavigationException
Context: Target URL: {url}
```

**Common Causes**:
- Invalid URL format
- Network connectivity issues
- DNS resolution failures
- Firewall blocking access

**Resolution Steps**:
1. **Validate URL**: Check URL format and accessibility
2. **Test Connectivity**: Ping target domain
3. **Check DNS**: Verify DNS resolution
4. **Firewall**: Review firewall settings

---

## 🔐 Login-Related Errors

### Form Detection Errors

#### LOGIN_FORM_001: Login Form Not Found
```
Error Message: "Login form not found. The website structure may have changed."
Exception: LoginFormNotFoundException
Context: Page URL: {url}
```

**Common Causes**:
- Website redesign changed form structure
- JavaScript-rendered forms not loaded
- Form inside iframe or shadow DOM
- Wrong page URL

**Resolution Steps**:
1. **Visual Inspection**: Check page manually in browser
2. **Debug Mode**: Enable debug logging to see detection attempts
3. **Wait Time**: Increase wait time for dynamic content
4. **Custom Configuration**: Add URL-specific selectors
5. **Screenshot Review**: Check captured screenshots

**Debug Commands**:
```powershell
# Enable debug mode for detailed logging
ChromeConnect.exe --debug --USR user --PSW pass --URL "https://example.com/login" --DOM CORP --INCOGNITO yes --KIOSK no --CERT ignore
```

#### CREDENTIAL_ENTRY_001: Credential Entry Failed
```
Error Message: "Failed to enter credentials in {FieldType} field. The form structure may have changed."
Exception: CredentialEntryException
Context: Field Type: {username|password|domain}
```

**Resolution Steps**:
1. **Check Field Selectors**: Verify form field identification
2. **Element State**: Ensure fields are visible and enabled
3. **Timing**: Add delays for dynamic form loading
4. **Alternative Selectors**: Configure backup selectors

### Authentication Errors

#### INVALID_CREDS_001: Invalid Credentials
```
Error Message: "Login failed due to invalid credentials."
Exception: InvalidCredentialsException
Context: Error Messages: {error_messages_from_page}
```

**Common Causes**:
- Incorrect username or password
- Account locked or disabled
- Domain authentication required
- Two-factor authentication enabled

**Resolution Steps**:
1. **Verify Credentials**: Double-check username and password
2. **Account Status**: Check if account is active
3. **Domain Requirements**: Ensure domain is correctly specified
4. **MFA**: Check if multi-factor authentication is required

---

## 🌐 Network-Related Errors

### Connection Errors

#### CONNECTION_FAILED_001: Connection Failed
```
Error Message: "Failed to connect to {URL}. Please check your internet connection."
Exception: ConnectionFailedException
Context: Target URL: {url}
```

**Resolution Steps**:
1. **Internet Connection**: Verify network connectivity
2. **URL Accessibility**: Test URL in regular browser
3. **Proxy Settings**: Check corporate proxy configuration
4. **DNS Resolution**: Verify domain name resolution

### Certificate Errors

#### CERTIFICATE_001: Certificate Validation Failed
```
Error Message: "Certificate validation failed for {URL}."
Exception: CertificateException
Context: Target URL: {url}, Certificate Subject: {subject}, Validation Errors: {errors}
```

**Resolution Steps**:
1. **Ignore Certificates**: Use `--CERT ignore` parameter
2. **Update Certificates**: Install latest root certificates
3. **Corporate Certificates**: Install corporate CA certificates
4. **Time Sync**: Ensure system time is correct

### Timeout Errors

#### REQUEST_TIMEOUT_001: Request Timeout
```
Error Message: "Request to {URL} timed out after {timeout}ms"
Exception: RequestTimeoutException
Context: Request URL: {url}, Timeout: {milliseconds}ms
```

**Resolution Steps**:
1. **Increase Timeout**: Adjust timeout settings in configuration
2. **Network Speed**: Check internet connection speed
3. **Server Performance**: Verify target server responsiveness
4. **Retry Logic**: Enable automatic retry mechanisms

---

## ⚙️ System-Related Errors

### Configuration Errors

#### CONFIG_001: Configuration Error
```
Error Message: "Configuration error with parameter: {parameter}"
Exception: ConfigurationException
Context: Parameter: {parameter_name}
```

**Common Parameters**:
- Missing required command-line arguments
- Invalid URL format
- Malformed timeout values
- Invalid boolean values for flags

**Resolution Steps**:
1. **Review Syntax**: Check command-line argument format
2. **Required Parameters**: Ensure all required parameters are provided
3. **Value Validation**: Verify parameter values are in correct format
4. **Documentation**: Reference command-line documentation

### File Operation Errors

#### FILE_OP_001: File Operation Failed
```
Error Message: "File operation '{operation}' failed on {path}"
Exception: FileOperationException
Context: File: {file_path}, Operation: {operation_type}
```

**Common Operations**:
- Screenshot capture
- Log file creation
- Configuration file access
- Driver executable access

**Resolution Steps**:
1. **Permissions**: Check file and directory permissions
2. **Disk Space**: Ensure sufficient disk space
3. **Path Validity**: Verify file paths are correct
4. **File Locks**: Check for file locking issues

---

## 🎯 Service-Specific Errors

### Session Management Errors

```
Log Message: "Failed to create session: {SessionId}"
Log Level: Error
Service: SessionManager
```

**Common Causes**:
- Browser driver not available
- Session storage issues
- Memory constraints

### JavaScript Interaction Errors

```
Log Message: "JavaScript execution failed: {Description}"
Log Level: Error
Service: JavaScriptInteractionManager
```

**Common Causes**:
- JavaScript errors on page
- Security restrictions
- Invalid JavaScript code

### Detection Metrics Warnings

```
Log Message: "Could not find detection attempt with ID: {attemptId}"
Log Level: Warning
Service: DetectionMetricsService
```

**Common Causes**:
- Concurrent access issues
- Memory cleanup
- Invalid attempt tracking

---

## ⚠️ Warning Messages

### Common Warning Scenarios

#### Timeout Warnings
```
Log Message: "Operation timeout"
Log Level: Warning
Service: TimeoutManager
```

#### Element Detection Warnings
```
Log Message: "Element '{ElementSelector}' did not appear within timeout"
Log Level: Warning
Service: MultiStepLoginNavigator
```

#### Performance Warnings
```
Log Message: "JavaScript execution timed out: {Description}"
Log Level: Warning
Service: JavaScriptInteractionManager
```

#### Session Warnings
```
Log Message: "Recovery attempt {Attempt} failed for session: {SessionId}"
Log Level: Warning
Service: SessionManager
```

---

## 🔍 Troubleshooting Guide

### General Debugging Steps

#### 1. Enable Debug Mode
```powershell
ChromeConnect.exe --debug --USR user --PSW pass --URL "https://example.com" --DOM CORP --INCOGNITO yes --KIOSK no --CERT ignore
```

#### 2. Check Screenshots
Debug mode automatically captures screenshots on errors. Check the screenshots directory for visual debugging.

#### 3. Review Logs
Enable detailed logging to understand the execution flow:
```json
{
  "Logging": {
    "LogLevel": "Debug",
    "LogDirectory": "logs",
    "LogSensitiveInfo": false
  }
}
```

#### 4. Test Manually
Always test the target URL manually in a browser to understand the expected behavior.

### Specific Error Categories

#### Browser Launch Issues
1. **Chrome Installation**: Verify Google Chrome is installed
2. **ChromeDriver**: Ensure ChromeDriver is available and version-compatible
3. **System Resources**: Check available memory and CPU
4. **Permissions**: Ensure appropriate execution permissions

#### Login Detection Issues
1. **Page Analysis**: Manually inspect the login page
2. **Network Tab**: Check for AJAX requests and dynamic content
3. **Console Errors**: Look for JavaScript errors
4. **Element Inspection**: Verify form field selectors

#### Network Connectivity Issues
1. **Basic Connectivity**: Test with ping and traceroute
2. **DNS Resolution**: Verify domain name resolution
3. **Proxy Settings**: Check corporate proxy configuration
4. **Firewall Rules**: Review firewall and security software

#### Authentication Failures
1. **Credential Verification**: Test credentials manually
2. **Account Status**: Check for account lockouts or restrictions
3. **Multi-Factor Authentication**: Identify MFA requirements
4. **Domain Requirements**: Verify domain authentication needs

### Performance Optimization

#### Timeout Tuning
```json
{
  "Browser": {
    "PageLoadTimeoutSeconds": 30,
    "ElementWaitTimeSeconds": 10
  },
  "ErrorHandling": {
    "MaxRetryAttempts": 3,
    "InitialRetryDelayMs": 1000,
    "MaxRetryDelayMs": 30000
  }
}
```

#### Resource Management
- Use incognito mode to avoid session conflicts
- Close browser properly after each execution
- Monitor memory usage for long-running operations

---

## 📊 Error Severity Levels

### Critical Errors
- Browser initialization failures
- System resource exhaustion
- Security certificate errors (when not ignored)

### Standard Errors
- Login form detection failures
- Authentication rejections
- Network connectivity issues

### Warnings
- Operation timeouts (with retries available)
- Performance issues
- Non-critical feature failures

### Information
- Successful operations
- Status updates
- Performance metrics

---

## 🚨 Emergency Procedures

### System Recovery
1. **Kill Processes**: Terminate all Chrome and ChromeDriver processes
2. **Clear Cache**: Remove browser cache and temporary files
3. **Restart Services**: Restart relevant system services
4. **Check Logs**: Review system and application logs

### Escalation Criteria
- Repeated browser initialization failures
- Consistent authentication failures across multiple systems
- Performance degradation beyond acceptable thresholds
- Security-related errors

---

## 📈 Error Monitoring and Analytics

### Key Metrics to Monitor
- Authentication success rate
- Average response times
- Error frequency by type
- Browser stability metrics

### Automated Monitoring
```powershell
# PowerShell script for monitoring
$result = ChromeConnect.exe --USR $user --PSW $pass --URL $url --DOM $domain --INCOGNITO yes --KIOSK no --CERT ignore
switch ($LASTEXITCODE) {
    0 { Write-Host "✅ Success" -ForegroundColor Green }
    1 { Write-Host "❌ Login Failed" -ForegroundColor Red }
    2 { Write-Host "⚠️ System Error" -ForegroundColor Yellow }
    3 { Write-Host "🔧 Config Error" -ForegroundColor Magenta }
    default { Write-Host "❓ Unknown Error: $LASTEXITCODE" -ForegroundColor Cyan }
}
```

### Log Analysis
- Pattern recognition for recurring issues
- Performance trend analysis
- Success rate tracking
- Error categorization and reporting

---

*This error reference is maintained alongside the ChromeConnect codebase and updated with each release. For the most current information, always refer to the latest version in the repository.* 