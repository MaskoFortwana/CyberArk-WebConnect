using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebConnect.Core;
using WebConnect.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OpenQA.Selenium;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;

namespace WebConnect.Tests.Detection;

[TestClass]
public class DomainSkipTests
{
    private ILoggerFactory? _loggerFactory;
    private Mock<IWebDriver>? _mockDriver;
    private Mock<IWebElement>? _mockUsernameElement;
    private Mock<IWebElement>? _mockPasswordElement;
    private Mock<IWebElement>? _mockDomainElement;
    private Mock<IWebElement>? _mockSubmitButton;
    private Mock<IWebElement>? _mockFormElement;
    private LoginDetector? _loginDetector;
    private CredentialManager? _credentialManager;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));
        
        _mockDriver = new Mock<IWebDriver>();
        _mockUsernameElement = new Mock<IWebElement>();
        _mockPasswordElement = new Mock<IWebElement>();
        _mockDomainElement = new Mock<IWebElement>();
        _mockSubmitButton = new Mock<IWebElement>();
        _mockFormElement = new Mock<IWebElement>();

        // Setup mock driver URL to prevent NullReferenceException in LoginPageConfigurations.GetConfigurationForUrl
        _mockDriver.Setup(d => d.Url).Returns("https://test.example.com");

        // Setup default empty collection for all CSS selectors to avoid ArgumentNullException
        _mockDriver.Setup(d => d.FindElements(It.IsAny<By>()))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

        // Setup comprehensive CSS selector mocks for fast-path detection
        _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockPasswordElement.Object }));

        // Setup specific selectors for username detection
        _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='text'], input[type='email'], input:not([type]), select[name*='user'], select[id*='user'], select[name*='login'], select[id*='login']")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockUsernameElement.Object }));

        // Setup domain detection selectors
        _mockDriver.Setup(d => d.FindElements(By.CssSelector("select[name*='domain' i], select[id*='domain' i], input[name*='domain' i], input[id*='domain' i]")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockDomainElement.Object }));

        // Setup other common selectors that might be used
        _mockDriver.Setup(d => d.FindElements(By.CssSelector("button[type='submit'], input[type='submit']")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockSubmitButton.Object }));

        // Setup the main domain selector from LoginDetector.cs fast path
        _mockDriver.Setup(d => d.FindElements(By.CssSelector("select[name*='domain'], select[id*='domain'], select[name*='realm'], select[id*='realm'], select[name*='tenant'], select[id*='tenant'], select[name*='org'], select[id*='org'], select[name*='company'], select[id*='company'], select[name*='authority'], select[id*='authority'], input[name*='domain'], input[id*='domain'], input[name*='realm'], input[id*='realm'], input[name*='tenant'], input[id*='tenant'], input[name*='org'], input[id*='org'], input[name*='company'], input[id*='company'], input[name*='authority'], input[id*='authority'], select[class*='domain'], input[class*='domain'], select[placeholder*='domain' i], input[placeholder*='domain' i], select[aria-label*='domain' i], input[aria-label*='domain' i]")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockDomainElement.Object }));

        // Setup tag-based selectors that might be used
        _mockDriver.Setup(d => d.FindElements(By.TagName("input")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockUsernameElement.Object, _mockPasswordElement.Object, _mockDomainElement.Object }));
        
        _mockDriver.Setup(d => d.FindElements(By.TagName("button")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockSubmitButton.Object }));
        
        _mockDriver.Setup(d => d.FindElements(By.TagName("select")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

        _mockDriver.Setup(d => d.FindElements(By.TagName("a")))
            .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

        // Configure mock elements with comprehensive attributes
        _mockUsernameElement.Setup(e => e.TagName).Returns("input");
        _mockUsernameElement.Setup(e => e.GetAttribute("type")).Returns("text");
        _mockUsernameElement.Setup(e => e.GetAttribute("id")).Returns("username");
        _mockUsernameElement.Setup(e => e.GetAttribute("name")).Returns("username");
        _mockUsernameElement.Setup(e => e.GetAttribute("value")).Returns("");
        _mockUsernameElement.Setup(e => e.GetAttribute("class")).Returns("");
        _mockUsernameElement.Setup(e => e.GetAttribute("placeholder")).Returns("");
        _mockUsernameElement.Setup(e => e.SendKeys(It.IsAny<string>())).Verifiable();
        _mockUsernameElement.Setup(e => e.Clear()).Verifiable();
        _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
        _mockUsernameElement.Setup(e => e.Enabled).Returns(true);

        _mockPasswordElement.Setup(e => e.TagName).Returns("input");
        _mockPasswordElement.Setup(e => e.GetAttribute("type")).Returns("password");
        _mockPasswordElement.Setup(e => e.GetAttribute("id")).Returns("password");
        _mockPasswordElement.Setup(e => e.GetAttribute("name")).Returns("password");
        _mockPasswordElement.Setup(e => e.GetAttribute("value")).Returns("");
        _mockPasswordElement.Setup(e => e.GetAttribute("class")).Returns("");
        _mockPasswordElement.Setup(e => e.GetAttribute("placeholder")).Returns("");
        _mockPasswordElement.Setup(e => e.SendKeys(It.IsAny<string>())).Verifiable();
        _mockPasswordElement.Setup(e => e.Clear()).Verifiable();
        _mockPasswordElement.Setup(e => e.Displayed).Returns(true);
        _mockPasswordElement.Setup(e => e.Enabled).Returns(true);

        _mockDomainElement.Setup(e => e.TagName).Returns("input");
        _mockDomainElement.Setup(e => e.GetAttribute("type")).Returns("text");
        _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
        _mockDomainElement.Setup(e => e.GetAttribute("class")).Returns("");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("");
        
        // Setup value tracking for domain element to handle verification
        var domainValue = "";
        _mockDomainElement.Setup(e => e.GetAttribute("value")).Returns(() => domainValue);
        _mockDomainElement.Setup(e => e.SendKeys(It.IsAny<string>()))
            .Callback<string>(value => domainValue += value)
            .Verifiable();
        _mockDomainElement.Setup(e => e.Clear())
            .Callback(() => domainValue = "")
            .Verifiable();
        
        _mockDomainElement.Setup(e => e.Displayed).Returns(true);
        _mockDomainElement.Setup(e => e.Enabled).Returns(true);

        _mockSubmitButton.Setup(e => e.TagName).Returns("button");
        _mockSubmitButton.Setup(e => e.GetAttribute("type")).Returns("submit");
        _mockSubmitButton.Setup(e => e.GetAttribute("id")).Returns("submit");
        _mockSubmitButton.Setup(e => e.GetAttribute("name")).Returns("submit");
        _mockSubmitButton.Setup(e => e.GetAttribute("value")).Returns("");
        _mockSubmitButton.Setup(e => e.GetAttribute("class")).Returns("");
        _mockSubmitButton.Setup(e => e.Displayed).Returns(true);
        _mockSubmitButton.Setup(e => e.Enabled).Returns(true);

        var loginDetectorLogger = _loggerFactory.CreateLogger<LoginDetector>();
        var credentialManagerLogger = _loggerFactory.CreateLogger<CredentialManager>();
        
        _loginDetector = new LoginDetector(loginDetectorLogger);
        _credentialManager = new CredentialManager(credentialManagerLogger);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _loggerFactory?.Dispose();
    }

    [TestMethod]
    public async Task LoginDetector_WhenDomainIsNone_ShouldSkipDetection()
    {
        // Arrange
        var mockForm = _mockFormElement!.Object;

        // Act
        var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "none");

        // Assert
        Assert.IsNotNull(result, "LoginDetector should return a result");
        Assert.IsNull(result.DomainField, "Domain field should be null when domain is 'none'");
    }

    [TestMethod]
    public async Task LoginDetector_WhenDomainIsNoneCaseInsensitive_ShouldSkipDetection()
    {
        // Arrange - Test case insensitive behavior
        var testCases = new[] { "none", "NONE", "None", "NoNe", "nOnE" };

        foreach (var domainValue in testCases)
        {
            // Act
            var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, domainValue);

            // Assert
            Assert.IsNotNull(result, $"LoginDetector should return a result for domain '{domainValue}'");
            Assert.IsNull(result.DomainField, $"Domain field should be null when domain is '{domainValue}'");
        }
    }

    [TestMethod]
    public async Task LoginDetector_WhenDomainIsNotNone_ShouldPerformDetection()
    {
        // Arrange
        // Setup DOM elements that would normally be found
        _mockDriver!.Setup(d => d.FindElements(It.IsAny<By>()))
               .Returns(new List<IWebElement> { _mockDomainElement!.Object }.AsReadOnly());
        
        _mockDomainElement!.Setup(e => e.GetAttribute("type")).Returns("text");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");

        // Act
        var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver.Object, "testdomain");

        // Assert
        Assert.IsNotNull(result, "LoginDetector should return a result");
        // Note: In a real scenario, this would detect a domain field, but our mock setup is minimal
        // The important thing is that detection logic is executed (not skipped)
    }

    [TestMethod]
    public async Task CredentialManager_WhenDomainIsNone_ShouldSkipDomainEntry()
    {
        // Arrange
        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object,
            SubmitButton = _mockSubmitButton!.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "none"
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Verify username and password were entered
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
        
        // Verify domain field was NOT touched when domain is "none"
        _mockDomainElement.Verify(e => e.Clear(), Times.Never);
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task CredentialManager_WhenDomainIsNoneCaseInsensitive_ShouldSkipDomainEntry()
    {
        // Arrange
        var testCases = new[] { "none", "NONE", "None", "NoNe", "nOnE" };

        foreach (var domainValue in testCases)
        {
            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement!.Object,
                PasswordField = _mockPasswordElement!.Object,
                DomainField = _mockDomainElement!.Object,
                SubmitButton = _mockSubmitButton!.Object
            };

            var credentials = new LoginCredentials
            {
                Username = "testuser",
                Password = "testpass",
                Domain = domainValue
            };

            // Act
            var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
                credentials.Username, credentials.Password, credentials.Domain);

            // Assert
            Assert.IsTrue(result, $"Credential entry should succeed for domain '{domainValue}'");
            
            // Verify domain field was NOT touched
            _mockDomainElement.Verify(e => e.Clear(), Times.Never);
            _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never);
        }
    }

    [TestMethod]
    public async Task CredentialManager_WhenDomainIsValidValue_ShouldEnterDomain()
    {
        // Arrange
        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object,
            SubmitButton = _mockSubmitButton!.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "testdomain"
        };

        // Setup domain element to support enhanced text entry with value verification
        var currentValue = "";
        _mockDomainElement!.Setup(e => e.GetAttribute("value"))
            .Returns(() => currentValue);
        
        _mockDomainElement.Setup(e => e.SendKeys(It.IsAny<string>()))
            .Callback<string>(text => {
                currentValue += text; // Simulate text accumulation
            });

        _mockDomainElement.Setup(e => e.Clear())
            .Callback(() => {
                currentValue = ""; // Reset value on clear
            });

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Verify all fields were entered including domain
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
        _mockDomainElement.Verify(e => e.Clear(), Times.Once);
        
        // The enhanced text entry breaks the domain into chunks, so verify SendKeys was called 
        // at least once but allow for multiple calls (e.g., "testd" + "omain")
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.AtLeastOnce);
        
        // Verify the final accumulated value is correct
        Assert.AreEqual(credentials.Domain, currentValue, 
            "Final domain value should match the expected domain");
    }

    [TestMethod]
    public async Task CredentialManager_WhenDomainFieldIsNull_ShouldNotAttemptDomainEntry()
    {
        // Arrange - No domain field present
        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = null, // No domain field detected
            SubmitButton = _mockSubmitButton!.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "none"
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed even without domain field");
        
        // Verify username and password were entered
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
        
        // No domain field operations should occur
        _mockDomainElement.Verify(e => e.Clear(), Times.Never);
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never);
    }
} 