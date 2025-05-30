using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebConnect.Core;
using WebConnect.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OpenQA.Selenium;
using System.Collections.ObjectModel;
using System.Linq;

namespace WebConnect.Tests.Detection;

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
                .AddFilter("WebConnect", Microsoft.Extensions.Logging.LogLevel.Debug)
                .AddConsole();
        });

        // Setup mock WebDriver and elements
        _mockDriver = new Mock<IWebDriver>();
        _mockUsernameElement = new Mock<IWebElement>();
        _mockPasswordElement = new Mock<IWebElement>();
        _mockDomainElement = new Mock<IWebElement>();
        _mockSubmitButton = new Mock<IWebElement>();

        // Configure mock elements with comprehensive attributes to prevent NullReferenceExceptions
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
        _mockDomainElement.Setup(e => e.GetAttribute("value")).Returns("");
        _mockDomainElement.Setup(e => e.GetAttribute("class")).Returns("");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("");
        _mockDomainElement.Setup(e => e.SendKeys(It.IsAny<string>())).Verifiable();
        _mockDomainElement.Setup(e => e.Clear()).Verifiable();
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
        // Arrange - Valid domain field that's separate from username/password
        _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain-field");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Enter domain");

        // Setup domain element to support enhanced text entry with value verification
        var currentValue = "";
        _mockDomainElement.Setup(e => e.GetAttribute("value"))
            .Returns(() => currentValue);
        
        _mockDomainElement.Setup(e => e.SendKeys(It.IsAny<string>()))
            .Callback<string>(text => {
                currentValue += text; // Simulate text accumulation
            });

        _mockDomainElement.Setup(e => e.Clear())
            .Callback(() => {
                currentValue = ""; // Reset value on clear
            });

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
        
        // Verify domain field was used (enhanced text entry may split the text)
        _mockDomainElement.Verify(e => e.Clear(), Times.Once);
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.AtLeastOnce);
        
        // Verify the final accumulated value is correct
        Assert.AreEqual(credentials.Domain, currentValue, "Final domain value should match expected domain");
        
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

        // Setup mock options for the dropdown
        var mockOption1 = new Mock<IWebElement>();
        mockOption1.Setup(o => o.Text).Returns("-- Select Domain --");
        mockOption1.Setup(o => o.GetAttribute("value")).Returns("");

        var mockOption2 = new Mock<IWebElement>();
        mockOption2.Setup(o => o.Text).Returns("testdomain");
        mockOption2.Setup(o => o.GetAttribute("value")).Returns("testdomain");

        var mockOption3 = new Mock<IWebElement>();
        mockOption3.Setup(o => o.Text).Returns("otherdomain");
        mockOption3.Setup(o => o.GetAttribute("value")).Returns("otherdomain");

        var options = new ReadOnlyCollection<IWebElement>(new List<IWebElement>
        {
            mockOption1.Object,
            mockOption2.Object,
            mockOption3.Object
        });

        // Setup the domain element to return options when FindElements is called
        _mockDomainElement.Setup(e => e.FindElements(By.TagName("option"))).Returns(options);

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
        
        // For dropdown, verify that the correct option was clicked
        mockOption2.Verify(o => o.Click(), Times.Once, "The matching domain option should be clicked");
        
        // Username and password should work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenDomainIsNone_ShouldNotReceiveDomainValue()
    {
        // Arrange - Valid domain field but domain value is "none"
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
            Domain = "none" // Special "none" value to skip domain handling
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed");
        
        // Domain field should NOT be used when domain value is "none"
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never);
        _mockDomainElement.Verify(e => e.Clear(), Times.Never);
        
        // Username and password should work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }

    [TestMethod]
    public async Task DomainField_WhenDomainIsNoneCaseInsensitive_ShouldNotReceiveDomainValue()
    {
        // Arrange - Test various case combinations of "none"
        var testCases = new[] { "none", "NONE", "None", "NoNe", "nOnE" };

        foreach (var domainValue in testCases)
        {
            // Setup fresh mocks for each test case
            var mockDomainElement = new Mock<IWebElement>();
            mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain-field");
            mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
            mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Enter domain");
            mockDomainElement.Setup(e => e.Displayed).Returns(true);
            mockDomainElement.Setup(e => e.Enabled).Returns(true);

            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement.Object,
                PasswordField = _mockPasswordElement.Object,
                DomainField = mockDomainElement.Object,
                SubmitButton = _mockSubmitButton.Object
            };

            var credentials = new LoginCredentials
            {
                Username = "testuser",
                Password = "testpass",
                Domain = domainValue // Case variations of "none"
            };

            // Act
            var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
                credentials.Username, credentials.Password, credentials.Domain);

            // Assert
            Assert.IsTrue(result, $"Credential entry should succeed for domain value '{domainValue}'");
            
            // Domain field should NOT be used when domain value is any case variation of "none"
            mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never,
                $"Domain field should not receive any input for domain value '{domainValue}'");
            mockDomainElement.Verify(e => e.Clear(), Times.Never,
                $"Domain field should not be cleared for domain value '{domainValue}'");
        }

        // Username and password should work normally for all test cases
        _mockUsernameElement.Verify(e => e.SendKeys("testuser"), Times.Exactly(testCases.Length));
        _mockPasswordElement.Verify(e => e.SendKeys("testpass"), Times.Exactly(testCases.Length));
    }

    [TestMethod]
    public async Task DomainField_WhenDomainIsNoneButFieldIsNull_ShouldNotCauseError()
    {
        // Arrange - No domain field detected (null) and domain value is "none"
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
            Domain = "none" // Domain is "none" but no field to skip
        };

        // Act
        var result = await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
            credentials.Username, credentials.Password, credentials.Domain);

        // Assert
        Assert.IsTrue(result, "Credential entry should succeed even when domain is 'none' and no domain field exists");
        
        // Username and password should work normally
        _mockUsernameElement.Verify(e => e.SendKeys(credentials.Username), Times.Once);
        _mockPasswordElement.Verify(e => e.SendKeys(credentials.Password), Times.Once);
    }
} 
