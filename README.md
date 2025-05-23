# ChromeConnect

A Windows-based automation utility for signing into web portals using command-line parameters and verifying credential correctness.

## Features

- Command-line interface for web portal authentication
- Chrome automation using Selenium WebDriver
- Multiple login field detection strategies
- Secure credential handling
- Screenshot capture for troubleshooting failed logins
- Human-like typing simulation
- Compiled as a standalone Windows executable (.exe)

## Usage

```powershell
ChromeConnect.exe -USR alice -PSW s3cr3t -URL https://login.example.com -DOM myco -INCOGNITO yes -KIOSK no -CERT enforce
```

### Command-line Parameters

| Flag | Required | Values | Behavior |
|------|----------|--------|-----------|
| `-USR` | ✓ | string | Username for login form |
| `-PSW` | ✓ | string | Password (masked in logs) |
| `-URL` | ✓ | valid URL | Target login page |
| `-DOM` | ✓ | string | Domain or tenant identifier |
| `-INCOGNITO` | ✓ | `yes`/`no` | Adds `--incognito` when `yes` |
| `-KIOSK` | ✓ | `yes`/`no` | Adds `--kiosk` when `yes` |
| `-CERT` | ✓ | `ignore`/`enforce` | Adds `--ignore-certificate-errors` when `ignore` |

## Browser Behavior

- **Successful Login**: The application will terminate, but the Chrome browser session it initiated will remain open. This allows you to continue using the authenticated session.
- **Unsuccessful Login**: The application will terminate, and the Chrome browser will be closed automatically.

## Screenshot Capture

ChromeConnect automatically captures screenshots during failed login attempts to assist with troubleshooting:

- **When Captured**: Screenshots are taken when error messages are detected or when exceptions occur during login verification
- **Location**: Saved in the `screenshots` directory within the application's installation folder
- **Filename Format**: `[Scenario]_[Timestamp].png` (e.g., `LoginFailed_20250522_152412.png`)
- **Screenshot Types**:
  - `LoginFailed_*.png`: Shows error messages displayed by the login page
  - `VerificationError_*.png`: Captures exceptions during the verification process

These screenshots provide valuable diagnostic information for identifying login issues, including:
- Visible error messages
- Form field validation states
- UI inconsistencies or unexpected page layouts
- Browser rendering problems

When requesting support, include relevant screenshots along with the command used (with sensitive data redacted).

## Requirements

- Windows OS (Windows 10/11 recommended)
- Google Chrome browser installed
- .NET Runtime (only needed if not using self-contained executable)

## Building from Source

### Prerequisites

- .NET SDK 8.0 or later
- Visual Studio 2022 or other .NET-compatible IDE

### Build Commands

```powershell
# Clone the repository
git clone https://github.com/yourusername/ChromeConnect.git
cd ChromeConnect

# Build (Debug)
dotnet build

# Build (Release)
dotnet build -c Release

# Publish self-contained executable
dotnet publish src/ChromeConnect/ChromeConnect.csproj -c Release -r win-x64 --self-contained
```

The compiled executable will be available in `src/ChromeConnect/bin/Release/net8.0/win-x64/publish/ChromeConnect.exe`.

## License

MIT
