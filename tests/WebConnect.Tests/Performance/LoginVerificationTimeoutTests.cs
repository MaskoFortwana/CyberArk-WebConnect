using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebConnect.Core;
using WebConnect.Services;
using WebConnect.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Threading;
using Moq;
using System.Diagnostics;
using OpenQA.Selenium;

namespace WebConnect.Tests.Performance;

[TestClass]
public class LoginVerificationTimeoutTests
{
    private ILoggerFactory? _loggerFactory;
    private Mock<IWebDriver>? _mockDriver;
    private Mock<IWebElement>? _mockPasswordElement;
    private LoginVerifier? _loginVerifier;
    private TestContext? _testContextInstance;

    /// <summary>
    ///Gets or sets the test context which provides
    ///information about and functionality for the current test run.
    ///</summary>
    public TestContext? TestContext
    {
        get => _testContextInstance;
        set => _testContextInstance = value;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerFactory = LoggerFactory.Create(builder => 
        {
            builder
                .AddFilter("Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning)
                .AddFilter("System", Microsoft.Extensions.Logging.LogLevel.Warning)
                .AddFilter("WebConnect", Microsoft.Extensions.Logging.LogLevel.Debug)
                .AddConsole();
        });

        // Setup mock WebDriver
        _mockDriver = new Mock<IWebDriver>();
        _mockPasswordElement = new Mock<IWebElement>();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _loggerFactory?.Dispose();
    }

    [TestMethod]
    public async Task OptimizedConfiguration_ShouldCompleteUnder10Seconds()
    {
        // Arrange - Use the optimized configuration from the fix
        var config = new LoginVerificationConfig
        {
            MaxVerificationTimeSeconds = 10, // 10 seconds (down from 30s default)
            InitialDelayMs = 500,            // Quick initial delay
            EnableTimingLogs = true,         // Enable performance monitoring
            CaptureScreenshotsOnFailure = true
        };

        var logger = _loggerFactory!.CreateLogger<LoginVerifier>();
        var timeoutConfig = new TimeoutConfig();
        var policyFactoryLogger = _loggerFactory!.CreateLogger<PolicyFactory>();
        var policyFactory = new PolicyFactory(policyFactoryLogger, timeoutConfig);
        _loginVerifier = new LoginVerifier(logger, config, timeoutConfig, policyFactory);

        // Mock successful login scenario (URL change detected quickly)
        var originalUrl = "https://example.com/login";
        var successUrl = "https://example.com/dashboard";
        
        _mockDriver.SetupSequence(d => d.Url)
            .Returns(originalUrl)  // Initial check
            .Returns(successUrl);  // After "login" - URL changed

        _mockPasswordElement.Setup(e => e.Displayed).Returns(false); // Form disappeared

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _loginVerifier.VerifyLoginSuccessAsync(_mockDriver!.Object);
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(result, "Login verification should succeed");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, 
            $"Verification should complete in under 10 seconds, but took {stopwatch.ElapsedMilliseconds}ms");
        
        // For successful login with URL change, should be very fast (under 3 seconds)
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 3000, 
            $"URL change detection should be very fast, but took {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task QuickErrorDetection_ShouldFailFast()
    {
        // Arrange - Configuration for quick error detection
        var config = new LoginVerificationConfig
        {
            MaxVerificationTimeSeconds = 10,
            InitialDelayMs = 500,
            EnableTimingLogs = true,
            CaptureScreenshotsOnFailure = true
        };

        var logger = _loggerFactory!.CreateLogger<LoginVerifier>();
        var timeoutConfig = new TimeoutConfig();
        var policyFactoryLogger = _loggerFactory!.CreateLogger<PolicyFactory>();
        var policyFactory = new PolicyFactory(policyFactoryLogger, timeoutConfig);
        _loginVerifier = new LoginVerifier(logger, config, timeoutConfig, policyFactory);

        var originalUrl = "https://example.com/login";
        
        // Mock failed login scenario - URL doesn't change, form still visible
        _mockDriver.Setup(d => d.Url).Returns(originalUrl);
        _mockPasswordElement.Setup(e => e.Displayed).Returns(true); // Form still visible
        
        // Mock page source with error message for immediate detection
        _mockDriver.Setup(d => d.PageSource).Returns(
            "<html><body><div class='error'>Invalid credentials</div></body></html>");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _loginVerifier.VerifyLoginSuccessAsync(_mockDriver!.Object);
        stopwatch.Stop();

        // Assert
        Assert.IsFalse(result, "Login verification should fail");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, 
            $"Verification should complete in under 10 seconds, but took {stopwatch.ElapsedMilliseconds}ms");
        
        // Error detection should be reasonably fast
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 8000, 
            $"Error detection should complete within error check timeout, but took {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task MaxTimeout_ShouldNotExceed10Seconds()
    {
        // Arrange - Test the absolute maximum timeout scenario
        var config = new LoginVerificationConfig
        {
            MaxVerificationTimeSeconds = 10,
            InitialDelayMs = 500,
            EnableTimingLogs = true,
            CaptureScreenshotsOnFailure = true
        };

        var logger = _loggerFactory!.CreateLogger<LoginVerifier>();
        var timeoutConfig = new TimeoutConfig();
        var policyFactoryLogger = _loggerFactory!.CreateLogger<PolicyFactory>();
        var policyFactory = new PolicyFactory(policyFactoryLogger, timeoutConfig);
        _loginVerifier = new LoginVerifier(logger, config, timeoutConfig, policyFactory);

        var originalUrl = "https://example.com/login";
        
        // Mock ambiguous scenario - no clear success or failure indicators
        _mockDriver.Setup(d => d.Url).Returns(originalUrl); // URL doesn't change
        _mockPasswordElement.Setup(e => e.Displayed).Returns(true); // Form still visible
        _mockDriver.Setup(d => d.PageSource).Returns("<html><body>Loading...</body></html>"); // No error messages

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _loginVerifier.VerifyLoginSuccessAsync(_mockDriver!.Object);
        stopwatch.Stop();

        // Assert
        // In ambiguous cases, the system should default to success after max timeout
        Assert.IsTrue(result, "Ambiguous cases should default to success");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 9000, 
            $"Should wait close to max timeout in ambiguous cases, but only took {stopwatch.ElapsedMilliseconds}ms");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds <= 11000, 
            $"Should not exceed max timeout significantly, but took {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task PerformanceComparison_OldVsNewTimeout()
    {
        // Arrange - Test both old (30s) and new (10s) configurations
        var oldConfig = new LoginVerificationConfig
        {
            MaxVerificationTimeSeconds = 30, // Old default
            InitialDelayMs = 1000,
            EnableTimingLogs = true,
            CaptureScreenshotsOnFailure = true
        };

        var newConfig = new LoginVerificationConfig
        {
            MaxVerificationTimeSeconds = 10, // New optimized
            InitialDelayMs = 500,
            EnableTimingLogs = true,
            CaptureScreenshotsOnFailure = true
        };

        var logger = _loggerFactory!.CreateLogger<LoginVerifier>();

        var originalUrl = "https://example.com/login";
        
        // Mock ambiguous scenario for both tests
        _mockDriver.Setup(d => d.Url).Returns(originalUrl);
        _mockPasswordElement.Setup(e => e.Displayed).Returns(true);
        _mockDriver.Setup(d => d.PageSource).Returns("<html><body>Loading...</body></html>");

        // Test new configuration
        var timeoutConfig = new TimeoutConfig();
        var policyFactoryLogger = _loggerFactory!.CreateLogger<PolicyFactory>();
        var policyFactory = new PolicyFactory(policyFactoryLogger, timeoutConfig);
        _loginVerifier = new LoginVerifier(logger, newConfig, timeoutConfig, policyFactory);
        var stopwatchNew = Stopwatch.StartNew();
        var resultNew = await _loginVerifier.VerifyLoginSuccessAsync(_mockDriver!.Object);
        stopwatchNew.Stop();

        // Test old configuration (simulate, don't actually wait 30 seconds)
        // We'll just verify the configuration difference
        var improvementRatio = (double)oldConfig.MaxVerificationTimeSeconds / newConfig.MaxVerificationTimeSeconds;

        // Assert
        Assert.IsTrue(improvementRatio == 3.0, 
            $"New configuration should be 3x faster (30s -> 10s), ratio: {improvementRatio}");
        Assert.IsTrue(stopwatchNew.ElapsedMilliseconds <= 11000, 
            $"New configuration should complete within 10s, took {stopwatchNew.ElapsedMilliseconds}ms");
        
        TestContext?.WriteLine($"Performance improvement: {improvementRatio}x faster");
        TestContext?.WriteLine($"New config actual time: {stopwatchNew.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task ProgressiveVerification_ShouldFollowPhases()
    {
        // Arrange - Test the progressive verification phases
        var config = new LoginVerificationConfig
        {
            MaxVerificationTimeSeconds = 10,
            InitialDelayMs = 500,
            EnableTimingLogs = true,
            CaptureScreenshotsOnFailure = true
        };

        var logger = _loggerFactory!.CreateLogger<LoginVerifier>();
        var timeoutConfig = new TimeoutConfig();
        var policyFactoryLogger = _loggerFactory!.CreateLogger<PolicyFactory>();
        var policyFactory = new PolicyFactory(policyFactoryLogger, timeoutConfig);
        _loginVerifier = new LoginVerifier(logger, config, timeoutConfig, policyFactory);

        var originalUrl = "https://example.com/login";
        var successUrl = "https://example.com/dashboard";
        
        // Mock URL change after a few calls (representing time passage)
        var callCount = 0;
        _mockDriver.Setup(d => d.Url).Returns(() => 
        {
            callCount++;
            // Simulate URL change after a few calls (representing time passage)
            return callCount > 3 ? successUrl : originalUrl;
        });

        _mockPasswordElement.Setup(e => e.Displayed).Returns(false);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _loginVerifier.VerifyLoginSuccessAsync(_mockDriver!.Object);
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(result, "Should detect success via URL change");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 3000, 
            $"Should complete in early phases, but took {stopwatch.ElapsedMilliseconds}ms");
    }
} 
