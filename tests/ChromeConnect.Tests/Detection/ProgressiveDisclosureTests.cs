using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenQA.Selenium;
using Microsoft.Extensions.Logging;
using ChromeConnect.Core;
using ChromeConnect.Models;
using System.Collections.ObjectModel;

namespace ChromeConnect.Tests.Detection
{
    [TestClass]
    public class ProgressiveDisclosureTests
    {
        private ILoggerFactory? _loggerFactory;
        private Mock<IWebDriver>? _mockDriver;
        private Mock<IJavaScriptExecutor>? _mockJsExecutor;
        private Mock<IWebElement>? _mockUsernameElement;
        private Mock<IWebElement>? _mockPasswordElement;
        private Mock<IWebElement>? _mockSubmitButton;
        private CredentialManager? _credentialManager;

        [TestInitialize]
        public void TestInitialize()
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _mockDriver = new Mock<IWebDriver>();
            _mockJsExecutor = new Mock<IJavaScriptExecutor>();
            _mockUsernameElement = new Mock<IWebElement>();
            _mockPasswordElement = new Mock<IWebElement>();
            _mockSubmitButton = new Mock<IWebElement>();

            // Setup driver to also implement IJavaScriptExecutor
            _mockDriver.As<IJavaScriptExecutor>();
            _mockJsExecutor.Setup(js => js.ExecuteScript(It.IsAny<string>(), It.IsAny<object[]>()))
                          .Returns(new { inputCount = 2, passwordCount = 1, buttonCount = 1, visibleInputs = 1, bodyHash = 1000 });

            var logger = _loggerFactory.CreateLogger<CredentialManager>();
            _credentialManager = new CredentialManager(logger);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _loggerFactory?.Dispose();
        }

        [TestMethod]
        public async Task DetectProgressiveDisclosure_UsernameVisiblePasswordHidden_ReturnsTrue()
        {
            // Arrange
            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
            _mockUsernameElement.Setup(e => e.TagName).Returns("input");

            _mockPasswordElement.Setup(e => e.Displayed).Returns(false);
            _mockPasswordElement.Setup(e => e.Enabled).Returns(true);
            _mockPasswordElement.Setup(e => e.TagName).Returns("input");

            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement.Object,
                PasswordField = _mockPasswordElement.Object,
                SubmitButton = null
            };

            // Setup driver to return empty collections for hidden element search
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));
            _mockDriver.Setup(d => d.FindElements(It.IsAny<By>()))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

            // Act
            var result = await InvokeDetectProgressiveDisclosureAsync(loginForm);

            // Assert
            Assert.IsTrue(result, "Should detect progressive disclosure when username is visible but password is hidden");
        }

        [TestMethod]
        public async Task DetectProgressiveDisclosure_AllFieldsVisible_ReturnsFalse()
        {
            // Arrange
            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
            _mockUsernameElement.Setup(e => e.TagName).Returns("input");

            _mockPasswordElement.Setup(e => e.Displayed).Returns(true);
            _mockPasswordElement.Setup(e => e.Enabled).Returns(true);
            _mockPasswordElement.Setup(e => e.TagName).Returns("input");

            _mockSubmitButton.Setup(e => e.Displayed).Returns(true);
            _mockSubmitButton.Setup(e => e.Enabled).Returns(true);
            _mockSubmitButton.Setup(e => e.TagName).Returns("button");

            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement.Object,
                PasswordField = _mockPasswordElement.Object,
                SubmitButton = _mockSubmitButton.Object
            };

            // Setup driver to return empty collections for hidden element search
            _mockDriver.Setup(d => d.FindElements(It.IsAny<By>()))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

            // Act
            var result = await InvokeDetectProgressiveDisclosureAsync(loginForm);

            // Assert
            Assert.IsFalse(result, "Should not detect progressive disclosure when all fields are visible");
        }

        [TestMethod]
        public async Task DetectProgressiveDisclosure_HiddenPasswordInDOM_ReturnsTrue()
        {
            // Arrange
            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);

            var hiddenPasswordElement = new Mock<IWebElement>();
            hiddenPasswordElement.Setup(e => e.Displayed).Returns(false);
            hiddenPasswordElement.Setup(e => e.Enabled).Returns(true);

            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement.Object,
                PasswordField = null,
                SubmitButton = null
            };

            // Setup driver to return hidden password field
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { hiddenPasswordElement.Object }));
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

            // Act
            var result = await InvokeDetectProgressiveDisclosureAsync(loginForm);

            // Assert
            Assert.IsTrue(result, "Should detect progressive disclosure when hidden password fields exist in DOM");
        }

        [TestMethod]
        public async Task EnterCredentialsSequentially_SuccessfulFlow_ReturnsTrue()
        {
            // Arrange
            var passwordFieldAfterUsername = new Mock<IWebElement>();
            passwordFieldAfterUsername.Setup(e => e.Displayed).Returns(true);
            passwordFieldAfterUsername.Setup(e => e.Enabled).Returns(true);
            passwordFieldAfterUsername.Setup(e => e.TagName).Returns("input");

            var submitButtonAfterPassword = new Mock<IWebElement>();
            submitButtonAfterPassword.Setup(e => e.Displayed).Returns(true);
            submitButtonAfterPassword.Setup(e => e.Enabled).Returns(true);
            submitButtonAfterPassword.Setup(e => e.TagName).Returns("button");
            submitButtonAfterPassword.Setup(e => e.Text).Returns("Login");

            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
            _mockUsernameElement.Setup(e => e.TagName).Returns("input");

            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement.Object,
                PasswordField = null, // Initially null, will appear after username entry
                SubmitButton = null   // Initially null, will appear after password entry
            };

            // Setup progressive element appearance
            var callCount = 0;
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']")))
                      .Returns(() =>
                      {
                          callCount++;
                          if (callCount > 2) // After username is entered
                          {
                              return new ReadOnlyCollection<IWebElement>(new List<IWebElement> { passwordFieldAfterUsername.Object });
                          }
                          return new ReadOnlyCollection<IWebElement>(new List<IWebElement>());
                      });

            var submitCallCount = 0;
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button")))
                      .Returns(() =>
                      {
                          submitCallCount++;
                          if (submitCallCount > 2) // After password is entered
                          {
                              return new ReadOnlyCollection<IWebElement>(new List<IWebElement> { submitButtonAfterPassword.Object });
                          }
                          return new ReadOnlyCollection<IWebElement>(new List<IWebElement>());
                      });

            // Setup JavaScript executor for DOM monitoring
            _mockDriver.As<IJavaScriptExecutor>()
                      .Setup(js => js.ExecuteScript(It.IsAny<string>()))
                      .Returns(new { inputCount = 2, passwordCount = 1, buttonCount = 1, visibleInputs = 1, bodyHash = 1000 });

            // Act
            var result = await _credentialManager!.EnterCredentialsAsync(
                _mockDriver.Object, 
                loginForm, 
                "testuser", 
                "testpass", 
                "");

            // Assert
            Assert.IsTrue(result, "Sequential credential entry should succeed");
            
            // Verify username was entered
            _mockUsernameElement.Verify(e => e.Clear(), Times.AtLeastOnce);
            _mockUsernameElement.Verify(e => e.Click(), Times.AtLeastOnce);
            // Verify username was entered (implementation splits the text)
            _mockUsernameElement.Verify(e => e.SendKeys("test"), Times.AtLeastOnce);
            _mockUsernameElement.Verify(e => e.SendKeys("user"), Times.AtLeastOnce);
            
            // Verify password was entered (implementation splits the text)
            passwordFieldAfterUsername.Verify(e => e.Clear(), Times.AtLeastOnce);
            passwordFieldAfterUsername.Verify(e => e.SendKeys("test"), Times.AtLeastOnce);
            passwordFieldAfterUsername.Verify(e => e.SendKeys("pass"), Times.AtLeastOnce);
            
            // Verify submit button was clicked
            submitButtonAfterPassword.Verify(e => e.Click(), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task EnterCredentialsSequentially_PasswordFieldNeverAppears_ReturnsFalse()
        {
            // Arrange
            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
            _mockUsernameElement.Setup(e => e.TagName).Returns("input");

            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement.Object,
                PasswordField = null,
                SubmitButton = null
            };

            // Setup driver to never return password field
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));
            _mockDriver.Setup(d => d.FindElements(It.IsAny<By>()))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

            // Setup JavaScript executor
            _mockDriver.As<IJavaScriptExecutor>()
                      .Setup(js => js.ExecuteScript(It.IsAny<string>()))
                      .Returns(new { inputCount = 1, passwordCount = 0, buttonCount = 0, visibleInputs = 1, bodyHash = 1000 });

            // Act
            var result = await _credentialManager!.EnterCredentialsAsync(
                _mockDriver.Object, 
                loginForm, 
                "testuser", 
                "testpass", 
                "");

            // Assert
            Assert.IsFalse(result, "Sequential credential entry should fail when password field never appears");
            
            // Verify username was still entered (implementation splits the text)
            _mockUsernameElement.Verify(e => e.SendKeys("test"), Times.AtLeastOnce);
            _mockUsernameElement.Verify(e => e.SendKeys("user"), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task EnterCredentialsSequentially_WithDomainField_HandlesOptionalDomain()
        {
            // Arrange
            var passwordFieldAfterUsername = new Mock<IWebElement>();
            passwordFieldAfterUsername.Setup(e => e.Displayed).Returns(true);
            passwordFieldAfterUsername.Setup(e => e.Enabled).Returns(true);
            passwordFieldAfterUsername.Setup(e => e.TagName).Returns("input");

            var domainFieldAfterPassword = new Mock<IWebElement>();
            domainFieldAfterPassword.Setup(e => e.Displayed).Returns(true);
            domainFieldAfterPassword.Setup(e => e.Enabled).Returns(true);
            domainFieldAfterPassword.Setup(e => e.TagName).Returns("input");

            var submitButtonAfterDomain = new Mock<IWebElement>();
            submitButtonAfterDomain.Setup(e => e.Displayed).Returns(true);
            submitButtonAfterDomain.Setup(e => e.Enabled).Returns(true);
            submitButtonAfterDomain.Setup(e => e.TagName).Returns("button");

            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
            _mockUsernameElement.Setup(e => e.TagName).Returns("input");

            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement.Object,
                PasswordField = null,
                SubmitButton = null
            };

            // Setup progressive element appearance
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { passwordFieldAfterUsername.Object }));

            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[name*='domain'], input[id*='domain'], select[name*='domain'], select[id*='domain']")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { domainFieldAfterPassword.Object }));

            _mockDriver.Setup(d => d.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { submitButtonAfterDomain.Object }));

            // Setup JavaScript executor
            _mockDriver.As<IJavaScriptExecutor>()
                      .Setup(js => js.ExecuteScript(It.IsAny<string>()))
                      .Returns(new { inputCount = 3, passwordCount = 1, buttonCount = 1, visibleInputs = 2, bodyHash = 1500 });

            // Act
            var result = await _credentialManager!.EnterCredentialsAsync(
                _mockDriver.Object, 
                loginForm, 
                "testuser", 
                "testpass", 
                "testdomain");

            // Assert
            Assert.IsTrue(result, "Sequential credential entry should succeed with domain field");
            
            // Verify all fields were entered (implementation splits the text)
            _mockUsernameElement.Verify(e => e.SendKeys("test"), Times.AtLeastOnce);
            _mockUsernameElement.Verify(e => e.SendKeys("user"), Times.AtLeastOnce);
            passwordFieldAfterUsername.Verify(e => e.SendKeys("test"), Times.AtLeastOnce);
            passwordFieldAfterUsername.Verify(e => e.SendKeys("pass"), Times.AtLeastOnce);
            domainFieldAfterPassword.Verify(e => e.SendKeys("testd"), Times.AtLeastOnce);
            domainFieldAfterPassword.Verify(e => e.SendKeys("omain"), Times.AtLeastOnce);
            submitButtonAfterDomain.Verify(e => e.Click(), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task EnterCredentialsSequentially_FallbackToEnterKey_WhenNoSubmitButton()
        {
            // Arrange
            var passwordFieldAfterUsername = new Mock<IWebElement>();
            passwordFieldAfterUsername.Setup(e => e.Displayed).Returns(true);
            passwordFieldAfterUsername.Setup(e => e.Enabled).Returns(true);
            passwordFieldAfterUsername.Setup(e => e.TagName).Returns("input");

            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
            _mockUsernameElement.Setup(e => e.TagName).Returns("input");

            var loginForm = new LoginFormElements
            {
                UsernameField = _mockUsernameElement.Object,
                PasswordField = null,
                SubmitButton = null
            };

            // Setup password field to appear but no submit button
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { passwordFieldAfterUsername.Object }));

            _mockDriver.Setup(d => d.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

            // Setup JavaScript executor
            _mockDriver.As<IJavaScriptExecutor>()
                      .Setup(js => js.ExecuteScript(It.IsAny<string>()))
                      .Returns(new { inputCount = 2, passwordCount = 1, buttonCount = 0, visibleInputs = 2, bodyHash = 1200 });

            // Act
            var result = await _credentialManager!.EnterCredentialsAsync(
                _mockDriver.Object, 
                loginForm, 
                "testuser", 
                "testpass", 
                "");

            // Assert
            Assert.IsTrue(result, "Sequential credential entry should succeed with Enter key fallback");
            
            // Verify Enter key was used for submission
            passwordFieldAfterUsername.Verify(e => e.SendKeys(Keys.Return), Times.AtLeastOnce);
        }

        /// <summary>
        /// Helper method to invoke the private DetectProgressiveDisclosureAsync method
        /// </summary>
        private async Task<bool> InvokeDetectProgressiveDisclosureAsync(LoginFormElements loginForm)
        {
            // Use reflection to access the private method
            var method = typeof(CredentialManager).GetMethod("DetectProgressiveDisclosureAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method == null)
                throw new InvalidOperationException("DetectProgressiveDisclosureAsync method not found");

            var task = (Task<bool>)method.Invoke(_credentialManager!, new object[] { _mockDriver!.Object, loginForm })!;
            return await task;
        }
    }
} 