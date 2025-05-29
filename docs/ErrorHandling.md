# WebConnect Error Handling System

This document provides an overview of the error handling system in WebConnect, including exception hierarchy, error handlers, and best practices for error management.

## Table of Contents

1. [Exception Hierarchy](#exception-hierarchy)
2. [Error Handler Components](#error-handler-components)
3. [Screenshot Capture](#screenshot-capture)
4. [Timeout Management](#timeout-management)
5. [Retry Strategies](#retry-strategies)
6. [Error Monitoring](#error-monitoring)
7. [Testing Error Scenarios](#testing-error-scenarios)
8. [Best Practices](#best-practices)

## Exception Hierarchy

WebConnect uses a structured exception hierarchy to categorize and handle different types of errors:

```
WebConnectException (Base)
├── BrowserException
│   ├── BrowserInitializationException
│   ├── BrowserNavigationException
│   ├── BrowserTimeoutException
│   └── ElementNotFoundException
├── LoginException
│   ├── LoginFormNotFoundException
│   ├── CredentialEntryException
│   └── InvalidCredentialsException
├── NetworkException
│   ├── ConnectionFailedException
│   ├── CertificateException
│   └── RequestTimeoutException
└── AppSystemException
    ├── ConfigurationException
    ├── FileOperationException
    ├── ResourceNotFoundException
    └── AppOperationCanceledException
```

### Common Properties

All WebConnect exceptions include these common properties:

- `Timestamp`: When the exception occurred (UTC)
- `ErrorCode`: A unique identifier for the error type
- `Context`: Additional information about the context in which the error occurred
- Standard .NET Exception properties (`Message`, `InnerException`, etc.)

### When to Use Which Exception

- **BrowserException**: Issues with the browser itself (initialization, navigation, element interaction)
- **LoginException**: Problems during the login process (form detection, credential entry, validation)
- **NetworkException**: Network-related issues (connectivity, certificate validation, timeouts)
- **AppSystemException**: Application or system-level errors (configuration, file operations, resources)

## Error Handler Components

### ErrorHandler Class

The `ErrorHandler` class is the central component for managing exceptions:

```csharp
// Example usage
var errorHandler = new ErrorHandler(logger, screenshotCapture);

// Handle exception
await errorHandler.HandleExceptionAsync(exception, driver);

// Execute with error handling
await errorHandler.ExecuteWithErrorHandlingAsync(async () => {
    // Your code here
}, driver);

// Execute with retry
await errorHandler.ExecuteWithRetryAsync(async () => {
    // Operation that might fail transiently
}, driver, retryCount: 3);
```

#### Key Features

- Automatic screenshot capture on error
- Specialized handling based on exception type
- Resource cleanup during errors
- Exception logging with contextual information
- Retry capabilities with exponential backoff

### Configuration Options

The `ErrorHandlerSettings` class allows customization of error handling behavior:

```csharp
var settings = new ErrorHandlerSettings {
    CaptureScreenshots = true,
    CloseDriverOnError = true,
    DefaultRetryCount = 3,
    DefaultRetryDelayMs = 1000,
    MaxRetryDelayMs = 30000,
    BackoffMultiplier = 2.0,
    AddJitter = true
};

var errorHandler = new ErrorHandler(logger, screenshotCapture, settings);
```

## Screenshot Capture

WebConnect captures screenshots automatically when errors occur to aid in troubleshooting:

### IScreenshotCapture Interface

```csharp
public interface IScreenshotCapture
{
    string CaptureScreenshot(IWebDriver driver, string prefix);
}
```

The `LoginVerifier` class implements this interface to capture browser state during login verification.

### Screenshot Naming Convention

Screenshots are saved with the following naming pattern:
```
Error_{ExceptionType}_{Timestamp}.png
```

For example: `Error_InvalidCredentials_20250523_091532.png`

## Timeout Management

### TimeoutManager Class

The `TimeoutManager` handles timeouts for various operations:

```csharp
// Example usage
var timeoutManager = new TimeoutManager(logger);

// Wait for element with timeout
var element = timeoutManager.WaitForElement(driver, By.Id("loginButton"));

// Execute with timeout
await timeoutManager.ExecuteWithTimeoutAsync(async () => {
    // Operation that needs timeout
}, timeoutMs: 5000, "LoginOperation");
```

### Timeout Settings

The `TimeoutSettings` class allows customization of timeout behavior:

```csharp
var settings = new TimeoutSettings {
    DefaultTimeoutMs = 30000,  // 30 seconds
    ElementTimeoutMs = 10000,  // 10 seconds
    ConditionTimeoutMs = 15000, // 15 seconds
    UrlChangeTimeoutMs = 5000   // 5 seconds
};

var timeoutManager = new TimeoutManager(logger, settings);
```

## Retry Strategies

WebConnect includes a flexible retry system for handling transient failures:

### Exponential Backoff with Jitter

The retry system uses exponential backoff with jitter for optimal retry behavior:

1. Start with an initial delay (e.g., 1 second)
2. For each retry, multiply by the backoff factor (e.g., 2.0)
3. Add random jitter (±15%) to avoid synchronized retries
4. Cap at a maximum delay (e.g., 30 seconds)

### Transient Error Detection

The system automatically identifies errors that are likely to be transient:

- Network connectivity issues
- Request timeouts
- Temporary WebDriver errors
- Resource conflicts

## Error Monitoring

### ErrorMonitor Class

The `ErrorMonitor` tracks error patterns and provides reporting capabilities:

```csharp
// Example usage
var errorMonitor = new ErrorMonitor(logger);

// Record an error
errorMonitor.RecordError(exception, "LoginModule");

// Get error metrics
var metrics = errorMonitor.GetAllErrorMetrics();
var recentErrors = errorMonitor.GetRecentErrors(10);

// Generate a report
var report = errorMonitor.GenerateReport();
```

### Error Reporting

The system provides several reporting capabilities:

- Automatic threshold alerts for frequent errors
- Periodic error summary reports
- Error rate monitoring and alerting
- Detailed error metrics by type

## Testing Error Scenarios

### Unit Testing Exception Handling

```csharp
[Fact]
public async Task HandleExceptionAsync_CapturessScreenshot_WhenDriverProvided()
{
    // Arrange
    var mockLogger = new Mock<ILogger<ErrorHandler>>();
    var mockScreenshotCapture = new Mock<IScreenshotCapture>();
    mockScreenshotCapture
        .Setup(x => x.CaptureScreenshot(It.IsAny<IWebDriver>(), It.IsAny<string>()))
        .Returns("screenshot.png");
    
    var mockDriver = new Mock<IWebDriver>();
    var exception = new LoginException("Test exception");
    
    var errorHandler = new ErrorHandler(mockLogger.Object, mockScreenshotCapture.Object);
    
    // Act
    await errorHandler.HandleExceptionAsync(exception, mockDriver.Object);
    
    // Assert
    mockScreenshotCapture.Verify(x => x.CaptureScreenshot(
        mockDriver.Object, It.Is<string>(s => s.Contains("Login"))), Times.Once);
}
```

### Testing Retry Logic

```csharp
[Fact]
public async Task ExecuteWithRetryAsync_RetriesCorrectNumberOfTimes_BeforeFailure()
{
    // Arrange
    var mockLogger = new Mock<ILogger<ErrorHandler>>();
    var mockScreenshotCapture = new Mock<IScreenshotCapture>();
    
    var errorHandler = new ErrorHandler(mockLogger.Object, mockScreenshotCapture.Object);
    
    int attemptCount = 0;
    
    // Act & Assert
    await Assert.ThrowsAsync<ConnectionFailedException>(async () => {
        await errorHandler.ExecuteWithRetryAsync(async () => {
            attemptCount++;
            throw new ConnectionFailedException("Connection failed");
        }, retryCount: 3);
    });
    
    Assert.Equal(4, attemptCount); // Initial attempt + 3 retries
}
```

## Best Practices

### Throwing Exceptions

1. Use the most specific exception type
2. Include meaningful error messages
3. Add context information and error codes
4. Preserve the original exception as innerException

```csharp
// Good:
throw new LoginFormNotFoundException(
    "Login form elements not found after navigation completed",
    "LOGIN_FORM_404",
    $"URL: {driver.Url}",
    originalException);

// Bad:
throw new Exception("Login failed");
```

### Handling Exceptions

1. Use the ErrorHandler for consistent exception management
2. Allow transient errors to be retried
3. Capture relevant context when catching exceptions
4. Clean up resources in finally blocks or use using statements

```csharp
// Good:
try
{
    await errorHandler.ExecuteWithRetryAsync(async () => {
        // Login operation
    }, driver);
}
catch (LoginException ex) when (ex is not InvalidCredentialsException)
{
    // Handle recoverable login errors
}
catch (Exception ex)
{
    // Handle other errors
}
finally
{
    // Cleanup resources
}

// Bad:
try
{
    // Login operation
}
catch
{
    // Swallow exception without handling or logging
}
```

### Resource Management

1. Use `using` statements for disposable resources
2. Implement explicit cleanup in error handlers
3. Ensure driver disposal even during errors
4. Release resources in the correct order

### Error Reporting

1. Record all significant errors in the ErrorMonitor
2. Add context information to help with troubleshooting
3. Establish error thresholds for alerting
4. Review error reports periodically to identify patterns

---

## Conclusion

The WebConnect error handling system provides robust mechanisms for detecting, reporting, and recovering from failures. Following these guidelines will help ensure a resilient and maintainable application that gracefully handles the complexities of automated web authentication. 