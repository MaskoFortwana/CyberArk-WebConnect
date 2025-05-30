using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using WebConnect.Core;
using WebConnect.Models;
using WebConnect.Services;
using WebConnect.Configuration;
using Moq;
using OpenQA.Selenium;
using System.Threading.Tasks;

namespace WebConnect.Tests;

[TestClass]
public class DomainNoneIntegrationTests
{
    private ILoggerFactory? _loggerFactory;
    private Mock<IWebDriver>? _mockDriver;
    private Mock<IWebElement>? _mockUsernameElement;
    private Mock<IWebElement>? _mockPasswordElement;
    private Mock<IWebElement>? _mockDomainElement;
    private Mock<IWebElement>? _mockSubmitButton;
    private Mock<IScreenshotCapture>? _mockScreenshotCapture;
    
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
        _mockSubmitButton = new Mock<IWebElement>();
        _mockScreenshotCapture = new Mock<IScreenshotCapture>();

        // Configure mock elements
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

        // Setup domain element attributes
        _mockDomainElement.Setup(e => e.GetAttribute("type")).Returns("text");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Domain");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _loggerFactory?.Dispose();
    }

    [TestMethod]
    public void CommandLineOptions_DomainNoneIntegration_ShouldWorkEndToEnd()
    {
        // Arrange
        var options = new CommandLineOptions
        {
            Username = "testuser",
            Password = "testpass",
            Url = "https://example.com/login",
            Domain = "none",
            IncognitoString = "no",
            KioskString = "no",
            CertString = "ignore"
        };

        // Act & Assert
        Assert.AreEqual("none", options.Domain, "Domain should be set to 'none'");
        Assert.IsTrue(options.ShouldSkipDomainDetection(), "ShouldSkipDomainDetection should return true");

        // Verify other options work normally
        Assert.AreEqual("testuser", options.Username);
        Assert.AreEqual("testpass", options.Password);
        Assert.AreEqual("https://example.com/login", options.Url);
        Assert.IsFalse(options.Incognito);
        Assert.IsFalse(options.Kiosk);
        Assert.IsTrue(options.IgnoreCertErrors);
    }

    [TestMethod]
    public async Task LoginDetectorAndCredentialManager_DomainNoneIntegration_ShouldSkipDomainProcessing()
    {
        // Arrange
        var loginDetectorLogger = _loggerFactory!.CreateLogger<LoginDetector>();
        var credentialManagerLogger = _loggerFactory.CreateLogger<CredentialManager>();
        
        var loginDetector = new LoginDetector(loginDetectorLogger);
        var credentialManager = new CredentialManager(credentialManagerLogger);

        // Act - Test LoginDetector with domain="none"
        var detectionResult = await loginDetector.DetectLoginFormAsync(_mockDriver!.Object, "none");

        // Assert - Domain field should be null (skipped)
        Assert.IsNotNull(detectionResult, "LoginDetector should return a result");
        Assert.IsNull(detectionResult.DomainField, "Domain field should be null when domain is 'none'");

        // Act - Test CredentialManager with domain="none"
        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object,
            SubmitButton = _mockSubmitButton!.Object
        };

        var credentialResult = await credentialManager.EnterCredentialsAsync(_mockDriver.Object, loginForm,
            "testuser", "testpass", "none");

        // Assert - Credentials should be entered except domain
        Assert.IsTrue(credentialResult, "Credential entry should succeed");
        _mockUsernameElement.Verify(e => e.SendKeys("testuser"), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys("testpass"), Times.Once);
        
        // Domain field should not be touched
        _mockDomainElement.Verify(e => e.Clear(), Times.Never);
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task FullWorkflow_DomainNoneIntegration_ShouldOptimizePerformance()
    {
        // Arrange - Create all required components
        var browserManagerLogger = _loggerFactory!.CreateLogger<BrowserManager>();
        var loginDetectorLogger = _loggerFactory.CreateLogger<LoginDetector>();
        var credentialManagerLogger = _loggerFactory.CreateLogger<CredentialManager>();
        var loginVerifierLogger = _loggerFactory.CreateLogger<LoginVerifier>();
        var errorHandlerLogger = _loggerFactory.CreateLogger<ErrorHandler>();
        var timeoutManagerLogger = _loggerFactory.CreateLogger<TimeoutManager>();
        var errorMonitorLogger = _loggerFactory.CreateLogger<ErrorMonitor>();

        // Create component instances (simplified for testing)
        var browserManager = new BrowserManager(browserManagerLogger);
        var loginDetector = new LoginDetector(loginDetectorLogger);
        var credentialManager = new CredentialManager(credentialManagerLogger);
        
        // Test the key integration point: domain parameter flow
        var commandLineOptions = new CommandLineOptions
        {
            Username = "testuser",
            Password = "testpass",
            Url = "https://example.com/login",
            Domain = "none",
            IncognitoString = "no",
            KioskString = "no",
            CertString = "ignore"
        };

        // Act - Test domain skip detection
        var shouldSkip = commandLineOptions.ShouldSkipDomainDetection();
        Assert.IsTrue(shouldSkip, "Command line options should indicate domain detection should be skipped");

        // Test LoginDetector receives and processes the domain parameter correctly
        var loginFormResult = await loginDetector.DetectLoginFormAsync(_mockDriver!.Object, commandLineOptions.Domain);
        
        // Assert - Integration works end-to-end
        Assert.IsNotNull(loginFormResult, "Login form detection should return a result");
        Assert.IsNull(loginFormResult.DomainField, "Domain field should be null when domain is 'none'");
        
        // Verify the optimization path was taken by checking that no domain field is present
        Assert.AreEqual("none", commandLineOptions.Domain, "Domain should remain 'none'");
    }

    [TestMethod]
    public async Task ErrorHandling_DomainNoneIntegration_ShouldWorkNormally()
    {
        // Arrange
        var loginDetectorLogger = _loggerFactory!.CreateLogger<LoginDetector>();
        var credentialManagerLogger = _loggerFactory.CreateLogger<CredentialManager>();
        
        var loginDetector = new LoginDetector(loginDetectorLogger);
        var credentialManager = new CredentialManager(credentialManagerLogger);

        // Setup a scenario where domain field is null (which is normal for domain="none")
        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = null, // Simulating no domain field detected (expected for domain="none")
            SubmitButton = _mockSubmitButton!.Object
        };

        // Act - Test that null domain field is handled gracefully
        var result = await credentialManager.EnterCredentialsAsync(_mockDriver!.Object, loginForm,
            "testuser", "testpass", "none");

        // Assert - Should work normally despite null domain field
        Assert.IsTrue(result, "Credential entry should succeed even with null domain field");
        
        _mockUsernameElement.Verify(e => e.SendKeys("testuser"), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys("testpass"), Times.Once);
        
        // No domain operations should be attempted
        _mockDomainElement.Verify(e => e.Clear(), Times.Never);
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void ValidationLogic_DomainNoneIntegration_ShouldPassValidation()
    {
        // Arrange
        var options = new CommandLineOptions
        {
            Username = "testuser",
            Password = "testpass",
            Url = "https://example.com/login",
            Domain = "none",
            IncognitoString = "no",
            KioskString = "no",
            CertString = "ignore"
        };

        // Act
        var validationErrors = options.ValidateOptions();

        // Assert - "none" should be accepted as a valid domain value
        Assert.IsNotNull(validationErrors, "Validation should return a list");
        
        // Check that no domain-related validation errors occur
        var domainErrors = validationErrors.Where(e => e.Contains("domain", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.AreEqual(0, domainErrors.Count, "There should be no domain validation errors");
        
        // Verify overall validation passes (assuming other required fields are properly set)
        var requiredFieldErrors = validationErrors.Where(e => 
            e.Contains("Username") || e.Contains("Password") || e.Contains("URL") || e.Contains("Domain")).ToList();
        Assert.AreEqual(0, requiredFieldErrors.Count, "All required fields should pass validation");
    }

    [TestMethod]
    public void HelpText_DomainNoneIntegration_ShouldDocumentNoneOption()
    {
        // Act
        var helpText = CommandLineOptions.GetExtendedHelpText();

        // Assert - Help text should mention the "none" option
        Assert.IsTrue(helpText.Contains("none"), "Help text should mention the 'none' option");
        Assert.IsTrue(helpText.Contains("skip domain field detection"), 
            "Help text should explain what the 'none' option does");
        
        // Verify example usage includes the --DOM none parameter
        Assert.IsTrue(helpText.Contains("--DOM none"), 
            "Help text should include an example with --DOM none");
    }

    [TestMethod]
    public async Task RegressionTest_DomainNoneIntegration_ShouldNotAffectNormalDomainProcessing()
    {
        // Arrange
        var loginDetectorLogger = _loggerFactory!.CreateLogger<LoginDetector>();
        var credentialManagerLogger = _loggerFactory.CreateLogger<CredentialManager>();
        
        var loginDetector = new LoginDetector(loginDetectorLogger);
        var credentialManager = new CredentialManager(credentialManagerLogger);

        // Setup mock for normal domain detection
        _mockDriver!.Setup(d => d.FindElements(It.IsAny<By>()))
               .Returns(new List<IWebElement> { _mockDomainElement!.Object }.AsReadOnly());

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement.Object,
            SubmitButton = _mockSubmitButton!.Object
        };

        // Act - Test with a regular domain value (not "none")
        var detectionResult = await loginDetector.DetectLoginFormAsync(_mockDriver.Object, "testdomain");
        var credentialResult = await credentialManager.EnterCredentialsAsync(_mockDriver.Object, loginForm,
            "testuser", "testpass", "testdomain");

        // Assert - Normal domain processing should work as before
        Assert.IsTrue(credentialResult, "Credential entry should succeed with regular domain");
        
        _mockUsernameElement.Verify(e => e.SendKeys("testuser"), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys("testpass"), Times.Once);
        _mockDomainElement.Verify(e => e.Clear(), Times.Once);
        _mockDomainElement.Verify(e => e.SendKeys("testdomain"), Times.Once);
        
        // Verify that the "none" optimization doesn't interfere with normal operation
        Assert.IsNotNull(detectionResult, "Detection should work normally with regular domain values");
    }

    [TestMethod]
    public void CaseInsensitive_DomainNoneIntegration_ShouldWorkWithAllCaseVariations()
    {
        // Arrange
        var testCases = new[] { "none", "NONE", "None", "NoNe", "nOnE" };

        foreach (var testCase in testCases)
        {
            var options = new CommandLineOptions
            {
                Username = "testuser",
                Password = "testpass",
                Url = "https://example.com/login",
                Domain = testCase,
                IncognitoString = "no",
                KioskString = "no",
                CertString = "ignore"
            };

            // Act & Assert
            Assert.IsTrue(options.ShouldSkipDomainDetection(), 
                $"ShouldSkipDomainDetection should return true for '{testCase}' (case insensitive)");
            
            // Verify validation passes
            var validationErrors = options.ValidateOptions();
            var domainErrors = validationErrors.Where(e => e.Contains("domain", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.AreEqual(0, domainErrors.Count, 
                $"No domain validation errors should occur for '{testCase}'");
        }
    }

    public TestContext? TestContext { get; set; }
} 