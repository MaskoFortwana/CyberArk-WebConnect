# ChromeConnect

<div align="center">

![ChromeConnect Logo](docs/images/logo.png)

**A powerful Windows automation tool for web portal authentication**

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Windows](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/yourorg/chromeconnect)](https://github.com/yourorg/chromeconnect/releases)

</div>

## 🚀 Overview

ChromeConnect is a Windows-based automation utility that streamlines web portal authentication using command-line parameters. Built with .NET 8.0 and Selenium WebDriver, it provides a reliable, secure, and user-friendly solution for automated login processes.

### ✨ Key Features

- **🔒 Secure Authentication**: Safe credential handling with masked password logging
- **🎯 Smart Detection**: Multiple login field detection strategies for diverse web portals
- **📸 Auto Screenshots**: Captures screenshots during failed logins for easy troubleshooting
- **⌨️ Human-like Typing**: Simulates natural typing patterns to avoid detection
- **🖥️ Self-contained**: Standalone Windows executable - no additional dependencies required
- **📊 Comprehensive Logging**: Detailed logs for monitoring and debugging
- **🌐 Chrome Integration**: Seamless integration with Google Chrome browser
- **⚡ High Performance**: Fast execution with .NET 8.0 runtime

---

## 📦 Installation

### Option 1: Download Pre-built Executable (Recommended)

1. **Download the latest release**
   - Visit [Releases](https://github.com/yourorg/chromeconnect/releases)
   - Download `ChromeConnect-X.X.X-win-x64.zip` (64-bit) or `ChromeConnect-X.X.X-win-x86.zip` (32-bit)

2. **Extract and setup**
   ```powershell
   # Extract to your preferred location
   Expand-Archive -Path ChromeConnect-X.X.X-win-x64.zip -DestinationPath C:\ChromeConnect
   
   # (Optional) Add to PATH for global access
   $env:PATH += ";C:\ChromeConnect"
   ```

3. **Verify installation**
   ```powershell
   ChromeConnect.exe --version
   ```

### Option 2: Build from Source

#### Prerequisites
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Git](https://git-scm.com/downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (optional)

#### Build Steps
```powershell
# Clone the repository
git clone https://github.com/yourorg/chromeconnect.git
cd chromeconnect

# Build using the provided script (PowerShell)
./publish.ps1 -Version "1.0.0" -Configuration Release -RuntimeIdentifier "win-x64"

# Build for 32-bit Windows
./publish.ps1 -Version "1.0.0" -Configuration Release -RuntimeIdentifier "win-x86"

# Or build manually
dotnet publish src/ChromeConnect -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

---

## 🚀 Quick Start

### Basic Usage
```powershell
ChromeConnect.exe --USR alice --PSW s3cr3t --URL https://login.example.com --DOM mycompany --INCOGNITO no --KIOSK no --CERT ignore
```

### Real-world Example
```powershell
# Corporate portal login
ChromeConnect.exe --USR john.doe --PSW MySecurePass123! --URL https://portal.corporate.com/login --DOM CORPORATE --INCOGNITO yes --KIOSK no --CERT ignore --debug
```

---

## 📖 Command-line Reference

### Required Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `--USR` | Username for login form | `--USR john.doe` |
| `--PSW` | Password (automatically masked in logs) | `--PSW MyPassword123` |
| `--URL` | Target login page URL | `--URL https://login.example.com` |
| `--DOM` | Domain or tenant identifier | `--DOM CORPORATE` |
| `--INCOGNITO` | Enable incognito mode (`yes`/`no`) | `--INCOGNITO yes` |
| `--KIOSK` | Enable kiosk mode (`yes`/`no`) | `--KIOSK no` |
| `--CERT` | Certificate validation (`ignore`/`enforce`) | `--CERT ignore` |

### Optional Parameters

| Parameter | Description | Default | Example |
|-----------|-------------|---------|---------|
| `--debug` | Enable debug logging | `false` | `--debug` |
| `--version` | Display version information | - | `--version` |
| `--help` | Show help information | - | `--help` |

---

## 💡 Usage Examples

### Corporate Single Sign-On (SSO)
```powershell
ChromeConnect.exe --USR employee.id --PSW CompanyPassword --URL https://sso.company.com --DOM COMPANY --INCOGNITO yes --KIOSK no --CERT ignore
```

### Development Environment
```powershell
ChromeConnect.exe --USR testuser --PSW devpass123 --URL https://dev.example.com/login --DOM DEV --INCOGNITO no --KIOSK no --CERT ignore --debug
```

### Secure Production Environment
```powershell
ChromeConnect.exe --USR prod.user --PSW SecureProductionPass --URL https://secure.production.com --DOM PROD --INCOGNITO yes --KIOSK yes --CERT enforce
```

### Automated Testing
```powershell
# Create a batch script for automated testing
ChromeConnect.exe --USR test.automation --PSW AutomationPass --URL https://test.portal.com --DOM TEST --INCOGNITO yes --KIOSK no --CERT ignore
```

---

## 🔧 Configuration

### Embedded Configuration

ChromeConnect uses **embedded configuration** - no external configuration files are required! All settings are built into the executable with sensible defaults:

- **Default Timeout**: 30 seconds
- **Max Retry Attempts**: 3
- **Screenshot on Error**: Enabled
- **Logging Level**: Information (use `--debug` for detailed logging)
- **Log Location**: Windows temp folder (`%TEMP%\ChromeConnect\`)

### Command-line Overrides

All configuration can be controlled via command-line parameters:

| Parameter | Description | Default |
|-----------|-------------|---------|
| `--debug` | Enable debug logging | `false` |
| `--version` | Show version and deployment info | - |

---

## 🔐 Enterprise Compatibility

### AppLocker DLL Extraction Solution

ChromeConnect includes a **specialized DLL extraction solution** designed for enterprise environments with AppLocker or similar security policies that restrict runtime DLL extraction.

#### ✨ Key Features
- **🛡️ AppLocker Compatible**: No runtime extraction to restricted directories
- **📦 Pre-extracted Dependencies**: All DLLs included in deployment package
- **🔧 Zero Configuration**: Works out-of-the-box in restricted environments
- **⚡ Automated**: Fully integrated into build pipeline

#### 🎯 Target Environments
- **CyberArk PSM**: Privileged Session Management environments
- **Corporate Workstations**: With strict AppLocker policies
- **Zero-Trust Networks**: Where file system access is controlled
- **Secure Environments**: Any environment blocking temp directory writes

#### 📋 How It Works
1. **Build-Time Extraction**: DLLs are pre-extracted during the build process
2. **Package Integration**: Extracted dependencies are included in deployment ZIP
3. **Runtime Resolution**: .NET finds dependencies without temp directory access
4. **Security Compliance**: No policy violations or permission issues

For detailed information, see [DLL_EXTRACTION_SOLUTION.md](DLL_EXTRACTION_SOLUTION.md).

---

## 🔍 Browser Behavior

### Successful Login
- ✅ Application terminates with exit code `0`
- 🌐 Chrome browser **remains open** for continued use
- 📄 Session is preserved for further browsing

### Failed Login
- ❌ Application terminates with exit code `1`
- 🔒 Chrome browser **closes automatically**
- 📸 Screenshot captured for troubleshooting
- 📝 Detailed error logged

---

## 📸 Screenshots & Logging

### Automatic Screenshots
ChromeConnect captures screenshots during failures for easy troubleshooting:

| Scenario | Filename Pattern | Location |
|----------|------------------|----------|
| Login Failed | `LoginFailed_YYYYMMDD_HHMMSS.png` | `%TEMP%\ChromeConnect\screenshots\` |
| Verification Error | `VerificationError_YYYYMMDD_HHMMSS.png` | `%TEMP%\ChromeConnect\screenshots\` |
| Browser Error | `BrowserError_YYYYMMDD_HHMMSS.png` | `%TEMP%\ChromeConnect\screenshots\` |
| Form Not Found | `FormNotFound_YYYYMMDD_HHMMSS.png` | `%TEMP%\ChromeConnect\screenshots\` |

### Log Files
Detailed logs are stored in the Windows temp folder:

```
%TEMP%\ChromeConnect\
├── chromeconnect-20241123.log    # Daily log files
├── chromeconnect-20241124.log
└── screenshots/                  # Error screenshots
    ├── LoginFailed_20241123_143022.png
    └── ...
```

**Log Levels:**
- `Information`: General operation status
- `Warning`: Non-critical issues
- `Error`: Critical failures
- `Debug`: Detailed debugging information (use `--debug`)

---

## 🛠️ Troubleshooting

### Common Issues

#### ❌ "Chrome driver not found"
**Solution:**
```powershell
# ChromeConnect automatically downloads ChromeDriver
# Ensure internet connectivity and check logs
ChromeConnect.exe --debug --USR test --PSW test --URL https://example.com --DOM test --INCOGNITO no --KIOSK no --CERT ignore
```

#### ❌ "Login form not detected"
**Cause:** Website structure may have changed
**Solution:**
1. Check the screenshot in `%TEMP%\ChromeConnect\screenshots\`
2. Verify the URL is correct
3. Check if the site requires specific browser settings

#### ❌ "Permission denied" errors
**Cause:** Insufficient permissions or antivirus blocking
**Solution:**
1. Run as administrator
2. Add ChromeConnect to antivirus exclusions
3. Check Windows Defender settings

#### ❌ "Browser launch failed"
**Cause:** Chrome not installed or incompatible version
**Solution:**
1. Install [Google Chrome](https://www.google.com/chrome/)
2. Update Chrome to the latest version
3. Check Windows compatibility

### Debug Mode
Enable detailed logging for troubleshooting:
```powershell
ChromeConnect.exe --debug --USR your.user --PSW your.password --URL https://your.site.com --DOM YOUR_DOMAIN --INCOGNITO no --KIOSK no --CERT ignore
```

---

## 🔄 Exit Codes

| Code | Status | Description |
|------|--------|-------------|
| `0` | Success | Login completed successfully |
| `1` | Failure | Login attempt failed (invalid credentials, form not found, etc.) |
| `2` | Error | Application error (browser launch failed, network issues, etc.) |

---

## 🏗️ Architecture

ChromeConnect is built with a modular architecture:

```
ChromeConnect/
├── Core/
│   ├── BrowserManager.cs          # Chrome automation
│   ├── TimeoutManager.cs          # Timeout handling
│   └── Constants.cs               # Application constants
├── Services/
│   ├── ChromeConnectService.cs    # Main orchestration
│   ├── LoginDetector.cs          # Form detection
│   ├── LoginPerformer.cs         # Login execution
│   ├── SessionManager.cs         # Session management
│   └── ErrorHandler.cs           # Error handling
├── Models/
│   ├── CommandLineOptions.cs     # CLI options
│   ├── LoginFormElements.cs      # Form models
│   └── Configuration models      # Various config models
└── Exceptions/
    └── Custom exception classes  # Specific exceptions
```

---

## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [Selenium WebDriver](https://www.selenium.dev/) for browser automation
- [WebDriverManager](https://github.com/rosolko/WebDriverManager.Net) for automatic driver management
- [Serilog](https://serilog.net/) for structured logging
- [CommandLineParser](https://github.com/commandlineparser/commandline) for CLI handling

---

## 📞 Support

- 📚 **Documentation**: [Full Documentation](docs/)
- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/yourorg/chromeconnect/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/yourorg/chromeconnect/discussions)
- 📧 **Email**: support@chromeconnect.com

---

<div align="center">

**Made with ❤️ by the ChromeConnect Team**

[⭐ Star this repo](https://github.com/yourorg/chromeconnect) | [🐛 Report Bug](https://github.com/yourorg/chromeconnect/issues) | [🚀 Request Feature](https://github.com/yourorg/chromeconnect/issues)

</div>
