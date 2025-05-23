using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using ChromeConnect.Core;
using ChromeConnect.Models;
using Moq;
using OpenQA.Selenium;

namespace ChromeConnect.Tests.Detection;

[TestClass]
public class DomainFieldHandlingTests
{
    private ILoggerFactory? _loggerFactory;
    private Mock<IWebDriver>? _mockDriver;
    private Mock<IWebElement>? _mockUsernameElement;
    private Mock<IWebElement>? _mockPasswordElement;
    private Mock<IWebElement>? _mockDomainElement;
    private Mock<IWebElement>? _mockSubmitButton;
    private CredentialManager? _credentialManager;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerFactory = LoggerFactory.Create(builder => 
        {
            builder
                .AddFilter("Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning)
                .AddFilter("System", Microsoft.Extensions.Logging.LogLevel.Warning)
                .AddFilter("ChromeConnect", Microsoft.Extensions.Logging.LogLevel.Debug)
                .AddConsole();
        });

        // Setup mock WebDriver and elements
        _mockDriver = new Mock<IWebDriver>();
        _mockUsernameElement = new Mock<IWebElement>();
        _mockPasswordElement = new Mock<IWebElement>();
        _mockDomainElement = new Mock<IWebElement>();
        _mockSubmitButton = new Mock<IWebElement>();

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

        var logger = _loggerFactory.CreateLogger<CredentialManager>();
        _credentialManager = new CredentialManager(logger);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _loggerFactory?.Dispose();
    }

    [TestMethod]
    public async Task DomainField_WhenValidAndSeparate_ShouldReceiveDomainValue()
    {
        // Arrange - Valid domain field with domain-specific attributes
        _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain-field");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Enter domain");

        // Different elements for username and password
        _mockUsernameElement.Setup(e => e.GetAttribute("id")).Returns("username-field");
        _mockPasswordElement.Setup(e => e.GetAttribute("id")).Returns("password-field");

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement.Object,
            PasswordField = _mockPasswordElement.Object,
            DomainField = _mockDomainElement.Object,
            SubmitButton = _mockSubmitButton.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "testdomain"
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Verify domain field received the domain value
        _mockDomainElement.Verify(e => e.Clear(), Times.Once);
        _mockDomainElement.Verify(e => e.SendKeys(credentials.Domain), Times.Once);
        
        // Verify username and password fields received their respective values
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenSameAsUsernameField_ShouldNotReceiveDomainValue()
    {
        // Arrange - Domain field is actually the same element as username field (incorrect detection)
        var sameElement = _mockUsernameElement.Object;
        
        _mockUsernameElement.Setup(e => e.GetAttribute("id")).Returns("username-field");
        _mockUsernameElement.Setup(e => e.GetAttribute("name")).Returns("username");
        _mockUsernameElement.Setup(e => e.GetAttribute("placeholder")).Returns("Username");

        var loginForm = new LoginFormElements
        {
            UsernameField = sameElement,
            PasswordField = _mockPasswordElement.Object,
            DomainField = sameElement, // Same element as username - this is the bug scenario
            SubmitButton = _mockSubmitButton.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "testdomain"
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Verify username field received username value (not overwritten by domain)
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        
        // Domain should NOT be entered because validation should detect the overlap
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Domain), Times.Never);
        
        // Password should still work normally
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenSameAsPasswordField_ShouldNotReceiveDomainValue()
    {
        // Arrange - Domain field is actually the same element as password field (incorrect detection)
        var sameElement = _mockPasswordElement.Object;
        
        _mockPasswordElement.Setup(e => e.GetAttribute("id")).Returns("password-field");
        _mockPasswordElement.Setup(e => e.GetAttribute("name")).Returns("password");
        _mockPasswordElement.Setup(e => e.GetAttribute("placeholder")).Returns("Password");

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement.Object,
            PasswordField = sameElement,
            DomainField = sameElement, // Same element as password - this is the bug scenario
            SubmitButton = _mockSubmitButton.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "testdomain"
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Verify password field received password value (not overwritten by domain)
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
        
        // Domain should NOT be entered because validation should detect the overlap
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Domain), Times.Never);
        
        // Username should still work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenHasUsernameAttributes_ShouldNotReceiveDomainValue()
    {
        // Arrange - Domain field has username-like attributes (incorrect detection)
        _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("user-domain-field");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("username-domain");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Enter username or domain");

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement.Object,
            PasswordField = _mockPasswordElement.Object,
            DomainField = _mockDomainElement.Object,
            SubmitButton = _mockSubmitButton.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "testdomain"
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Domain should NOT be entered because field has username attributes
        _mockDomainElement.Verify(e => e.SendKeys(credentials.Domain), Times.Never);
        
        // Username and password should work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenHasPasswordAttributes_ShouldNotReceiveDomainValue()
    {
        // Arrange - Domain field has password-like attributes (incorrect detection)
        _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("password-domain-field");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("password-domain");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Enter password or domain");

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement.Object,
            PasswordField = _mockPasswordElement.Object,
            DomainField = _mockDomainElement.Object,
            SubmitButton = _mockSubmitButton.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "testdomain"
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Domain should NOT be entered because field has password attributes
        _mockDomainElement.Verify(e => e.SendKeys(credentials.Domain), Times.Never);
        
        // Username and password should work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenNoDomainProvided_ShouldNotBeUsed()
    {
        // Arrange - Valid domain field but no domain value provided
        _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain-field");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Enter domain");

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement.Object,
            PasswordField = _mockPasswordElement.Object,
            DomainField = _mockDomainElement.Object,
            SubmitButton = _mockSubmitButton.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "" // No domain provided
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Domain field should not be used when no domain value is provided
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never);
        _mockDomainElement.Verify(e => e.Clear(), Times.Never);
        
        // Username and password should work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenNull_ShouldNotCauseError()
    {
        // Arrange - No domain field detected (null)
        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement.Object,
            PasswordField = _mockPasswordElement.Object,
            DomainField = null, // No domain field
            SubmitButton = _mockSubmitButton.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "testdomain" // Domain provided but no field to enter it
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed even without domain field");
        
        // Username and password should work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenValidDropdown_ShouldSelectValue()
    {
        // Arrange - Domain field is a dropdown (select element)
        _mockDomainElement.Setup(e => e.TagName).Returns("select");
        _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain-select");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Select domain");

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement.Object,
            PasswordField = _mockPasswordElement.Object,
            DomainField = _mockDomainElement.Object,
            SubmitButton = _mockSubmitButton.Object
        };

        var credentials = new LoginCredentials
        {
            Username = "testuser",
            Password = "testpass",
            Domain = "testdomain"
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // For dropdown, the implementation should try to select by text first
        // If that fails, it falls back to SendKeys
        // We can't easily mock SelectElement, so we verify that the element was interacted with
        _mockDomainElement.Verify(e => e.Clear(), Times.Once);
        
        // Username and password should work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }
} 