# ChromeConnect - Main Application Flow

This document describes how all components of ChromeConnect are integrated into the main application flow.

## Overview

ChromeConnect is a modular console application that automates the process of logging into web applications using Chrome browser automation. The application follows a clean architecture with separate components for each responsibility, all orchestrated by the main `ChromeConnectService` class.

## Component Integration

The following diagram illustrates how the components are integrated:

```
┌─────────────────┐
│   Program.cs    │
│  (Entry Point)  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ChromeConnectSvc │◄────┐
│  (Orchestrator) │     │
└────────┬────────┘     │
         │              │
   ┌─────┴─────┐        │
   │           │        │
   ▼           ▼        │
┌─────┐   ┌────────┐    │
│Error│   │Browser │    │
│Hdlr │   │Manager │    │
└──┬──┘   └───┬────┘    │
   │          │         │
   │     ┌────▼─────┐   │
   │     │  Login   │   │
   │     │ Detector │   │
   │     └────┬─────┘   │
   │          │         │
   │     ┌────▼──────┐  │
   │     │Credential │  │
   │     │ Manager   │  │
   │     └────┬──────┘  │
   │          │         │
   │     ┌────▼──────┐  │
   │     │  Login    │  │
   │     │ Verifier  │  │
   │     └────┬──────┘  │
   │          │         │
   └──────────┼─────────┘
              │
              ▼
         [Login Result]
```

## Application Flow

The main application flow follows these steps:

1. **Startup and Configuration**:
   - The application starts from `Program.cs`
   - Command-line arguments are parsed
   - A host builder is created and configured
   - Configuration is loaded from appsettings.json
   - Services are registered with dependency injection
   - The `ChromeConnectService` is resolved and executed

2. **Browser Launch**:
   - The `BrowserManager` launches Chrome with appropriate settings
   - Error handling ensures any browser launch failures are properly handled

3. **Login Form Detection**:
   - The `LoginDetector` scans the page for login form elements
   - Timeout management ensures the operation doesn't hang indefinitely
   - Multiple detection strategies are used for robustness

4. **Credential Entry**:
   - The `CredentialManager` enters credentials into the detected form
   - Credentials are entered in a "human-like" manner
   - The form is submitted either by clicking a button or pressing Enter

5. **Login Verification**:
   - The `LoginVerifier` checks if the login was successful
   - Multiple verification strategies are employed
   - Screenshots are captured on login failure

6. **Error Handling**:
   - The `ErrorHandler` manages exceptions throughout the process
   - The `TimeoutManager` enforces timeouts for all operations
   - Retries are used for transient failures
   - The `ErrorMonitor` tracks errors and identifies patterns

7. **Result Reporting**:
   - The application returns appropriate exit codes
   - Log messages provide detailed information about each step
   - Resources are properly cleaned up before exit

## Configuration

The application is configured through various settings classes:

- **AppSettings**: Main configuration class with settings for:
  - Browser (Chrome driver path, timeouts, etc.)
  - Logging (directory, file size, etc.)
  - Error handling (retries, timeouts, etc.)

These settings are bound from appsettings.json and provided to services through dependency injection.

## Error Handling

Error handling is a central part of the application flow:

1. **Exception Hierarchy**:
   - `ChromeConnectException`: Base exception class
   - `BrowserException`: Browser-related issues
   - `LoginException`: Login-related issues
   - `NetworkException`: Network connectivity issues
   - `AppSystemException`: General application errors

2. **Error Recovery Strategy**:
   - Retry for transient issues (e.g., network errors)
   - Exponential backoff with jitter to avoid thundering herd
   - Graceful degradation when possible
   - Clean resource disposal even during errors

3. **Exit Codes**:
   - 0: Success - login completed successfully
   - 1: Login failure - invalid credentials, form not found, etc.
   - 2: System error - browser not launching, network error, etc.
   - 3: Configuration error - invalid settings, missing files, etc.

## Dependency Injection

The application uses Microsoft's dependency injection container:

- **ServiceCollectionExtensions**: Provides extension methods to register services
- **Constructor Injection**: All services receive their dependencies via constructor
- **Singleton Lifetimes**: Most services are registered as singletons

## Testing

The application includes tests for both individual components and integrated workflows:

- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test how components work together
- **Mock Objects**: Used to simulate dependencies and test error scenarios

See the test files in the `tests/ChromeConnect.Tests` directory for examples.

## Command-Line Interface

The application is invoked from the command line with these required parameters:

- `--USR`: Username for login
- `--PSW`: Password (masked in logs)
- `--URL`: Target login page URL
- `--DOM`: Domain or tenant identifier
- `--INCOGNITO`: Whether to run in incognito mode ("yes"/"no")
- `--KIOSK`: Whether to run in kiosk mode ("yes"/"no")
- `--CERT`: Certificate validation ("ignore"/"enforce")

Optional parameters:
- `--version`: Display version information
- `--debug`: Enable debug logging

Example:
```
ChromeConnect --USR=myuser --PSW=mypassword --URL=https://example.com/login --DOM=mydomain --INCOGNITO=yes --KIOSK=no --CERT=enforce
```

## Extending the Application

The modular architecture makes it easy to extend the application:

1. **Add New Login Detection Strategies**:
   - Implement new detection methods in `LoginDetector`
   
2. **Add New Login Verification Strategies**:
   - Implement new verification methods in `LoginVerifier`
   
3. **Support New Error Types**:
   - Add new exception classes to the exception hierarchy
   - Update `ErrorHandler` to handle the new exception types

4. **Add New Functionality**:
   - Create new service classes
   - Register them in `ServiceCollectionExtensions`
   - Inject them where needed 