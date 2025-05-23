using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using System.Net;
using ChromeConnect.Core; // For IScreenshotCapture if needed by ErrorHandler
using ChromeConnect.Services;
using ChromeConnect.Models;
using ChromeConnect.Exceptions; // For specific exception checking if needed later
using Moq; // Added for Moq

namespace ChromeConnect.Tests;

[TestClass]
public class LoginTests
{
    private TestContext? _testContextInstance;
    private ILoggerFactory? _loggerFactory;

    // Services to be tested or used by tests
    private ChromeConnectService? _chromeConnectService;
    private Mock<IScreenshotCapture>? _mockScreenshotCapture;
    private AppSettings? _appSettings; // Made nullable

    public TestContext? TestContext
    {
        get { return _testContextInstance; }
        set { _testContextInstance = value; }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        // Ignore SSL certificate errors for all HTTPS requests in this test run
        // This affects HttpClient if used directly. WebDriver config is separate.
        ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
        
        // Basic Logger Setup
        _loggerFactory = LoggerFactory.Create(builder => 
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("ChromeConnect", LogLevel.Debug)
                .AddConsole();
        });

        // Initialize AppSettings
        _appSettings = new AppSettings(); 

        // Mock IScreenshotCapture
        _mockScreenshotCapture = new Mock<IScreenshotCapture>();

        // Instantiate services
        var browserManagerLogger = _loggerFactory.CreateLogger<BrowserManager>();
        var loginDetectorLogger = _loggerFactory.CreateLogger<LoginDetector>();
        var credentialManagerLogger = _loggerFactory.CreateLogger<CredentialManager>();
        var loginVerifierLogger = _loggerFactory.CreateLogger<LoginVerifier>();
        // Removed ScreenshotCapture logger as we use mock IScreenshotCapture
        var errorHandlerLogger = _loggerFactory.CreateLogger<ErrorHandler>();
        var timeoutManagerLogger = _loggerFactory.CreateLogger<TimeoutManager>();
        var errorMonitorLogger = _loggerFactory.CreateLogger<ErrorMonitor>();
        var serviceLogger = _loggerFactory.CreateLogger<ChromeConnectService>();

        // Instantiate with actual settings from AppSettings, mapped to service-specific settings classes
        
        // ErrorHandler specific settings
        var serviceErrorHandlerSettings = new ErrorHandlerSettings
        {
            CaptureScreenshots = _appSettings!.ErrorHandling.CaptureScreenshotsOnError,
            CloseDriverOnError = _appSettings!.ErrorHandling.CloseBrowserOnError,
            DefaultRetryCount = _appSettings!.ErrorHandling.MaxRetryAttempts, 
            DefaultRetryDelayMs = _appSettings!.ErrorHandling.InitialRetryDelayMs, 
            MaxRetryDelayMs = _appSettings!.ErrorHandling.MaxRetryDelayMs,
            BackoffMultiplier = _appSettings!.ErrorHandling.BackoffMultiplier,
            AddJitter = _appSettings!.ErrorHandling.AddJitter
        };
        var errorHandler = new ErrorHandler(errorHandlerLogger, _mockScreenshotCapture.Object, serviceErrorHandlerSettings);
        
        // TimeoutManager specific settings
        var serviceTimeoutSettings = new TimeoutSettings
        {
            DefaultTimeoutMs = _appSettings!.Browser.PageLoadTimeoutSeconds * 1000,
            ElementTimeoutMs = _appSettings!.Browser.ElementWaitTimeSeconds * 1000,
            ConditionTimeoutMs = _appSettings!.Browser.PageLoadTimeoutSeconds * 1000, 
            UrlChangeTimeoutMs = 5000 
        };
        var timeoutManager = new TimeoutManager(timeoutManagerLogger, serviceTimeoutSettings);

        // ErrorMonitor specific settings
        var serviceErrorMonitorSettings = new ErrorMonitorSettings();
        serviceErrorMonitorSettings.TrackRecentErrors = _appSettings!.ErrorHandling.EnableRetry; 
        var errorMonitor = new ErrorMonitor(errorMonitorLogger, serviceErrorMonitorSettings);

        // Attempt simplified constructors for services with unknown definitions
        // These will likely cause build errors if they require more parameters.
        var browserManager = new BrowserManager(browserManagerLogger); 
        var loginDetector = new LoginDetector(loginDetectorLogger); 
        var credentialManager = new CredentialManager(credentialManagerLogger); 
        var loginVerifier = new LoginVerifier(loginVerifierLogger); 
        

        _chromeConnectService = new ChromeConnectService(
            serviceLogger,
            browserManager,
            loginDetector,
            credentialManager,
            loginVerifier,
            _mockScreenshotCapture.Object, 
            errorHandler,
            timeoutManager,
            errorMonitor
        );
    }

    [TestCleanup]
    public void TestCleanup()
    {
        // Reset the SSL callback
        ServicePointManager.ServerCertificateValidationCallback = null; 
        _loggerFactory?.Dispose();
    }

    private static IEnumerable<object[]> LoginTestData =>
        new List<object[]>
        {
            // Format: [string url, string username, string password, string? domain, bool expectSuccess]
            new object[] { "https://10.22.11.2:10001/login.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login2.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login2.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login3.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login3.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login4.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login4.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login5.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login5.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login6.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login6.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login-alt.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login-alt.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login-alt2.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login-alt2.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login-alt3.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login-alt3.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login-alt4.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login-alt4.htm", "testuser", "wrongpassword", "picovina", false },
            new object[] { "https://10.22.11.2:10001/login-alt5.htm", "testuser", "LPKOjihu", "picovina", true },
            new object[] { "https://10.22.11.2:10001/login-alt5.htm", "testuser", "wrongpassword", "picovina", false },
        };

    [DataTestMethod]
    [DynamicData(nameof(LoginTestData))]
    public async Task PerformLoginTest(string url, string username, string password, string? domain, bool expectSuccess)
    {
        Assert.IsNotNull(_chromeConnectService, "ChromeConnectService is not initialized.");
        Assert.IsNotNull(_appSettings, "_appSettings is not initialized."); // Added null check for safety

        var commandLineOptions = new CommandLineOptions
        {
            Url = url,
            Username = username,
            Password = password,
            Domain = domain ?? string.Empty,
            Incognito = false, 
            Kiosk = false,     
            IgnoreCertErrors = true
            // Headless removed - controlled by AppSettings.Browser.UseHeadless via BrowserManager
            // TimeoutSeconds removed - controlled by TimeoutManager settings
        };

        TestContext?.WriteLine($"Testing URL: {url} with Username: {username}, ExpectSuccess: {expectSuccess}");

        int exitCode = -1; // Default to an unhandled error code
        try
        {
            exitCode = await _chromeConnectService.ExecuteAsync(commandLineOptions);
        }
        catch (Exception ex)
        {
            // Catch any unexpected exceptions from ExecuteAsync itself, though ErrorHandler should manage most.
            TestContext?.WriteLine($"ExecuteAsync threw an unexpected exception: {ex.ToString()}");
            Assert.Fail($"ChromeConnectService.ExecuteAsync threw an unhandled exception: {ex.Message}");
        }

        TestContext?.WriteLine($"Service executed. URL: {url}, ExitCode: {exitCode}, ExpectedSuccess: {expectSuccess}");

        if (expectSuccess)
        {
            Assert.AreEqual(0, exitCode, $"Expected successful login (exit code 0) for {url} but got {exitCode}.");
        }
        else
        {
            // For an expected login failure, an exit code of 1 is anticipated.
            Assert.AreEqual(1, exitCode, $"Expected failed login (exit code 1) for {url} due to bad credentials but got {exitCode}.");
        }
    }
} 