# WebConnect Login Implementation Guide

This guide provides technical implementation details and code examples for working with the WebConnect login detection and automation system.

## Architecture Overview

The WebConnect login system uses a modular architecture with clear separation of concerns:

```
WebConnectService
├── BrowserManager (Chrome lifecycle)
├── LoginDetector (Form detection)
├── CredentialManager (Credential entry)
├── LoginVerifier (Success/failure verification)
├── ErrorHandler (Error handling & retry logic)
├── TimeoutManager (Timeout management)
└── ErrorMonitor (Error tracking)
```

## Configuration System

### URL-Specific Configuration

The `LoginPageConfiguration` model allows customization for specific login pages:

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

### Adding New URL Configurations

To add configuration for a new login page, modify the `InitializeConfigurations()` method:

```csharp
private static void InitializeConfigurations()
{
    // Example: Configuration for a specific application
    _configurations.Add(new LoginPageConfiguration
    {
        UrlPattern = "myapp.company.com/login",
        Priority = 20,
        DisplayName = "MyApp Login",
        UsernameSelectors = new List<string>
        {
            "input[name='email']",
            "input[id='email-field']",
            "#login-email"
        },
        PasswordSelectors = new List<string>
        {
            "input[name='password']",
            "input[id='password-field']",
            "#login-password"
        },
        SubmitButtonSelectors = new List<string>
        {
            "button[type='submit']",
            "#login-button",
            ".submit-btn"
        },
        SuccessIndicators = new List<string>
        {
            ".dashboard-container",
            "a[href*='logout']",
            ".user-menu"
        },
        FailureIndicators = new List<string>
        {
            ".error-message",
            ".alert-danger",
            "#login-error"
        },
        AdditionalWaitMs = 3000,
        RequiresJavaScript = true,
        Notes = "Requires additional wait for AJAX form validation"
    });
}
```

## Detection Strategies

### Three-Tier Detection System

The system uses three detection strategies in order of priority:

#### Tier 1: URL-Specific Configuration
```csharp
private LoginFormElements DetectByConfiguration(IWebDriver driver, LoginPageConfiguration config)
{
    var loginForm = new LoginFormElements();
    
    // Try each username selector in order
    foreach (var selector in config.UsernameSelectors)
    {
        try
        {
            var elements = driver.FindElements(By.CssSelector(selector));
            if (elements.Count > 0 && elements[0].Displayed)
            {
                loginForm.UsernameField = elements[0];
                _logger.LogDebug($"Username field found using selector: {selector}");
                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Selector {selector} failed: {ex.Message}");
        }
    }
    
    // Similar logic for password, domain, and submit button...
    return loginForm;
}
```

#### Tier 2: Common Attributes Detection
```csharp
private LoginFormElements DetectByCommonAttributes(IWebDriver driver)
{
    // Standard selectors that work across most forms
    string[] usernameSelectors = {
        "input[type='text'][id*='user' i]",
        "input[type='text'][name*='user' i]",
        "input[type='email']",
        "input[id*='email' i]"
    };
    
    // Find first matching element
    foreach (var selector in usernameSelectors)
    {
        var elements = driver.FindElements(By.CssSelector(selector));
        if (elements.Count > 0 && elements[0].Displayed)
        {
            return elements[0];
        }
    }
    
    return null;
}
```

#### Tier 3: XPath Fallback Detection
```csharp
private LoginFormElements DetectByXPath(IWebDriver driver)
{
    // XPath expressions for complex form structures
    string[] usernameXPaths = {
        "//input[contains(translate(@id, 'USERNAME', 'username'), 'username')]",
        "//label[contains(translate(., 'USERNAME', 'username'), 'username')]/following::input[1]",
        "//label[contains(translate(., 'USERNAME', 'username'), 'username')]/..//input"
    };
    
    // Try each XPath expression
    foreach (var xpath in usernameXPaths)
    {
        try
        {
            var elements = driver.FindElements(By.XPath(xpath));
            if (elements.Count > 0 && elements[0].Displayed)
            {
                return elements[0];
            }
        }
        catch { /* Continue to next XPath */ }
    }
    
    return null;
}
```

## Service Implementation Examples

### Basic Usage

```csharp
// Create service with dependencies
var logger = loggerFactory.CreateLogger<WebConnectService>();
var browserManager = new BrowserManager(logger);
var loginDetector = new LoginDetector(logger);
var credentialManager = new CredentialManager(logger);
var loginVerifier = new LoginVerifier(logger);
var screenshotCapture = new ScreenshotCapture();
var errorHandler = new ErrorHandler(logger, screenshotCapture);
var timeoutManager = new TimeoutManager(logger);
var errorMonitor = new ErrorMonitor(logger);

var service = new WebConnectService(
    logger,
    browserManager,
    loginDetector,
    credentialManager,
    loginVerifier,
    screenshotCapture,
    errorHandler,
    timeoutManager,
    errorMonitor);

// Execute login
var options = new CommandLineOptions
{
    Url = "https://example.com/login",
    Username = "user@example.com",
    Password = "password123",
    Domain = "company",
    IgnoreCertErrors = true
};

int exitCode = await service.ExecuteAsync(options);
```

### Custom Error Handler Configuration

```csharp
var errorHandlerSettings = new ErrorHandlerSettings
{
    CaptureScreenshots = true,
    CloseDriverOnError = true,
    DefaultRetryCount = 3,
    DefaultRetryDelayMs = 1000,
    MaxRetryDelayMs = 30000,
    BackoffMultiplier = 2.0,
    AddJitter = true,
    OnError = async (exception) => {
        // Custom error handling logic
        await LogErrorToDatabase(exception);
        await SendErrorNotification(exception);
    }
};

var errorHandler = new ErrorHandler(logger, screenshotCapture, errorHandlerSettings);
```

### Timeout Configuration

```csharp
var timeoutSettings = new TimeoutSettings
{
    DefaultTimeoutMs = 30000,
    ElementTimeoutMs = 10000,
    ConditionTimeoutMs = 15000,
    UrlChangeTimeoutMs = 5000
};

var timeoutManager = new TimeoutManager(logger, timeoutSettings);
```

## Testing Implementation

### MSTest Parameterized Tests

```csharp
[TestClass]
public class LoginTests
{
    private WebConnectService _webConnectService;
    
    [TestInitialize]
    public void SetUp()
    {
        // Initialize service with test configuration
        _webConnectService = CreateTestService();
    }
    
    private static IEnumerable<object[]> LoginTestData =>
        new List<object[]>
        {
            new object[] { "https://app1.test.com/login", "testuser", "password", "domain", true },
            new object[] { "https://app1.test.com/login", "testuser", "wrongpass", "domain", false },
            new object[] { "https://app2.test.com/signin", "testuser", "password", null, true },
        };

    [DataTestMethod]
    [DynamicData(nameof(LoginTestData))]
    public async Task PerformLoginTest(string url, string username, string password, string domain, bool expectSuccess)
    {
        var options = new CommandLineOptions
        {
            Url = url,
            Username = username,
            Password = password,
            Domain = domain ?? string.Empty,
            IgnoreCertErrors = true
        };

        int exitCode = await _webConnectService.ExecuteAsync(options);

        if (expectSuccess)
        {
            Assert.AreEqual(0, exitCode, $"Expected successful login for {url}");
        }
        else
        {
            Assert.AreEqual(1, exitCode, $"Expected failed login for {url}");
        }
    }
}
```

### Mock Service Testing

```csharp
[TestClass]
public class LoginDetectorTests
{
    private Mock<ILogger<LoginDetector>> _mockLogger;
    private LoginDetector _loginDetector;
    private Mock<IWebDriver> _mockDriver;

    [TestInitialize]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<LoginDetector>>();
        _loginDetector = new LoginDetector(_mockLogger.Object);
        _mockDriver = new Mock<IWebDriver>();
    }

    [TestMethod]
    public async Task DetectLoginFormAsync_FindsElements_WhenStandardFormPresent()
    {
        // Arrange
        var mockUsernameElement = new Mock<IWebElement>();
        var mockPasswordElement = new Mock<IWebElement>();
        
        mockUsernameElement.Setup(e => e.Displayed).Returns(true);
        mockPasswordElement.Setup(e => e.Displayed).Returns(true);
        
        _mockDriver.Setup(d => d.FindElements(It.IsAny<By>()))
            .Returns(new List<IWebElement> { mockUsernameElement.Object }.AsReadOnly());

        // Act
        var result = await _loginDetector.DetectLoginFormAsync(_mockDriver.Object);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.UsernameField);
    }
}
```

## Debugging and Troubleshooting

### Enhanced Logging

The system includes comprehensive logging for debugging form detection issues:

```csharp
private void LogPageInformation(IWebDriver driver)
{
    _logger.LogInformation($"Current URL: {driver.Url}");
    _logger.LogInformation($"Page title: {driver.Title}");
    
    var inputElements = driver.FindElements(By.TagName("input"));
    _logger.LogInformation($"Total input elements: {inputElements.Count}");
    
    var passwordFields = driver.FindElements(By.CssSelector("input[type='password']"));
    _logger.LogInformation($"Password fields: {passwordFields.Count}");
}

private async Task LogDetailedDOMInformation(IWebDriver driver)
{
    var inputElements = driver.FindElements(By.TagName("input"));
    
    _logger.LogDebug("=== Detailed Input Elements Analysis ===");
    for (int i = 0; i < Math.Min(inputElements.Count, 10); i++)
    {
        var element = inputElements[i];
        _logger.LogDebug($"Input {i + 1}:");
        _logger.LogDebug($"  Type: {GetElementAttribute(element, "type")}");
        _logger.LogDebug($"  ID: {GetElementAttribute(element, "id")}");
        _logger.LogDebug($"  Name: {GetElementAttribute(element, "name")}");
        _logger.LogDebug($"  Placeholder: {GetElementAttribute(element, "placeholder")}");
        _logger.LogDebug($"  Class: {GetElementAttribute(element, "class")}");
        _logger.LogDebug($"  Visible: {element.Displayed}");
    }
}
```

### Screenshot Capture

Screenshots are automatically captured on failures:

```csharp
public string CaptureScreenshot(IWebDriver driver, string prefix)
{
    try
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"{prefix}_{timestamp}.png";
        var filepath = Path.Combine("screenshots", filename);
        
        Directory.CreateDirectory(Path.GetDirectoryName(filepath));
        
        var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
        screenshot.SaveAsFile(filepath, ScreenshotImageFormat.Png);
        
        _logger.LogInformation($"Screenshot saved: {filepath}");
        return filepath;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to capture screenshot");
        return null;
    }
}
```

## Performance Optimization

### Selector Performance Tips

1. **Use specific selectors**: Prefer ID and name attributes over generic CSS selectors
2. **Avoid complex XPath**: Use CSS selectors when possible for better performance
3. **Order selectors by likelihood**: Put most common selectors first in configuration

### Wait Strategy Optimization

```csharp
// Efficient explicit wait
var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
var element = wait.Until(d => {
    var elements = d.FindElements(By.CssSelector("input[type='password']"));
    return elements.FirstOrDefault(e => e.Displayed);
});

// Avoid Thread.Sleep - use WebDriverWait instead
await Task.Delay(1000); // Only for specific timing requirements
```

### Resource Management

```csharp
public void Dispose()
{
    try
    {
        _driver?.Quit();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Error closing browser");
    }
    finally
    {
        _driver?.Dispose();
        _driver = null;
    }
}
```

## Security Considerations

### Certificate Handling

```csharp
// For test environments only
ServicePointManager.ServerCertificateValidationCallback = 
    (sender, cert, chain, sslPolicyErrors) => true;
```

### Credential Security

```csharp
// Use SecureString for sensitive data in production
public class SecureCredentialManager
{
    public void EnterPassword(IWebElement passwordField, SecureString password)
    {
        IntPtr passwordPtr = Marshal.SecureStringToGlobalAllocUnicode(password);
        try
        {
            string passwordText = Marshal.PtrToStringUni(passwordPtr);
            passwordField.Clear();
            passwordField.SendKeys(passwordText);
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(passwordPtr);
        }
    }
}
```

## Extension Points

### Custom Login Detectors

```csharp
public class CustomLoginDetector : LoginDetector
{
    public CustomLoginDetector(ILogger<LoginDetector> logger) : base(logger) { }
    
    public override async Task<LoginFormElements?> DetectLoginFormAsync(IWebDriver driver)
    {
        // Custom detection logic for special form types
        var result = await base.DetectLoginFormAsync(driver);
        
        if (result == null && IsSpecialFormType(driver))
        {
            result = DetectSpecialForm(driver);
        }
        
        return result;
    }
    
    private bool IsSpecialFormType(IWebDriver driver)
    {
        // Custom logic to identify special form types
        return driver.PageSource.Contains("custom-form-identifier");
    }
}
```

### Custom Verification Logic

```csharp
public class CustomLoginVerifier : LoginVerifier
{
    public override async Task<bool> VerifyLoginSuccessAsync(IWebDriver driver)
    {
        // Custom verification logic
        if (await CheckCustomSuccessIndicator(driver))
        {
            return true;
        }
        
        // Fall back to base implementation
        return await base.VerifyLoginSuccessAsync(driver);
    }
    
    private async Task<bool> CheckCustomSuccessIndicator(IWebDriver driver)
    {
        // Application-specific success detection
        return driver.FindElements(By.CssSelector(".app-specific-success")).Count > 0;
    }
}
```

This implementation guide provides the technical foundation for extending and customizing the WebConnect login automation system for specific use cases and environments. 