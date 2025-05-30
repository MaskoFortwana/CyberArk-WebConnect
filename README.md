# WebConnect

<div align="center">

![WebConnect Logo](docs/images/logo-light.png)

**CyberArk Connection Component for Automated Web Authentication**

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Windows](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)
[![CyberArk PSM](https://img.shields.io/badge/CyberArk-PSM-green.svg)](https://www.cyberark.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

</div>

## 🚀 Overview

WebConnect is a specialized **CyberArk connection component** designed to provide automated access to web applications through CyberArk's Privileged Session Management (PSM). Unlike traditional connection components that require manual WebFormFields configuration for each target website, WebConnect **automatically detects login fields and verifies login success**, dramatically reducing administrative overhead and saving significant configuration time.

**Please note**
The whole code was completely written by AI using Cursor.
Code security is being verified on every change by using Snyk
It is meant to be a open-source community project, it is not related to CyberArk company anyhow
Use at own risk, no guarantees
Feel free to reach out for any questions

### Key Benefits for CyberArk Administrators

- **🎯 Zero WebFormFields Configuration**: Eliminates the need to manually specify login field selectors for each website
- **🔍 Intelligent Field Detection**: Automatically identifies username, password, and domain fields using advanced algorithms  
- **✅ Automatic Login Verification**: Confirms successful authentication without manual success criteria configuration
- **⏱️ Consistent Timeouts**: All logins complete within 20-60 seconds with proper timeout handling
- **🛡️ Enterprise Security**: Compatible with CyberArk security standards and AppLocker policies
- **📊 Comprehensive Logging**: Detailed audit trails stored in PSM shadow user locations

This connection component **transforms web authentication management** by removing the complexity of custom field mapping, allowing administrators to focus on drinking coffee than technical implementation details.

---

## ✨ Key Features

### Automatic Authentication
- **Smart Field Detection**: Intelligently identifies login form elements without configuration
- **Success Verification**: Automatically determines when login has completed successfully
- **Domain Handling**: Supports both username/password and username/domain/password scenarios
- **Dropdown Support**: Handles username and domain dropdown menus automatically


### CyberArk Integration
- **Seamless PSM Integration**: Works directly with CyberArk's session management infrastructure
- **Credential Injection**: Receives credentials securely from CyberArk Password Vault
- **Session Logging**: All actions are logged through CyberArk's audit framework
- **Shadow User Support**: Operates within PSM shadow user context

1. **Download the latest release**
   - Visit [Releases](https://github.com/MaskoFortwana/CyberArk-WebConnect/releases)
   - Download `WebConnect-X.X.X-win-x64.zip`

---

## 🖥️ System Requirements

### Prerequisites
- **CyberArk Components**: 
  - CyberArk PSM Server (v13.0 or later recommended)
- **Browser Requirements**:
  - Google Chrome (latest stable version)
  - ChromeDriver v136+ (matching Chrome version)
- **Runtime**: .NET 8.0 Runtime (included in deployment, no need to install on PSM server)

### Supported Environments
- **Primary**: CyberArk PSM (Privileged Session Management)
- **Testing**: Standalone testing outside CyberArk environment

### Browser Version Compatibility
- **Tested and Working**: Chrome v136 with ChromeDriver v136
- **Requirement**: Chrome and ChromeDriver versions **must match exactly**

⚠️ **Important**: Ensure Chrome and ChromeDriver versions are synchronized. Version mismatches will cause connection failures.

---

## 📦 CyberArk Installation & Configuration

### 1. Component Deployment

Deploy WebConnect to your PSM server in the standard CyberArk components directory:

```
C:\Program Files (x86)\CyberArk\PSM\Components\
├── WebConnect.exe                    # Main executable
├── WebConnect-Wrapper.exe           # AutoIt wrapper script
├── WebConnect-Wrapper.au3           # AutoIt source
└── WebConnect\                       # Dependencies directory
    ├── *.dll
    └──selenium-manager\
        └── windows\
	        └──selenium-manager.exe

```

### 2. AppLocker Configuration

**Critical**: The following AppLocker rules are **required** for WebConnect to function:

#### EXE Files
```xml
<!-- WebConnect START -->
<Application Name="WebConnect" Type="Exe" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect.exe" Method="Hash" />
<Application Name="WebConnect-Wrapper" Type="Exe" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect-Wrapper.exe" Method="Hash" />
<Application Name="WebConnect2" Type="Exe" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\WebConnect.exe" Method="Hash" />
<Application Name="ChromeDriver2" Type="Exe" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\chromedriver.exe" Method="Hash" />
<Application Name="WebConnect-Manager" Type="Exe" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\selenium-manager\windows\selenium-manager.exe" Method="Hash" />
<!-- WebConnect END -->
```

#### DLL Files
```xml
<Libraries Name="WebConnect-DLLs" Type="Dll" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\*" Method="Path" />
```

### 3. AutoIT wrapper Configuration

AutoIT script is being used as "middle-man" between WebConnect and CyberArk, can be found in cyb-deploy folder of the release .zip file.
Its only purpose is to execute the WebConnect.exe correctly, using the parameters from Comment parameter that is explained below in point 5.

Copy WebConnect-wrapper.exe to Components folder

### 4. Connection Component Configuration

Connection component to import can be found in cyb-deploy folder of the release .zip file.
Can be imported using psPAS or any other way you are used to import connection components to CyberArk

### 5. Platform Configuration

#### Comment Parameter Configuration

WebConnect uses CyberArk's **Comment parameter** for configuration. This parameter **must be added** to your platform definition:

**Parameter Setup:**
- **Name**: `Comment`
#### Comment Parameter Format

Configure the Comment field with the following format:

```
o1=https://|o2=/PasswordVault/v10/logon/ldap|o3=443|o4=none|o5=yes|o6=no|o7=ignore
```

This format is parsed by WebConnect-wrapper.exe and passed correctly to WebConnect.exe
#### Configuration Options Reference

| Option | Parameter        | Description                          | Common Values                      | Example         |
| ------ | ---------------- | ------------------------------------ | ---------------------------------- | --------------- |
| `o1`   | WebPrefix        | Protocol prefix for URL construction | `https://`, `http://`              | `o1=https://`   |
| `o2`   | WebSuffix        | Path suffix appended to hostname     | `/login.htm`, `/auth`, `/signin`   | `o2=/login.htm` |
| `o3`   | WebPort          | TCP port for web connection          | `443` (HTTPS), `80` (HTTP), `8080` | `o3=443`        |
| `o4`   | WebDomain        | Domain for authentication            | `acme.corp`, `none`                | `o4=acme.corp`  |
| `o5`   | WebIncognitoMode | Enable Chrome incognito mode         | `yes`, `no`                        | `o5=yes`        |
| `o6`   | WebKioskMode     | Enable Chrome kiosk mode             | `yes`, `no`                        | `o6=no`         |
| `o7`   | WebCertificate   | SSL certificate validation mode      | `enforce`, `ignore`                | `o7=ignore`     |

#### Configuration Examples

**Standard HTTPS Web Application:**
```
o1=https://|o2=/login|o3=443|o4=none|o5=yes|o6=no|o7=ignore
```

**Corporate Intranet with Domain:**
```
o1=https://|o2=/portal/login.aspx|o3=443|o4=corporate.local|o5=no|o6=no|o7=enforce
```

**Development Environment:**
```
o1=http://|o2=/dev/login|o3=8080|o4=none|o5=yes|o6=no|o7=ignore
```

⚠️ **Important Configuration Notes:**
- **All 7 options (o1-o7) must have values** - even if unused (use `none` for unused domain/port)
- **Recommended Practice**: Configure Comment parameter as **Required** and set at platform level
- **Admin Control**: Prevent users from editing account properties to maintain configuration consistency
- **Check for Known Bugs section

---

## 🔧 Testing Outside of CyberArk

### Prerequisites for Standalone Testing
- Windows machine with Chrome installed
- ChromeDriver v136+ matching your Chrome version
- WebConnect.exe and dependencies
- Testing with production passwords is NOT RECOMMENDED, as those are exposed in clear text

### Basic Test Command
```powershell
WebConnect.exe --USR testuser --PSW testpass --URL https://login.example.com --DOM none --INCOGNITO yes --KIOSK no --CERT ignore
```

### Test Parameter Mapping
When testing outside CyberArk, use these command-line parameters that correspond to Comment options:

| Comment Option | CLI Parameter | Description |
|----------------|---------------|-------------|
| `o1` (WebPrefix) | Include in `--URL` | Use full URL with protocol |
| `o2` (WebSuffix) | Include in `--URL` | Use full URL with path |
| `o3` (WebPort) | Include in `--URL` | Use full URL with port if non-standard |
| `o4` (WebDomain) | `--DOM` | Domain name or `none` |
| `o5` (WebIncognitoMode) | `--INCOGNITO` | `yes` or `no` |
| `o6` (WebKioskMode) | `--KIOSK` | `yes` or `no` |
| `o7` (WebCertificate) | `--CERT` | `ignore` or `enforce` |

### Testing Examples

**Test Corporate Portal:**
```powershell
WebConnect.exe --USR john.doe --PSW MyPassword123 --URL https://portal.company.com:8443/login --DOM CORPORATE --INCOGNITO yes --KIOSK no --CERT ignore
```

**Test Development Environment:**
```powershell
WebConnect.exe --USR devuser --PSW devpass --URL http://localhost:3000/signin --DOM none --INCOGNITO no --KIOSK no --CERT ignore
```

### Verification Steps
1. **Successful Login**: Chrome remains open and authenticated
2. **Failed Login**: Chrome closes automatically with error details
3. **Timeout**: Operation completes within 30-60 seconds
4. **Logs**: Check log output for field detection details

---

## 🎯 Automatic Field Detection vs. Manual Configuration

### Traditional CyberArk Approach
In standard CyberArk connection components, administrators must:
1. **Analyze each target website** HTML structure
2. **Identify CSS selectors** for username/password fields  
3. **Configure WebFormFields** manually for each platform
4. **Test and debug** field mappings for each website
5. **Maintain configurations** when websites change their HTML structure

### WebConnect Automated Approach
WebConnect eliminates this complexity by:
1. **Automatic Detection**: Uses multiple detection strategies to find login fields
2. **Intelligent Algorithms**: Recognizes common field patterns and attributes
3. **Zero Configuration**: No WebFormFields setup required
4. **Adaptive Logic**: Handles various website structures automatically
5. **Maintenance-Free**: Adapts to minor website changes without reconfiguration

### Detection Strategies
WebConnect employs multiple field detection methods:

- **Attribute Analysis**: Examines `name`, `id`, `type`, and `placeholder` attributes
- **Label Association**: Links form labels with their corresponding input fields  
- **Context Analysis**: Understands form structure and field relationships
- **Pattern Recognition**: Identifies common username/password field patterns
- **Dropdown Handling**: Automatically detects and handles username/domain dropdowns

### When Custom Selectors Are Needed
In rare cases where automatic detection fails, custom selectors can be configured through the Comment parameter extensions. Contact your CyberArk administrator for advanced configuration options.

---

## 📊 Log Files & Monitoring

### Log File Location
All WebConnect logs are stored in the PSM shadow user's local application data:

```
C:\Users\PSM-[ShadowUserID]\AppData\Local\Temp\WebConnect\
├── webconnect-YYYYMMDD.log        # Daily rotating logs
├── webconnect-YYYYMMDD-1.log      # Previous day's logs
└── screenshots\                    # Error screenshots
    ├── LoginFailed_YYYYMMDD_HHMMSS.png
    └── VerificationError_YYYYMMDD_HHMMSS.png
```

**Example Path:**
```
C:\Users\PSM-XYZ12093124018\AppData\Local\Temp\WebConnect\
```

### Log Rotation
- **Automatic Rotation**: Logs rotate on each application run
- **Size Management**: Prevents log files from growing too large
- **Retention**: Maintains recent logs while cleaning up old entries
- **Performance**: Ensures consistent application performance

### Log Content
Logs include detailed information about:
- **Field Detection**: What fields were identified and how
- **Authentication Steps**: Each stage of the login process
- **Success/Failure**: Detailed results of login attempts
- **Timing Information**: Execution duration and timeout handling
- **Error Details**: Specific failure reasons and troubleshooting data

### Monitoring in CyberArk
- **PSM Logs**: WebConnect activities appear in standard PSM audit logs
- **Session Recording**: All actions are captured in CyberArk session recordings
- **Vault Logging**: Credential access logged in Password Vault audit trail

---

## 🛠️ Troubleshooting & Common Issues

### ❌ Known Bugs & Issues
1. chromedriver.exe and webconenct.exe has to be placed in 2 locations currently and also allow both paths in applocker
	* C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect
	* C:\Program Files (x86)\CyberArk\PSM\Components\
		* Fix planned for version 1.0.2
2. Not working on websites with cookie notification that is not skippable.
3. 2FA websites not supported
4. Basic authentication not supported yet
	* Fix planned for version 1.0.3

### Login Field Detection Issues

#### ❌ "Username field not detected"
**Possible Causes:**
- Website uses non-standard field attributes
- JavaScript dynamically generates login form
- Multiple login forms present on page

**Resolution Steps:**
1. Check screenshot in logs directory for visual confirmation
2. Verify website loads completely (check for loading indicators)
3. Increase timeout if website loads slowly
4. Contact administrator for custom selector configuration

#### ❌ "Password field not detected"  
**Possible Causes:**
- Password field appears after username entry
- Two-step authentication process
- Dynamic form generation

**Resolution Steps:**
1. Verify the website supports single-page login
2. Check if website requires multi-step authentication
3. Review log files for field detection attempts
4. Test with different browser modes (incognito on/off)

### Authentication Verification Issues

#### ❌ "Login success verification failed"
**Possible Causes:**
- Website redirects to unexpected page
- Login succeeded but success criteria not met
- Multi-factor authentication required

**Resolution Steps:**
1. Check if website requires additional authentication steps
2. Verify success page URL patterns
3. Review screenshot for visual confirmation of page state
4. Check for popup dialogs or additional prompts

### Timeout and Performance Issues

#### ❌ "Operation timed out"
**Possible Causes:**
- Website loads slowly (>30 seconds)
- Network connectivity issues
- Browser startup delays

**Resolution Steps:**
1. Verify network connectivity to target website
2. Test website access manually from PSM server
3. Check Chrome/ChromeDriver version compatibility
4. Review system resource usage during operation

### Browser and ChromeDriver Issues

#### ❌ "ChromeDriver version mismatch"
**Resolution:**
1. Check installed Chrome version: `chrome://version/`
2. Download matching ChromeDriver from [ChromeDriver Downloads](https://chromedriver.chromium.org/downloads)
3. Replace existing ChromeDriver in WebConnect directory
4. Verify versions match exactly

#### ❌ "Chrome failed to start"
**Resolution:**
1. Verify Chrome is installed and accessible
2. Check AppLocker rules are properly configured
3. Ensure PSM shadow user has Chrome access permissions
4. Test Chrome launch manually as shadow user

### Configuration Issues

#### ❌ "Comment parameter format error"
**Resolution:**
1. Verify Comment parameter follows exact format: `o1=value|o2=value|...`
2. Ensure all 7 options (o1-o7) have values
3. Check for special characters that might break parsing
4. Use `none` for unused options rather than leaving empty

#### ❌ "AppLocker blocking execution"
**Resolution:**
1. Verify all required AppLocker rules are configured
2. Check rule paths match actual installation directory
3. Ensure rules apply to correct user groups
4. Test AppLocker policy with manual execution

### Debug Mode
Enable detailed logging for troubleshooting by adding debug parameter in CyberArk platform configuration or testing with:
```powershell
WebConnect.exe --debug --USR test --PSW test --URL https://example.com --DOM none --INCOGNITO yes --KIOSK no --CERT ignore
```

---

## ⏱️ Performance & Timeouts

### Login Completion Times
- **Target Window**: 30 seconds for most websites
- **Maximum Timeout**: 60 seconds in worst-case scenarios
- **Typical Performance**: 15-25 seconds for standard login processes

### Timeout Configuration
WebConnect implements intelligent timeout management:
- **Page Load Timeout**: 30 seconds for initial page loading
- **Element Detection Timeout**: 15 seconds for field identification
- **Authentication Timeout**: 30 seconds for login completion
- **Verification Timeout**: 15 seconds for success confirmation

### Performance Optimization
- **Browser Reuse**: Efficient Chrome session management
- **Smart Waiting**: Intelligent waits for dynamic content
- **Resource Management**: Optimized memory and CPU usage
- **Network Efficiency**: Minimal network overhead

---

## 🏗️ Integration Architecture

### CyberArk Component Integration
```
CyberArk PSM
├── Session Manager
│   ├── Shadow User Creation
│   ├── Credential Injection
│   └── Session Recording
├── WebConnect.exe
│   ├── Credential Reception
│   ├── Browser Automation
│   └── Success Verification
└── Audit & Logging
    ├── Session Logs
    ├── Credential Access Logs
    └── WebConnect Activity Logs
```

### Authentication Flow
1. **PSM Session Initiation**: User requests access to web application
2. **Credential Retrieval**: CyberArk retrieves credentials from Password Vault
3. **WebConnect Launch**: PSM launches WebConnect with credentials
4. **Field Detection**: WebConnect analyzes website and identifies login fields
5. **Credential Entry**: Automated entry of username/password/domain
6. **Success Verification**: Confirms successful authentication
7. **Session Handover**: Browser remains open for user interaction

### Security Boundaries
- **Credential Isolation**: Credentials never stored locally
- **Process Isolation**: WebConnect runs in dedicated PSM context
- **Network Security**: All connections through CyberArk's secure channels
- **Audit Trail**: Complete logging of all authentication activities

---