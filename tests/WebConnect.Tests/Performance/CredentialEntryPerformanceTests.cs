using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebConnect.Core;
using WebConnect.Models;
using WebConnect.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Moq;
using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace WebConnect.Tests.Performance;

[TestClass]
public class CredentialEntryPerformanceTests
{
    private ILoggerFactory? _loggerFactory;
    private Mock<IWebDriver>? _mockDriver;
    private Mock<IWebElement>? _mockUsernameElement;
    private Mock<IWebElement>? _mockPasswordElement;
    private Mock<IWebElement>? _mockDomainElement;
    private CredentialManager? _credentialManager;
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

        // Setup mock WebDriver and elements
        _mockDriver = new Mock<IWebDriver>();
        _mockUsernameElement = new Mock<IWebElement>();
        _mockPasswordElement = new Mock<IWebElement>();
        _mockDomainElement = new Mock<IWebElement>();

        // Configure mock elements to simulate successful interactions
        _mockUsernameElement.Setup(e => e.SendKeys(It.IsAny<string>())).Verifiable();
        _mockPasswordElement.Setup(e => e.SendKeys(It.IsAny<string>())).Verifiable();
        _mockDomainElement.Setup(e => e.SendKeys(It.IsAny<string>())).Verifiable();
        
        _mockUsernameElement.Setup(e => e.Clear()).Verifiable();
        _mockPasswordElement.Setup(e => e.Clear()).Verifiable();
        _mockDomainElement.Setup(e => e.Clear()).Verifiable();

        _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
        _mockPasswordElement.Setup(e => e.Displayed).Returns(true);
        _mockDomainElement.Setup(e => e.Displayed).Returns(true);

        _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
        _mockPasswordElement.Setup(e => e.Enabled).Returns(true);
        _mockDomainElement.Setup(e => e.Enabled).Returns(true);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _loggerFactory?.Dispose();
    }

    [TestMethod]
    public async Task DirectMode_ShouldBeVeryFast()
    {
        // Arrange
        var config = new CredentialEntryConfig
        {
            TypingMode = TypingMode.Direct,
            MinDelayMs = 0,
            MaxDelayMs = 0,
            SubmissionDelayMs = 100
        };

        var logger = _loggerFactory!.CreateLogger<CredentialManager>();
        _credentialManager = new CredentialManager(logger, config);

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser123",
            Password = "testpassword456",
            Domain = "testdomain"
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _credentialManager.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500, 
            $"Direct mode should complete in under 500ms, but took {stopwatch.ElapsedMilliseconds}ms");

        // Verify all elements were interacted with
        _mockUsernameElement.Verify(e => e.Clear(), Times.Once);
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.Clear(), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
        _mockDomainElement.Verify(e => e.Clear(), Times.Once);
        _mockDomainElement.Verify(e => e.SendKeys(credentials.Domain), Times.Once);
    }

    [TestMethod]
    public async Task OptimizedHumanMode_ShouldBeFasterThanOriginal()
    {
        // Arrange - Optimized Human mode (default configuration)
        var config = new CredentialEntryConfig
        {
            TypingMode = TypingMode.OptimizedHuman,
            MinDelayMs = 10,
            MaxDelayMs = 30,
            SubmissionDelayMs = 500
        };

        var logger = _loggerFactory!.CreateLogger<CredentialManager>();
        _credentialManager = new CredentialManager(logger, config);

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser123",
            Password = "testpassword456",
            Domain = "testdomain"
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _credentialManager.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);
        stopwatch.Stop();

        // Assert - Should be significantly faster than original (which could take several seconds)
        // Target: 3-5x improvement means under 2 seconds for what used to take 6-10 seconds
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000, 
            $"OptimizedHuman mode should complete in under 2 seconds, but took {stopwatch.ElapsedMilliseconds}ms");

        // Should be slower than Direct mode but still fast
        Assert.IsTrue(stopwatch.ElapsedMilliseconds > 100, 
            $"OptimizedHuman mode should take some time for human-like behavior, but only took {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task FullHumanMode_ShouldStillBeReasonablyFast()
    {
        // Arrange - Full Human mode with reduced delays
        var config = new CredentialEntryConfig
        {
            TypingMode = TypingMode.FullHuman,
            MinDelayMs = 10,
            MaxDelayMs = 50,
            SubmissionDelayMs = 500
        };

        var logger = _loggerFactory!.CreateLogger<CredentialManager>();
        _credentialManager = new CredentialManager(logger, config);

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser123",
            Password = "testpassword456",
            Domain = "testdomain"
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _credentialManager.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);
        stopwatch.Stop();

        // Assert - Should still be faster than original implementation
        // Even FullHuman mode should complete in under 5 seconds
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, 
            $"FullHuman mode should complete in under 5 seconds, but took {stopwatch.ElapsedMilliseconds}ms");

        // Should be slower than OptimizedHuman mode
        Assert.IsTrue(stopwatch.ElapsedMilliseconds > 500, 
            $"FullHuman mode should take more time than OptimizedHuman, but only took {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task PerformanceComparison_AllModes()
    {
        // Arrange
        var credentials = new LoginCredentials
        {
            Username = "testuser123",
            Password = "testpassword456",
            Domain = "testdomain"
        };

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object
        };

        var logger = _loggerFactory!.CreateLogger<CredentialManager>();
        var results = new Dictionary<TypingMode, long>();

        // Test each mode
        foreach (TypingMode mode in Enum.GetValues<TypingMode>())
        {
            var config = new CredentialEntryConfig
            {
                TypingMode = mode,
                MinDelayMs = mode switch
                {
                    TypingMode.Direct => 0,
                    TypingMode.OptimizedHuman => 10,
                    TypingMode.FullHuman => 10,
                    _ => 10
                },
                MaxDelayMs = mode switch
                {
                    TypingMode.Direct => 0,
                    TypingMode.OptimizedHuman => 30,
                    TypingMode.FullHuman => 50,
                    _ => 30
                },
                SubmissionDelayMs = 500
            };

            _credentialManager = new CredentialManager(logger, config);

            // Act
            var stopwatch = Stopwatch.StartNew();
            await _credentialManager.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
                credentials.Username, credentials.Password, credentials.Domain);
            stopwatch.Stop();

            results[mode] = stopwatch.ElapsedMilliseconds;
            TestContext?.WriteLine($"Mode: {mode}, Time: {stopwatch.ElapsedMilliseconds}ms");
        }

        // Assert
        Assert.IsTrue(results[TypingMode.Direct] < results[TypingMode.OptimizedHuman],
            "Direct mode should be faster than OptimizedHuman.");
        Assert.IsTrue(results[TypingMode.OptimizedHuman] < results[TypingMode.FullHuman],
            "OptimizedHuman mode should be faster than FullHuman.");

        TestContext?.WriteLine($"--- Performance Summary ---");
        TestContext?.WriteLine($"Direct Mode: {results[TypingMode.Direct]}ms");
        TestContext?.WriteLine($"OptimizedHuman Mode: {results[TypingMode.OptimizedHuman]}ms");
        TestContext?.WriteLine($"FullHuman Mode: {results[TypingMode.FullHuman]}ms");
    }

    [TestMethod]
    public async Task LongCredentials_ShouldStillBePerformant()
    {
        // Arrange - Test with longer credentials to ensure chunking works well
        var config = new CredentialEntryConfig
        {
            TypingMode = TypingMode.OptimizedHuman,
            MinDelayMs = 10,
            MaxDelayMs = 30,
            SubmissionDelayMs = 500
        };

        var logger = _loggerFactory!.CreateLogger<CredentialManager>();
        _credentialManager = new CredentialManager(logger, config);

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "very.long.username.with.many.characters@company.domain.com",
            Password = "VeryLongPasswordWithManyCharactersAndSpecialSymbols!@#$%^&*()123456789",
            Domain = "very.long.domain.name.with.multiple.subdomains.company.com"
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _credentialManager.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);
        stopwatch.Stop();

        // Assert - Even with long credentials, should complete in reasonable time
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, 
            $"Long credentials should complete in under 10 seconds, but took {stopwatch.ElapsedMilliseconds}ms");
    }
} 
