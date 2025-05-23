# ChromeConnect API Documentation

This documentation provides comprehensive reference information for developers integrating with or extending ChromeConnect. It covers all public APIs, classes, methods, events, and configuration options with detailed examples and usage guidelines.

## 📋 Table of Contents

- [Getting Started](#getting-started)
- [Core Components](#core-components)
- [Service Layer APIs](#service-layer-apis)
- [Data Models](#data-models)
- [Configuration System](#configuration-system)
- [Event System](#event-system)
- [Extension Points](#extension-points)
- [Integration Examples](#integration-examples)
- [Best Practices](#best-practices)
- [Migration Guide](#migration-guide)

---

## 🚀 Getting Started

### Installation and Setup

#### NuGet Package Integration
```xml
<PackageReference Include="ChromeConnect" Version="1.0.0" />
```

#### Basic DI Container Setup
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ChromeConnect.Services;

var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        // Add all ChromeConnect services
        services.AddChromeConnectServices();
        
        // Add configuration binding
        services.AddChromeConnectConfiguration(context.Configuration);
    })
    .Build();

// Get the main service
var chromeConnectService = host.Services.GetRequiredService<ChromeConnectService>();
```

#### Minimal Setup Without DI
```csharp
using Microsoft.Extensions.Logging;
using ChromeConnect.Core;
using ChromeConnect.Services;

// Create logger
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ChromeConnectService>();

// Create services manually
var browserManager = new BrowserManager(loggerFactory.CreateLogger<BrowserManager>());
var loginDetector = new LoginDetector(loggerFactory.CreateLogger<LoginDetector>());
var credentialManager = new CredentialManager(loggerFactory.CreateLogger<CredentialManager>());
var loginVerifier = new LoginVerifier(loggerFactory.CreateLogger<LoginVerifier>());
var screenshotCapture = new ScreenshotCapture();
var errorHandler = new ErrorHandler(loggerFactory.CreateLogger<ErrorHandler>(), screenshotCapture);
var timeoutManager = new TimeoutManager(loggerFactory.CreateLogger<TimeoutManager>());
var errorMonitor = new ErrorMonitor(loggerFactory.CreateLogger<ErrorMonitor>());

// Create main service
var service = new ChromeConnectService(
    logger, browserManager, loginDetector, credentialManager,
    loginVerifier, screenshotCapture, errorHandler, timeoutManager, errorMonitor);
```

---

## 🔧 Core Components

### ChromeConnectService

**Namespace:** `ChromeConnect.Services`  
**Purpose:** Main orchestration service that coordinates the entire authentication workflow.

#### Constructor
```csharp
public ChromeConnectService(
    ILogger<ChromeConnectService> logger,
    BrowserManager browserManager,
    LoginDetector loginDetector,
    CredentialManager credentialManager,
    LoginVerifier loginVerifier,
    IScreenshotCapture screenshotCapture,
    ErrorHandler errorHandler,
    TimeoutManager timeoutManager,
    ErrorMonitor errorMonitor)
```

#### Primary Method
```csharp
/// <summary>
/// Executes the ChromeConnect workflow with the specified options.
/// </summary>
/// <param name="options">The command-line options.</param>
/// <returns>The exit code of the operation (0=success, 1=failure, 2=error).</returns>
public async Task<int> ExecuteAsync(CommandLineOptions options)
```

#### Usage Example
```csharp
var options = new CommandLineOptions
{
    Username = "user@example.com",
    Password = "securePassword123",
    Url = "https://portal.company.com/login",
    Domain = "CORPORATE",
    Incognito = true,
    Kiosk = false,
    IgnoreCertErrors = true,
    Debug = false
};

int exitCode = await chromeConnectService.ExecuteAsync(options);

switch (exitCode)
{
    case 0:
        Console.WriteLine("Login successful!");
        break;
    case 1:
        Console.WriteLine("Login failed - check credentials or form detection");
        break;
    case 2:
        Console.WriteLine("Application error - check logs for details");
        break;
}
```

### BrowserManager

**Namespace:** `ChromeConnect.Core`  
**Purpose:** Manages Chrome browser lifecycle and WebDriver configuration.

#### Primary Method
```csharp
/// <summary>
/// Launches a Chrome browser instance with the specified configuration.
/// </summary>
/// <param name="url">The target URL to navigate to.</param>
/// <param name="incognito">Whether to use incognito mode.</param>
/// <param name="kiosk">Whether to use kiosk mode.</param>
/// <param name="ignoreCertErrors">Whether to ignore certificate errors.</param>
/// <returns>The configured WebDriver instance, or null if launch failed.</returns>
public virtual IWebDriver? LaunchBrowser(string url, bool incognito, bool kiosk, bool ignoreCertErrors)
```

#### Usage Example
```csharp
var browserManager = new BrowserManager(logger);
var driver = browserManager.LaunchBrowser(
    url: "https://example.com/login",
    incognito: true,
    kiosk: false,
    ignoreCertErrors: true);

if (driver != null)
{
    Console.WriteLine($"Browser launched successfully. Current URL: {driver.Url}");
    
    // Use driver for automation...
    
    // Clean up when done
    driver.Quit();
}
```

### LoginDetector

**Namespace:** `ChromeConnect.Services`  
**Purpose:** Detects and analyzes login forms on web pages using multiple strategies.

#### Primary Methods
```csharp
/// <summary>
/// Detects login form elements on the current page.
/// </summary>
/// <param name="driver">The WebDriver instance.</param>
/// <param name="url">The current URL for context.</param>
/// <returns>The detected login form elements, or null if not found.</returns>
public async Task<LoginFormElements?> DetectLoginFormAsync(IWebDriver driver, string url)

/// <summary>
/// Detects login forms with Shadow DOM support.
/// </summary>
/// <param name="driver">The WebDriver instance.</param>
/// <param name="url">The current URL for context.</param>
/// <returns>The detected login form elements with confidence scores.</returns>
public async Task<LoginFormDetectionResult?> DetectWithShadowDOMSupport(IWebDriver driver, string url)
```

#### Usage Example
```csharp
var loginDetector = new LoginDetector(logger);

// Basic detection
var elements = await loginDetector.DetectLoginFormAsync(driver, driver.Url);
if (elements != null)
{
    Console.WriteLine($"Username field: {elements.UsernameField?.TagName}");
    Console.WriteLine($"Password field: {elements.PasswordField?.TagName}");
    Console.WriteLine($"Submit button: {elements.SubmitButton?.TagName}");
}

// Advanced detection with Shadow DOM support
var result = await loginDetector.DetectWithShadowDOMSupport(driver, driver.Url);
if (result != null)
{
    Console.WriteLine($"Detection confidence: {result.ConfidenceScore}");
    Console.WriteLine($"Method used: {result.DetectionMethod}");
    Console.WriteLine($"Elements found: {result.Elements.GetElementCount()}");
}
```

---

## 🎯 Service Layer APIs

### SessionManager

**Namespace:** `ChromeConnect.Services`  
**Purpose:** Manages browser sessions including persistence, validation, and recovery.

#### Key Methods
```csharp
/// <summary>
/// Creates a new session with the specified configuration.
/// </summary>
public async Task<SessionData> CreateSessionAsync(
    IWebDriver driver, 
    string sessionId, 
    string domain, 
    CancellationToken cancellationToken = default)

/// <summary>
/// Validates an existing session.
/// </summary>
public async Task<SessionValidationResult_Model> ValidateSessionAsync(
    IWebDriver driver,
    SessionValidationRequest request,
    CancellationToken cancellationToken = default)

/// <summary>
/// Recovers an expired or invalid session.
/// </summary>
public async Task<SessionRecoveryResult> RecoverSessionAsync(
    IWebDriver driver,
    SessionRecoveryRequest request,
    CancellationToken cancellationToken = default)
```

#### Events
```csharp
/// <summary>
/// Event fired when a session state changes.
/// </summary>
public event EventHandler<SessionStateChangeEventArgs>? SessionStateChanged;

/// <summary>
/// Event fired when a session is validated.
/// </summary>
public event EventHandler<SessionValidationEventArgs>? SessionValidated;

/// <summary>
/// Event fired when a session recovery occurs.
/// </summary>
public event EventHandler<SessionRecoveryEventArgs>? SessionRecovered;
```

#### Usage Example
```csharp
var sessionManager = new SessionManager(logger, timeoutManager, configuration);

// Subscribe to events
sessionManager.SessionStateChanged += (sender, args) =>
{
    Console.WriteLine($"Session {args.SessionId} changed from {args.OldState} to {args.NewState}");
};

// Create a new session
var sessionData = await sessionManager.CreateSessionAsync(
    driver, 
    sessionId: Guid.NewGuid().ToString(), 
    domain: "company.com");

// Validate the session later
var validationRequest = new SessionValidationRequest
{
    SessionId = sessionData.SessionId,
    DeepValidation = true,
    TimeoutSeconds = 10
};

var validationResult = await sessionManager.ValidateSessionAsync(driver, validationRequest);
if (validationResult.IsValid)
{
    Console.WriteLine("Session is still valid");
}
else
{
    // Attempt recovery
    var recoveryRequest = new SessionRecoveryRequest
    {
        ExpiredSessionId = sessionData.SessionId,
        Strategy = SessionRecoveryStrategy.ReAuthenticate,
        Credentials = new Dictionary<string, string>
        {
            ["username"] = "user@example.com",
            ["password"] = "password123"
        }
    };
    
    var recoveryResult = await sessionManager.RecoverSessionAsync(driver, recoveryRequest);
    if (recoveryResult.Success)
    {
        Console.WriteLine($"Session recovered using strategy: {recoveryResult.StrategyUsed}");
    }
}
```

### JavaScriptInteractionManager

**Namespace:** `ChromeConnect.Services`  
**Purpose:** Handles JavaScript-heavy page interactions and dynamic content.

#### Key Methods
```csharp
/// <summary>
/// Executes JavaScript code with specified parameters.
/// </summary>
public async Task<JavaScriptOperationResult> ExecuteScriptAsync(
    IWebDriver driver,
    JavaScriptExecutionRequest request,
    CancellationToken cancellationToken = default)

/// <summary>
/// Waits for dynamic content based on the specified strategy.
/// </summary>
public async Task<DynamicContentWaitResult> WaitForDynamicContentAsync(
    IWebDriver driver,
    DynamicContentWaitRequest request,
    CancellationToken cancellationToken = default)

/// <summary>
/// Simulates user events using JavaScript.
/// </summary>
public async Task<EventSimulationResult> SimulateEventAsync(
    IWebDriver driver,
    EventSimulationRequest request,
    CancellationToken cancellationToken = default)
```

#### Events
```csharp
/// <summary>
/// Event fired when JavaScript is executed.
/// </summary>
public event EventHandler<JavaScriptExecutionEventArgs>? JavaScriptExecuted;

/// <summary>
/// Event fired when a JavaScript error is captured.
/// </summary>
public event EventHandler<JavaScriptErrorEventArgs>? JavaScriptErrorCaptured;

/// <summary>
/// Event fired when performance metrics are collected.
/// </summary>
public event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsCollected;
```

#### Usage Example
```csharp
var jsManager = new JavaScriptInteractionManager(logger, timeoutManager, configuration);

// Execute custom JavaScript
var jsRequest = new JavaScriptExecutionRequest
{
    Script = "return document.readyState;",
    ReturnResult = true,
    TimeoutSeconds = 5,
    Description = "Check document ready state"
};

var jsResult = await jsManager.ExecuteScriptAsync(driver, jsRequest);
if (jsResult.Success)
{
    Console.WriteLine($"Document state: {jsResult.ReturnValue}");
}

// Wait for Angular application to load
var waitRequest = new DynamicContentWaitRequest
{
    Strategy = WaitStrategy.AngularReady,
    TimeoutSeconds = 30,
    Description = "Wait for Angular app initialization"
};

var waitResult = await jsManager.WaitForDynamicContentAsync(driver, waitRequest);
if (waitResult.Success)
{
    Console.WriteLine("Angular application is ready");
}

// Simulate a custom event
var eventRequest = new EventSimulationRequest
{
    EventType = EventType.Click,
    ElementSelector = "#submit-button",
    UseNativeEvents = true,
    DelayMs = 100
};

var eventResult = await jsManager.SimulateEventAsync(driver, eventRequest);
if (eventResult.Success)
{
    Console.WriteLine("Click event simulated successfully");
}
```

### DetectionMetricsService

**Namespace:** `ChromeConnect.Services`  
**Purpose:** Tracks detection method performance and provides analytics for optimization.

#### Key Methods
```csharp
/// <summary>
/// Records the start of a detection attempt for tracking purposes.
/// </summary>
public string StartDetectionAttempt(string url, DetectionMethod method)

/// <summary>
/// Records the completion of a detection attempt.
/// </summary>
public void CompleteDetectionAttempt(
    string attemptId, 
    bool success, 
    LoginFormElements? elements, 
    int confidence)

/// <summary>
/// Gets the success rate for a specific detection method.
/// </summary>
public double GetSuccessRate(DetectionMethod method, int sinceDays = 7)

/// <summary>
/// Gets comprehensive analytics about detection performance.
/// </summary>
public DetectionAnalytics GetDetectionAnalytics(int sinceDays = 7)

/// <summary>
/// Gets the recommended detection method based on historical performance.
/// </summary>
public MethodRecommendation GetRecommendedMethod(string url)
```

#### Usage Example
```csharp
var metricsService = new DetectionMetricsService(logger);

// Start tracking a detection attempt
string attemptId = metricsService.StartDetectionAttempt(
    url: "https://portal.company.com/login",
    method: DetectionMethod.UrlSpecific);

try
{
    // Perform detection logic...
    var elements = await loginDetector.DetectLoginFormAsync(driver, url);
    
    // Record successful completion
    metricsService.CompleteDetectionAttempt(
        attemptId: attemptId,
        success: elements != null,
        elements: elements,
        confidence: 85);
}
catch (Exception ex)
{
    // Record failed attempt
    metricsService.CompleteDetectionAttempt(
        attemptId: attemptId,
        success: false,
        elements: null,
        confidence: 0);
}

// Get analytics
var analytics = metricsService.GetDetectionAnalytics(sinceDays: 30);
Console.WriteLine($"Overall success rate: {analytics.OverallSuccessRate:F2}%");
Console.WriteLine($"Average detection time: {analytics.AverageDetectionTime:F2}ms");

foreach (var kvp in analytics.MethodPerformance)
{
    var method = kvp.Key;
    var performance = kvp.Value;
    Console.WriteLine($"{method}: {performance.SuccessRate:F2}% success, " +
                     $"{performance.AverageConfidence:F2} avg confidence");
}

// Get recommendation for a specific URL
var recommendation = metricsService.GetRecommendedMethod("https://new-portal.company.com/auth");
Console.WriteLine($"Recommended method: {recommendation.Method}");
Console.WriteLine($"Confidence: {recommendation.Confidence:F2}%");
Console.WriteLine($"Reasoning: {recommendation.Reasoning}");
```

### PopupAndIFrameHandler

**Namespace:** `ChromeConnect.Services`  
**Purpose:** Handles popup windows and iFrames during login processes.

#### Key Methods
```csharp
/// <summary>
/// Detects and handles popup windows that may appear during login.
/// </summary>
public async Task<PopupDetectionResult> DetectAndHandlePopupsAsync(
    IWebDriver driver,
    PopupDetectionRequest request,
    CancellationToken cancellationToken = default)

/// <summary>
/// Detects and switches to relevant iFrames.
/// </summary>
public async Task<IFrameDetectionResult> DetectAndSwitchToIFrameAsync(
    IWebDriver driver,
    IFrameDetectionRequest request,
    CancellationToken cancellationToken = default)

/// <summary>
/// Performs context detection based on specified criteria.
/// </summary>
public async Task<ContextDetectionResult> DetectContextAsync(
    IWebDriver driver,
    ContextDetectionRequest request,
    CancellationToken cancellationToken = default)
```

#### Events
```csharp
/// <summary>
/// Event fired when a new context (popup or iFrame) is detected.
/// </summary>
public event EventHandler<ContextDetectionEventArgs>? ContextDetected;

/// <summary>
/// Event fired when switching to a different context.
/// </summary>
public event EventHandler<ContextSwitchEventArgs>? ContextSwitched;
```

#### Usage Example
```csharp
var popupHandler = new PopupAndIFrameHandler(logger, timeoutManager, configuration);

// Handle popups that might appear
var popupRequest = new PopupDetectionRequest
{
    ExpectedTitlePatterns = new[] { ".*Login.*", ".*Authentication.*" },
    AutoHandle = true,
    TimeoutSeconds = 10
};

var popupResult = await popupHandler.DetectAndHandlePopupsAsync(driver, popupRequest);
if (popupResult.PopupsDetected.Any())
{
    Console.WriteLine($"Handled {popupResult.PopupsDetected.Count} popups");
}

// Detect and switch to login iFrame
var iFrameRequest = new IFrameDetectionRequest
{
    SearchStrategy = IFrameSearchStrategy.TitleContent,
    ExpectedTitlePatterns = new[] { ".*login.*", ".*auth.*" },
    AutoSwitch = true,
    TimeoutSeconds = 15
};

var iFrameResult = await popupHandler.DetectAndSwitchToIFrameAsync(driver, iFrameRequest);
if (iFrameResult.Success && iFrameResult.SwitchedToIFrame)
{
    Console.WriteLine($"Switched to iFrame: {iFrameResult.IFrameId}");
    
    // Perform login operations in iFrame context...
    
    // Switch back to main content when done
    driver.SwitchTo().DefaultContent();
}
```

---

## 📊 Data Models

### CommandLineOptions

**Purpose:** Represents command-line arguments and configuration options.

```csharp
public class CommandLineOptions
{
    [Option("USR", Required = true, HelpText = "Username for login form")]
    public string Username { get; set; } = string.Empty;

    [Option("PSW", Required = true, HelpText = "Password (masked in logs)")]
    public string Password { get; set; } = string.Empty;

    [Option("URL", Required = true, HelpText = "Target login page")]
    public string Url { get; set; } = string.Empty;

    [Option("DOM", Required = true, HelpText = "Domain or tenant identifier")]
    public string Domain { get; set; } = string.Empty;

    [Option("INCOGNITO", Required = true, HelpText = "Use incognito mode (yes/no)")]
    public bool Incognito { get; set; } = false;

    [Option("KIOSK", Required = true, HelpText = "Use kiosk mode (yes/no)")]
    public bool Kiosk { get; set; } = false;

    [Option("CERT", Required = true, HelpText = "Certificate handling (ignore/enforce)")]
    public bool IgnoreCertErrors { get; set; } = false;

    [Option("version", HelpText = "Display version information")]
    public bool ShowVersion { get; set; } = false;

    [Option("debug", HelpText = "Enable debug logging")]
    public bool Debug { get; set; } = false;
}
```

### LoginFormElements

**Purpose:** Represents detected login form elements with their properties.

```csharp
public class LoginFormElements
{
    public IWebElement? UsernameField { get; set; }
    public IWebElement? PasswordField { get; set; }
    public IWebElement? DomainField { get; set; }
    public IWebElement? SubmitButton { get; set; }
    public IWebElement? Form { get; set; }
    
    public string DetectionMethod { get; set; } = string.Empty;
    public int ConfidenceScore { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.Now;
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets the count of detected elements.
    /// </summary>
    public int GetElementCount()
    {
        int count = 0;
        if (UsernameField != null) count++;
        if (PasswordField != null) count++;
        if (DomainField != null) count++;
        if (SubmitButton != null) count++;
        return count;
    }

    /// <summary>
    /// Validates that minimum required elements are present.
    /// </summary>
    public bool IsValid => UsernameField != null && PasswordField != null;
}
```

### SessionData

**Purpose:** Represents session information and state.

```csharp
public class SessionData
{
    public string SessionId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ExpiresAt { get; set; }
    public DateTime LastValidatedAt { get; set; } = DateTime.Now;
    public SessionState State { get; set; } = SessionState.Unknown;
    public string? UserId { get; set; }
    
    public Dictionary<string, object> Attributes { get; set; } = new();
    public Dictionary<string, string> Tokens { get; set; } = new();
    public List<SessionCookie> Cookies { get; set; } = new();
    
    public bool IsEncrypted { get; set; }
    public SessionStorageType StorageType { get; set; }
    
    /// <summary>
    /// Checks if the session is expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTime.Now > ExpiresAt.Value;
    
    /// <summary>
    /// Gets the time remaining until expiry.
    /// </summary>
    public TimeSpan? TimeUntilExpiry => ExpiresAt?.Subtract(DateTime.Now);
}
```

### Detection Enums

```csharp
/// <summary>
/// Available detection methods for login forms.
/// </summary>
public enum DetectionMethod
{
    UrlSpecific,        // URL-specific configuration
    CommonAttributes,   // Common HTML attributes
    XPath,             // XPath expressions
    ShadowDOM,         // Shadow DOM traversal
    JavaScript,        // JavaScript-based detection
    MachineLearning    // ML-based detection (future)
}

/// <summary>
/// Session states for tracking session lifecycle.
/// </summary>
public enum SessionState
{
    Unknown,
    Active,
    Expired,
    Invalid,
    Refreshing,
    Recovering,
    Terminated
}

/// <summary>
/// Wait strategies for dynamic content.
/// </summary>
public enum WaitStrategy
{
    DomReady,           // Wait for DOM to be ready
    NetworkIdle,        // Wait for network requests to complete
    ElementPresence,    // Wait for specific element
    ElementClickable,   // Wait for element to be clickable
    AjaxComplete,       // Wait for AJAX requests
    CustomCondition,    // Wait for custom JavaScript condition
    PerformanceStable,  // Wait for performance metrics
    AngularReady,       // Wait for Angular framework
    ReactReady          // Wait for React framework
}
```

---

## ⚙️ Configuration System

### AppSettings

**Purpose:** Main application configuration loaded from `appsettings.json`.

```csharp
public class AppSettings
{
    public BrowserSettings Browser { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public ErrorHandlingSettings ErrorHandling { get; set; } = new();
}

public class BrowserSettings
{
    public string ChromeDriverPath { get; set; } = string.Empty;
    public bool UseHeadless { get; set; } = false;
    public string[] AdditionalArguments { get; set; } = Array.Empty<string>();
    public int PageLoadTimeoutSeconds { get; set; } = 30;
    public int ElementWaitTimeSeconds { get; set; } = 10;
}

public class ErrorHandlingSettings
{
    public string ScreenshotDirectory { get; set; } = "screenshots";
    public bool CaptureScreenshotsOnError { get; set; } = true;
    public bool CloseBrowserOnError { get; set; } = true;
    public bool EnableRetry { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
    public int InitialRetryDelayMs { get; set; } = 1000;
    public int MaxRetryDelayMs { get; set; } = 30000;
    public double BackoffMultiplier { get; set; } = 2.0;
    public bool AddJitter { get; set; } = true;
}
```

### Configuration Example

**appsettings.json:**
```json
{
  "ChromeConnect": {
    "Browser": {
      "ChromeDriverPath": "",
      "UseHeadless": false,
      "AdditionalArguments": ["--disable-extensions", "--no-sandbox"],
      "PageLoadTimeoutSeconds": 30,
      "ElementWaitTimeSeconds": 10
    },
    "Logging": {
      "LogDirectory": "logs",
      "MaxFileSizeMb": 10,
      "RetainedFileCount": 5,
      "LogSensitiveInfo": false
    },
    "ErrorHandling": {
      "ScreenshotDirectory": "screenshots",
      "CaptureScreenshotsOnError": true,
      "CloseBrowserOnError": true,
      "EnableRetry": true,
      "MaxRetryAttempts": 3,
      "InitialRetryDelayMs": 1000,
      "MaxRetryDelayMs": 30000,
      "BackoffMultiplier": 2.0,
      "AddJitter": true
    }
  }
}
```

### URL-Specific Configuration

**Purpose:** Configure detection strategies for specific login pages.

```csharp
public class LoginPageConfiguration
{
    public string UrlPattern { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
    public string DisplayName { get; set; } = string.Empty;
    
    // Element selectors
    public List<string> UsernameSelectors { get; set; } = new();
    public List<string> PasswordSelectors { get; set; } = new();
    public List<string> DomainSelectors { get; set; } = new();
    public List<string> SubmitButtonSelectors { get; set; } = new();
    
    // Timing configuration
    public int AdditionalWaitMs { get; set; } = 0;
    
    // Verification selectors
    public List<string> SuccessIndicators { get; set; } = new();
    public List<string> FailureIndicators { get; set; } = new();
    
    // Special handling
    public bool RequiresJavaScript { get; set; } = false;
    public string Notes { get; set; } = string.Empty;
}
```

#### Adding Custom URL Configuration
```csharp
// Register custom configuration
services.Configure<List<LoginPageConfiguration>>(config =>
{
    config.Add(new LoginPageConfiguration
    {
        UrlPattern = "https://customportal.company.com/.*",
        Priority = 100,
        DisplayName = "Custom Corporate Portal",
        UsernameSelectors = new List<string>
        {
            "#custom-username",
            "[data-testid='username-input']"
        },
        PasswordSelectors = new List<string>
        {
            "#custom-password",
            "[data-testid='password-input']"
        },
        SubmitButtonSelectors = new List<string>
        {
            "#custom-login-btn",
            "[data-testid='login-button']"
        },
        RequiresJavaScript = true,
        AdditionalWaitMs = 2000,
        Notes = "Requires additional wait for dynamic content loading"
    });
});
```

---

## 📡 Event System

### SessionManager Events

```csharp
// Event argument classes
public class SessionStateChangeEventArgs : EventArgs
{
    public string SessionId { get; set; }
    public SessionState OldState { get; set; }
    public SessionState NewState { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Reason { get; set; }
}

public class SessionValidationEventArgs : EventArgs
{
    public string SessionId { get; set; }
    public SessionValidationResult Result { get; set; }
    public TimeSpan ValidationDuration { get; set; }
    public DateTime Timestamp { get; set; }
}

// Usage example
sessionManager.SessionStateChanged += (sender, args) =>
{
    logger.LogInformation("Session {SessionId} changed from {OldState} to {NewState}. Reason: {Reason}",
        args.SessionId, args.OldState, args.NewState, args.Reason);
    
    // Handle specific state transitions
    switch (args.NewState)
    {
        case SessionState.Expired:
            // Trigger session recovery
            _ = Task.Run(() => RecoverExpiredSession(args.SessionId));
            break;
        case SessionState.Invalid:
            // Clear session data
            ClearSessionData(args.SessionId);
            break;
    }
};
```

### JavaScriptInteractionManager Events

```csharp
public class JavaScriptExecutionEventArgs : EventArgs
{
    public string Script { get; set; }
    public object? Result { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}

public class JavaScriptErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? SourceLocation { get; set; }
    public DateTime Timestamp { get; set; }
}

// Usage example
jsManager.JavaScriptExecuted += (sender, args) =>
{
    if (!args.Success)
    {
        logger.LogWarning("JavaScript execution failed: {ErrorMessage}. Script: {Script}",
            args.ErrorMessage, args.Script);
    }
    else
    {
        logger.LogDebug("JavaScript executed successfully in {ExecutionTime}ms",
            args.ExecutionTime.TotalMilliseconds);
    }
};

jsManager.JavaScriptErrorCaptured += (sender, args) =>
{
    logger.LogError("JavaScript error captured: {ErrorMessage} at {SourceLocation}",
        args.ErrorMessage, args.SourceLocation);
        
    // Optionally send to error tracking service
    errorTracker.ReportJavaScriptError(args);
};
```

---

## 🔌 Extension Points

### Custom Screenshot Capture

**Interface:** `IScreenshotCapture`

```csharp
public interface IScreenshotCapture
{
    /// <summary>
    /// Captures a screenshot of the current browser state.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture from.</param>
    /// <param name="prefix">A prefix for the screenshot filename.</param>
    /// <returns>The path to the saved screenshot, or null if capture failed.</returns>
    string CaptureScreenshot(IWebDriver driver, string prefix);
}

// Custom implementation example
public class CustomScreenshotCapture : IScreenshotCapture
{
    private readonly ILogger<CustomScreenshotCapture> _logger;
    private readonly string _screenshotDirectory;
    
    public CustomScreenshotCapture(ILogger<CustomScreenshotCapture> logger, string screenshotDirectory)
    {
        _logger = logger;
        _screenshotDirectory = screenshotDirectory;
    }
    
    public string CaptureScreenshot(IWebDriver driver, string prefix)
    {
        try
        {
            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            var fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            var fullPath = Path.Combine(_screenshotDirectory, fileName);
            
            Directory.CreateDirectory(_screenshotDirectory);
            screenshot.SaveAsFile(fullPath);
            
            // Custom processing - upload to cloud storage, apply watermarks, etc.
            ProcessScreenshot(fullPath);
            
            _logger.LogInformation("Screenshot captured: {FileName}", fileName);
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot with prefix {Prefix}", prefix);
            return null;
        }
    }
    
    private void ProcessScreenshot(string filePath)
    {
        // Custom screenshot processing logic
        // Example: Upload to cloud storage, apply compression, add metadata
    }
}

// Register custom implementation
services.AddSingleton<IScreenshotCapture, CustomScreenshotCapture>();
```

### Custom Login Detection Strategy

```csharp
// Extend LoginDetector for custom detection logic
public class CustomLoginDetector : LoginDetector
{
    public CustomLoginDetector(ILogger<LoginDetector> logger) : base(logger) { }
    
    protected override async Task<LoginFormElements?> DetectByCustomStrategy(IWebDriver driver, string url)
    {
        // Implement custom detection logic
        // Example: Machine learning-based detection, API-based configuration, etc.
        
        var elements = new LoginFormElements();
        
        // Custom detection implementation
        elements.UsernameField = await DetectUsernameFieldWithML(driver);
        elements.PasswordField = await DetectPasswordFieldWithML(driver);
        elements.SubmitButton = await DetectSubmitButtonWithML(driver);
        
        if (elements.IsValid)
        {
            elements.DetectionMethod = "CustomML";
            elements.ConfidenceScore = CalculateMLConfidence(elements);
            return elements;
        }
        
        return null;
    }
    
    private async Task<IWebElement?> DetectUsernameFieldWithML(IWebDriver driver)
    {
        // Custom ML-based detection logic
        // Return the most likely username field
        return null;
    }
}
```

### Custom Error Handling

```csharp
public class CustomErrorHandler : ErrorHandler
{
    private readonly IExternalLoggingService _externalLogger;
    
    public CustomErrorHandler(
        ILogger<ErrorHandler> logger, 
        IScreenshotCapture screenshotCapture,
        IExternalLoggingService externalLogger) 
        : base(logger, screenshotCapture)
    {
        _externalLogger = externalLogger;
    }
    
    public override async Task<ErrorHandlingResult> HandleErrorAsync(
        Exception exception, 
        IWebDriver? driver, 
        string context, 
        ErrorHandlerSettings? settings = null)
    {
        // Call base implementation first
        var baseResult = await base.HandleErrorAsync(exception, driver, context, settings);
        
        // Add custom error handling
        try
        {
            // Send to external monitoring service
            await _externalLogger.LogErrorAsync(new ErrorReport
            {
                Exception = exception,
                Context = context,
                Timestamp = DateTime.UtcNow,
                ScreenshotPath = baseResult.ScreenshotPath,
                UserAgent = driver?.ExecuteScript("return navigator.userAgent") as string,
                PageUrl = driver?.Url,
                PageTitle = driver?.Title
            });
            
            // Send alert for critical errors
            if (IsCriticalError(exception))
            {
                await SendCriticalErrorAlert(exception, context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send error to external service");
        }
        
        return baseResult;
    }
    
    private bool IsCriticalError(Exception exception)
    {
        return exception is BrowserLaunchException || 
               exception is ChromeDriverException ||
               exception.Message.Contains("Chrome crashed");
    }
    
    private async Task SendCriticalErrorAlert(Exception exception, string context)
    {
        // Implementation for critical error alerts
        // Example: Slack notification, email alert, PagerDuty incident
    }
}
```

---

## 🛠️ Integration Examples

### ASP.NET Core Integration

```csharp
// Startup.cs or Program.cs
public void ConfigureServices(IServiceCollection services)
{
    // Add ChromeConnect services
    services.AddChromeConnectServices(options =>
    {
        options.DefaultTimeout = TimeSpan.FromSeconds(30);
        options.EnableMetrics = true;
        options.EnableEventLogging = true;
    });
    
    // Add configuration
    services.AddChromeConnectConfiguration(Configuration);
    
    // Add custom implementations
    services.AddSingleton<IScreenshotCapture, CloudScreenshotCapture>();
    services.AddScoped<ICustomAuthenticationService, CustomAuthenticationService>();
}

// Controller example
[ApiController]
[Route("api/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly ChromeConnectService _chromeConnectService;
    private readonly ILogger<AuthenticationController> _logger;
    
    public AuthenticationController(
        ChromeConnectService chromeConnectService,
        ILogger<AuthenticationController> logger)
    {
        _chromeConnectService = chromeConnectService;
        _logger = logger;
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> PerformLogin([FromBody] LoginRequest request)
    {
        try
        {
            var options = new CommandLineOptions
            {
                Username = request.Username,
                Password = request.Password,
                Url = request.LoginUrl,
                Domain = request.Domain,
                Incognito = true,
                IgnoreCertErrors = request.IgnoreCertErrors
            };
            
            var exitCode = await _chromeConnectService.ExecuteAsync(options);
            
            return exitCode switch
            {
                0 => Ok(new { Success = true, Message = "Login successful" }),
                1 => BadRequest(new { Success = false, Message = "Login failed" }),
                _ => StatusCode(500, new { Success = false, Message = "Application error" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }
}
```

### Background Service Integration

```csharp
public class ScheduledAuthenticationService : BackgroundService
{
    private readonly ChromeConnectService _chromeConnectService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScheduledAuthenticationService> _logger;
    
    public ScheduledAuthenticationService(
        ChromeConnectService chromeConnectService,
        IConfiguration configuration,
        ILogger<ScheduledAuthenticationService> logger)
    {
        _chromeConnectService = chromeConnectService;
        _configuration = configuration;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Perform scheduled authentication tasks
                await PerformScheduledAuthentications();
                
                // Wait for next execution interval
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled authentication service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
    
    private async Task PerformScheduledAuthentications()
    {
        var authTasks = _configuration.GetSection("ScheduledAuthentications")
            .Get<List<ScheduledAuthenticationTask>>();
        
        foreach (var task in authTasks)
        {
            if (task.ShouldExecuteNow())
            {
                await ExecuteAuthenticationTask(task);
            }
        }
    }
    
    private async Task ExecuteAuthenticationTask(ScheduledAuthenticationTask task)
    {
        var options = new CommandLineOptions
        {
            Username = task.Username,
            Password = await GetEncryptedPassword(task.PasswordKey),
            Url = task.LoginUrl,
            Domain = task.Domain,
            Incognito = true
        };
        
        var exitCode = await _chromeConnectService.ExecuteAsync(options);
        
        _logger.LogInformation("Scheduled authentication for {TaskName} completed with exit code {ExitCode}",
            task.Name, exitCode);
    }
}
```

### Testing Integration

```csharp
[TestClass]
public class ChromeConnectIntegrationTests
{
    private IServiceProvider _serviceProvider;
    private ChromeConnectService _chromeConnectService;
    
    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        
        // Add test logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // Add ChromeConnect services with test configuration
        services.AddChromeConnectServices();
        
        // Override with test implementations
        services.AddSingleton<IScreenshotCapture, TestScreenshotCapture>();
        
        _serviceProvider = services.BuildServiceProvider();
        _chromeConnectService = _serviceProvider.GetRequiredService<ChromeConnectService>();
    }
    
    [TestMethod]
    public async Task TestSuccessfulLogin()
    {
        // Arrange
        var options = new CommandLineOptions
        {
            Username = "test@example.com",
            Password = "testpassword",
            Url = "https://httpbin.org/forms/post",  // Test form
            Domain = "TEST",
            Incognito = true,
            IgnoreCertErrors = true,
            Debug = true
        };
        
        // Act
        var exitCode = await _chromeConnectService.ExecuteAsync(options);
        
        // Assert
        Assert.AreEqual(0, exitCode, "Login should succeed");
    }
    
    [TestMethod]
    public async Task TestInvalidCredentials()
    {
        // Test with invalid credentials
        var options = new CommandLineOptions
        {
            Username = "invalid@example.com",
            Password = "wrongpassword",
            Url = "https://httpbin.org/forms/post",
            Domain = "TEST",
            Incognito = true
        };
        
        var exitCode = await _chromeConnectService.ExecuteAsync(options);
        
        Assert.AreEqual(1, exitCode, "Login should fail with invalid credentials");
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }
}

// Test screenshot capture implementation
public class TestScreenshotCapture : IScreenshotCapture
{
    public string CaptureScreenshot(IWebDriver driver, string prefix)
    {
        // For testing, just return a mock path
        return $"test-screenshots/{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
    }
}
```

---

## 💡 Best Practices

### Service Lifetime Management

```csharp
// Recommended service lifetimes
services.AddSingleton<BrowserManager>();           // Expensive to create
services.AddSingleton<LoginDetector>();           // Stateless, can be shared
services.AddSingleton<DetectionMetricsService>(); // Maintains metrics state
services.AddScoped<SessionManager>();             // Per-request state
services.AddTransient<ChromeConnectService>();    // Lightweight orchestrator
```

### Error Handling and Logging

```csharp
// Implement structured logging
public class StructuredLoggingExample
{
    private readonly ILogger<StructuredLoggingExample> _logger;
    
    public async Task<int> ExecuteWithStructuredLogging(CommandLineOptions options)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Username"] = options.Username,
            ["Domain"] = options.Domain,
            ["Url"] = options.Url,
            ["Incognito"] = options.Incognito,
            ["SessionId"] = Guid.NewGuid().ToString()
        });
        
        try
        {
            _logger.LogInformation("Starting authentication process for {Username} at {Url}",
                options.Username, options.Url);
            
            var result = await ExecuteAuthentication(options);
            
            _logger.LogInformation("Authentication completed with exit code {ExitCode}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication process failed");
            throw;
        }
    }
}
```

### Performance Optimization

```csharp
// Configure timeouts appropriately
services.Configure<TimeoutSettings>(settings =>
{
    settings.DefaultTimeoutMs = 30000;        // 30 seconds default
    settings.ElementTimeoutMs = 10000;        // 10 seconds for elements
    settings.ConditionTimeoutMs = 15000;      // 15 seconds for conditions
    settings.UrlChangeTimeoutMs = 5000;       // 5 seconds for navigation
});

// Use connection pooling for browser instances (advanced)
services.AddSingleton<IBrowserPool, BrowserPool>();

// Implement caching for detection configurations
services.AddMemoryCache();
services.Decorate<LoginDetector, CachedLoginDetector>();
```

### Security Considerations

```csharp
// Secure credential handling
public class SecureCredentialManager
{
    private readonly IDataProtectionProvider _dataProtection;
    
    public SecureCredentialManager(IDataProtectionProvider dataProtection)
    {
        _dataProtection = dataProtection;
    }
    
    public string ProtectCredential(string credential)
    {
        var protector = _dataProtection.CreateProtector("ChromeConnect.Credentials");
        return protector.Protect(credential);
    }
    
    public string UnprotectCredential(string protectedCredential)
    {
        var protector = _dataProtection.CreateProtector("ChromeConnect.Credentials");
        return protector.Unprotect(protectedCredential);
    }
}

// Register data protection
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@".\keys"))
    .SetApplicationName("ChromeConnect");
```

---

## 🔄 Migration Guide

### From Command-Line to API

**Before (Command-line):**
```bash
ChromeConnect.exe --USR user@example.com --PSW password123 --URL https://portal.com --DOM CORPORATE --INCOGNITO yes --KIOSK no --CERT ignore
```

**After (API):**
```csharp
var chromeConnectService = serviceProvider.GetRequiredService<ChromeConnectService>();

var options = new CommandLineOptions
{
    Username = "user@example.com",
    Password = "password123",
    Url = "https://portal.com",
    Domain = "CORPORATE",
    Incognito = true,
    Kiosk = false,
    IgnoreCertErrors = true
};

int exitCode = await chromeConnectService.ExecuteAsync(options);
```

### Upgrading from v1.0 to v1.1 (Future)

```csharp
// v1.0 usage
var service = new ChromeConnectService(/* manual dependencies */);

// v1.1 usage with enhanced DI support
services.AddChromeConnectServices(options =>
{
    options.EnableAdvancedDetection = true;
    options.EnableMetrics = true;
    options.DefaultTimeout = TimeSpan.FromSeconds(45);
});
```

### Breaking Changes Checklist

1. **Interface Changes**: Check for any modified interface signatures
2. **Configuration**: Update `appsettings.json` with new configuration options
3. **Dependencies**: Update NuGet package references
4. **Events**: Review event argument classes for any new properties
5. **Error Handling**: Check for new exception types

---

## 📞 Support and Resources

### API Reference Links
- **Main Documentation**: [README.md](../README.md)
- **Command-line Reference**: [command-line-reference.md](command-line-reference.md)
- **Architecture Overview**: [architecture.md](architecture.md)
- **Usage Examples**: [usage-examples.md](usage-examples.md)
- **Troubleshooting**: [configuration-troubleshooting.md](configuration-troubleshooting.md)

### Code Examples Repository
All code examples from this documentation are available in the `examples/` directory:
- **Basic Integration**: `examples/basic-integration/`
- **Advanced Scenarios**: `examples/advanced-scenarios/`
- **Custom Extensions**: `examples/custom-extensions/`
- **Testing Examples**: `examples/testing/`

### Community Resources
- **GitHub Discussions**: Share integration patterns and ask questions
- **Sample Applications**: Reference implementations for common scenarios
- **API Updates**: Subscribe to API change notifications

---

*This API documentation is maintained alongside the codebase and updated with each release. For the most current information, always refer to the latest version in the repository.* 