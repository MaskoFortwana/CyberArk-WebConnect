using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenQA.Selenium;
using Microsoft.Extensions.Logging;
using WebConnect.Core;
using WebConnect.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Reflection;

namespace WebConnect.Tests.Detection
{
    [TestClass]
    public class SubmitButtonDetectionTests
    {
        private ILoggerFactory? _loggerFactory;
        private Mock<IWebDriver>? _mockDriver;
        private Mock<IJavaScriptExecutor>? _mockJsExecutor;
        private LoginDetector? _loginDetector;

        [TestInitialize]
        public void TestInitialize()
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _mockDriver = new Mock<IWebDriver>();
            _mockJsExecutor = new Mock<IJavaScriptExecutor>();

            // Setup driver to also implement IJavaScriptExecutor
            _mockDriver.As<IJavaScriptExecutor>();

            var logger = _loggerFactory.CreateLogger<LoginDetector>();
            _loginDetector = new LoginDetector(logger);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _loggerFactory?.Dispose();
        }

        [TestMethod]
        public void CalculateFastPathSubmitScore_CorrectSubmitButton_ReturnsHighScore()
        {
            // Arrange - This is the exact scenario from the user's issue
            var mockButton = new Mock<IWebElement>();
            mockButton.Setup(e => e.GetAttribute("id")).Returns("login_button_submit");
            mockButton.Setup(e => e.GetAttribute("data-testid")).Returns("login_button_submit");
            mockButton.Setup(e => e.GetAttribute("type")).Returns("submit");
            mockButton.Setup(e => e.GetAttribute("class")).Returns("p-element button__container login-base-auth-form__sign-in-button");
            mockButton.Setup(e => e.GetAttribute("name")).Returns("");
            mockButton.Setup(e => e.GetAttribute("value")).Returns("");
            mockButton.Setup(e => e.Text).Returns(""); // Simulate nested text issue
            mockButton.Setup(e => e.Displayed).Returns(false); // Simulate WebDriver visibility issue
            mockButton.Setup(e => e.Enabled).Returns(true);
            mockButton.Setup(e => e.TagName).Returns("button");

            // Setup JavaScript to return proper text content and visibility
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", mockButton.Object))
                .Returns("Sign In");

            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript(It.Is<string>(script => script.Contains("getComputedStyle")), mockButton.Object))
                .Returns(true); // Element is actually visible via JavaScript

            // Act
            var score = InvokeCalculateFastPathSubmitScore(mockButton.Object, _mockDriver!.Object);

            // Assert
            Assert.IsTrue(score > 5000, $"Button with correct attributes should have high score. Actual score: {score}");
            Assert.IsTrue(score > 810, $"Score should be higher than expected 810 points. Actual score: {score}");
        }

        [TestMethod]
        public void CalculateFastPathSubmitScore_StandardSubmitButton_ReturnsGoodScore()
        {
            // Arrange
            var mockButton = new Mock<IWebElement>();
            mockButton.Setup(e => e.GetAttribute("id")).Returns("submit");
            mockButton.Setup(e => e.GetAttribute("data-testid")).Returns("");
            mockButton.Setup(e => e.GetAttribute("type")).Returns("submit");
            mockButton.Setup(e => e.GetAttribute("class")).Returns("btn btn-primary");
            mockButton.Setup(e => e.GetAttribute("name")).Returns("");
            mockButton.Setup(e => e.GetAttribute("value")).Returns("");
            mockButton.Setup(e => e.Text).Returns("Submit");
            mockButton.Setup(e => e.Displayed).Returns(true);
            mockButton.Setup(e => e.Enabled).Returns(true);
            mockButton.Setup(e => e.TagName).Returns("button");

            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", mockButton.Object))
                .Returns("Submit");

            // Act
            var score = InvokeCalculateFastPathSubmitScore(mockButton.Object, _mockDriver!.Object);

            // Assert
            Assert.IsTrue(score > 1000, $"Standard submit button should have good score. Actual score: {score}");
        }

        [TestMethod]
        public void CalculateFastPathSubmitScore_AngularNestedButton_ExtractsTextCorrectly()
        {
            // Arrange - Angular button with nested span
            var mockButton = new Mock<IWebElement>();
            mockButton.Setup(e => e.GetAttribute("id")).Returns("login_btn");
            mockButton.Setup(e => e.GetAttribute("data-testid")).Returns("");
            mockButton.Setup(e => e.GetAttribute("type")).Returns("submit");
            mockButton.Setup(e => e.GetAttribute("class")).Returns("mat-raised-button");
            mockButton.Setup(e => e.GetAttribute("name")).Returns("");
            mockButton.Setup(e => e.GetAttribute("value")).Returns("");
            mockButton.Setup(e => e.Text).Returns(""); // Empty because text is in nested element
            mockButton.Setup(e => e.Displayed).Returns(true);
            mockButton.Setup(e => e.Enabled).Returns(true);
            mockButton.Setup(e => e.TagName).Returns("button");

            // Setup enhanced text extraction to return nested text
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", mockButton.Object))
                .Returns("Log In");

            // Act
            var score = InvokeCalculateFastPathSubmitScore(mockButton.Object, _mockDriver!.Object);

            // Assert
            Assert.IsTrue(score > 1000, $"Angular button with nested text should have good score. Actual score: {score}");
        }

        [TestMethod]
        public void CalculateFastPathSubmitScore_UtilityButton_ReturnsLowScore()
        {
            // Arrange - "Change authentication method" button that should be rejected
            var mockButton = new Mock<IWebElement>();
            mockButton.Setup(e => e.GetAttribute("id")).Returns("login-form-change-authentication");
            mockButton.Setup(e => e.GetAttribute("data-testid")).Returns("change-authentication-button");
            mockButton.Setup(e => e.GetAttribute("type")).Returns("");
            mockButton.Setup(e => e.GetAttribute("class")).Returns("login-form__change-authentication");
            mockButton.Setup(e => e.GetAttribute("name")).Returns("");
            mockButton.Setup(e => e.GetAttribute("value")).Returns("");
            mockButton.Setup(e => e.Text).Returns("Change authentication method");
            mockButton.Setup(e => e.Displayed).Returns(true);
            mockButton.Setup(e => e.Enabled).Returns(true);
            mockButton.Setup(e => e.TagName).Returns("button");

            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", mockButton.Object))
                .Returns("Change authentication method");

            // Act
            var score = InvokeCalculateFastPathSubmitScore(mockButton.Object, _mockDriver!.Object);

            // Assert
            Assert.IsTrue(score < 0, $"Utility button should have negative score. Actual score: {score}");
        }

        [TestMethod]
        public void CalculateFastPathSubmitScore_InputSubmitButton_ReturnsGoodScore()
        {
            // Arrange
            var mockInput = new Mock<IWebElement>();
            mockInput.Setup(e => e.GetAttribute("id")).Returns("loginSubmit");
            mockInput.Setup(e => e.GetAttribute("data-testid")).Returns("");
            mockInput.Setup(e => e.GetAttribute("type")).Returns("submit");
            mockInput.Setup(e => e.GetAttribute("class")).Returns("submit-btn");
            mockInput.Setup(e => e.GetAttribute("name")).Returns("submit");
            mockInput.Setup(e => e.GetAttribute("value")).Returns("Login");
            mockInput.Setup(e => e.Text).Returns("");
            mockInput.Setup(e => e.Displayed).Returns(true);
            mockInput.Setup(e => e.Enabled).Returns(true);
            mockInput.Setup(e => e.TagName).Returns("input");

            // Act
            var score = InvokeCalculateFastPathSubmitScore(mockInput.Object, _mockDriver!.Object);

            // Assert
            Assert.IsTrue(score > 5000, $"Input submit button should have high score. Actual score: {score}");
        }

        [TestMethod]
        public void EnhancedVisibilityDetection_WebDriverFalseJavaScriptTrue_ReturnsTrue()
        {
            // Arrange
            var mockButton = new Mock<IWebElement>();
            mockButton.Setup(e => e.Displayed).Returns(false); // WebDriver says not visible
            mockButton.Setup(e => e.Enabled).Returns(true);

            // JavaScript visibility check returns true
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript(It.Is<string>(script => script.Contains("getComputedStyle")), mockButton.Object))
                .Returns(true);

            // Act
            var isVisible = InvokeIsElementVisibleEnhanced(mockButton.Object, _mockDriver!.Object);

            // Assert
            Assert.IsTrue(isVisible, "Enhanced visibility detection should return true when JavaScript confirms visibility");
        }

        [TestMethod]
        public void EnhancedVisibilityDetection_WebDriverTrueJavaScriptFalse_ReturnsTrue()
        {
            // Arrange
            var mockButton = new Mock<IWebElement>();
            mockButton.Setup(e => e.Displayed).Returns(true); // WebDriver says visible
            mockButton.Setup(e => e.Enabled).Returns(true);

            // Act
            var isVisible = InvokeIsElementVisibleEnhanced(mockButton.Object, _mockDriver!.Object);

            // Assert
            Assert.IsTrue(isVisible, "Enhanced visibility detection should return true when WebDriver confirms visibility");
        }

        [TestMethod]
        public void EnhancedTextExtraction_NestedSpanContent_ExtractsCorrectly()
        {
            // Arrange
            var mockButton = new Mock<IWebElement>();
            mockButton.Setup(e => e.Text).Returns(""); // Standard WebDriver text extraction fails

            // Setup JavaScript textContent extraction to succeed
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", mockButton.Object))
                .Returns("Sign In");

            // Act
            var text = InvokeGetEnhancedButtonText(mockButton.Object, _mockDriver!.Object);

            // Assert
            Assert.AreEqual("sign in", text, "Enhanced text extraction should get text from nested elements");
        }

        [TestMethod]
        public void EnhancedTextExtraction_InnerHTMLFallback_ExtractsCorrectly()
        {
            // Arrange
            var mockButton = new Mock<IWebElement>();
            mockButton.Setup(e => e.Text).Returns(""); // Standard extraction fails

            // Setup textContent to return empty but innerHTML to have content
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", mockButton.Object))
                .Returns("");

            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].innerHTML || '';", mockButton.Object))
                .Returns("<span class=\"p-button-label\">Submit</span>");

            // Act
            var text = InvokeGetEnhancedButtonText(mockButton.Object, _mockDriver!.Object);

            // Assert
            Assert.AreEqual("submit", text, "Enhanced text extraction should extract text from innerHTML when textContent fails");
        }

        [TestMethod]
        public async Task FastPathDetection_WithCorrectButton_SelectsRightButton()
        {
            // Arrange
            var correctButton = new Mock<IWebElement>();
            correctButton.Setup(e => e.GetAttribute("id")).Returns("login_button_submit");
            correctButton.Setup(e => e.GetAttribute("data-testid")).Returns("login_button_submit");
            correctButton.Setup(e => e.GetAttribute("type")).Returns("submit");
            correctButton.Setup(e => e.GetAttribute("class")).Returns("login-button");
            correctButton.Setup(e => e.GetAttribute("name")).Returns("");
            correctButton.Setup(e => e.GetAttribute("value")).Returns("");
            correctButton.Setup(e => e.Text).Returns("");
            correctButton.Setup(e => e.Displayed).Returns(true);
            correctButton.Setup(e => e.Enabled).Returns(true);
            correctButton.Setup(e => e.TagName).Returns("button");

            var wrongButton = new Mock<IWebElement>();
            wrongButton.Setup(e => e.GetAttribute("id")).Returns("login-form-change-authentication");
            wrongButton.Setup(e => e.GetAttribute("data-testid")).Returns("change-authentication-button");
            wrongButton.Setup(e => e.GetAttribute("type")).Returns("");
            wrongButton.Setup(e => e.GetAttribute("class")).Returns("utility-button");
            wrongButton.Setup(e => e.GetAttribute("name")).Returns("");
            wrongButton.Setup(e => e.GetAttribute("value")).Returns("");
            wrongButton.Setup(e => e.Text).Returns("Change authentication method");
            wrongButton.Setup(e => e.Displayed).Returns(true);
            wrongButton.Setup(e => e.Enabled).Returns(true);
            wrongButton.Setup(e => e.TagName).Returns("button");

            var mockUsernameField = new Mock<IWebElement>();
            mockUsernameField.Setup(e => e.GetAttribute("type")).Returns("text");
            mockUsernameField.Setup(e => e.Displayed).Returns(true);
            mockUsernameField.Setup(e => e.TagName).Returns("input");

            var mockPasswordField = new Mock<IWebElement>();
            mockPasswordField.Setup(e => e.GetAttribute("type")).Returns("password");
            mockPasswordField.Setup(e => e.Displayed).Returns(true);
            mockPasswordField.Setup(e => e.TagName).Returns("input");

            // Setup driver to return elements in order: wrong button first, correct button second
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { mockPasswordField.Object }));

            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='text'], input[type='email'], input:not([type]), select[name*='user'], select[id*='user'], select[name*='login'], select[id*='login']")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { mockUsernameField.Object }));

            _mockDriver.Setup(d => d.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button")))
                      .Returns(new ReadOnlyCollection<IWebElement>(new List<IWebElement> { wrongButton.Object, correctButton.Object }));

            // Setup JavaScript calls for text extraction
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", correctButton.Object))
                .Returns("Sign In");

            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", wrongButton.Object))
                .Returns("Change authentication method");

            // Act
            var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver.Object, "none");

            // Assert
            Assert.IsNotNull(result, "Login form should be detected");
            Assert.IsNotNull(result.SubmitButton, "Submit button should be detected");
            Assert.AreEqual("login_button_submit", result.SubmitButton.GetAttribute("id"), "Should select the correct submit button, not the utility button");
        }

        [TestMethod]
        public void ButtonSelectionWithMultipleCandidates_SelectsHighestScoring()
        {
            // Arrange
            var highScoreButton = new Mock<IWebElement>();
            highScoreButton.Setup(e => e.GetAttribute("id")).Returns("login_button_submit");
            highScoreButton.Setup(e => e.GetAttribute("data-testid")).Returns("login_button_submit");
            highScoreButton.Setup(e => e.GetAttribute("type")).Returns("submit");
            highScoreButton.Setup(e => e.Text).Returns("Sign In");
            highScoreButton.Setup(e => e.Displayed).Returns(true);
            highScoreButton.Setup(e => e.TagName).Returns("button");

            var mediumScoreButton = new Mock<IWebElement>();
            mediumScoreButton.Setup(e => e.GetAttribute("id")).Returns("submit_btn");
            mediumScoreButton.Setup(e => e.GetAttribute("type")).Returns("submit");
            mediumScoreButton.Setup(e => e.Text).Returns("Submit");
            mediumScoreButton.Setup(e => e.Displayed).Returns(true);
            mediumScoreButton.Setup(e => e.TagName).Returns("button");

            var lowScoreButton = new Mock<IWebElement>();
            lowScoreButton.Setup(e => e.GetAttribute("id")).Returns("generic_button");
            lowScoreButton.Setup(e => e.Text).Returns("Click");
            lowScoreButton.Setup(e => e.Displayed).Returns(true);
            lowScoreButton.Setup(e => e.TagName).Returns("button");

            // Setup all buttons to return appropriate text
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", highScoreButton.Object))
                .Returns("Sign In");
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", mediumScoreButton.Object))
                .Returns("Submit");
            _mockDriver.As<IJavaScriptExecutor>()
                .Setup(js => js.ExecuteScript("return arguments[0].textContent || '';", lowScoreButton.Object))
                .Returns("Click");

            // Act - Get scores for all buttons
            var highScore = InvokeCalculateFastPathSubmitScore(highScoreButton.Object, _mockDriver!.Object);
            var mediumScore = InvokeCalculateFastPathSubmitScore(mediumScoreButton.Object, _mockDriver!.Object);
            var lowScore = InvokeCalculateFastPathSubmitScore(lowScoreButton.Object, _mockDriver!.Object);

            // Assert
            Assert.IsTrue(highScore > mediumScore, $"High score button ({highScore}) should score higher than medium score button ({mediumScore})");
            Assert.IsTrue(mediumScore > lowScore, $"Medium score button ({mediumScore}) should score higher than low score button ({lowScore})");
            Assert.IsTrue(highScore > 5000, $"High score button should have score > 5000, actual: {highScore}");
        }

        #region Helper Methods

        /// <summary>
        /// Helper method to invoke the private CalculateFastPathSubmitScore method
        /// </summary>
        private int InvokeCalculateFastPathSubmitScore(IWebElement element, IWebDriver driver)
        {
            var method = typeof(LoginDetector).GetMethod("CalculateFastPathSubmitScore", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (method == null)
                throw new InvalidOperationException("CalculateFastPathSubmitScore method not found");

            return (int)method.Invoke(_loginDetector!, new object[] { element, driver })!;
        }

        /// <summary>
        /// Helper method to invoke the private IsElementVisibleEnhanced method
        /// </summary>
        private bool InvokeIsElementVisibleEnhanced(IWebElement element, IWebDriver driver)
        {
            var method = typeof(LoginDetector).GetMethod("IsElementVisibleEnhanced", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (method == null)
                throw new InvalidOperationException("IsElementVisibleEnhanced method not found");

            return (bool)method.Invoke(_loginDetector!, new object[] { element, driver })!;
        }

        /// <summary>
        /// Helper method to invoke the private GetEnhancedButtonText method
        /// </summary>
        private string InvokeGetEnhancedButtonText(IWebElement element, IWebDriver driver)
        {
            var method = typeof(LoginDetector).GetMethod("GetEnhancedButtonText", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (method == null)
                throw new InvalidOperationException("GetEnhancedButtonText method not found");

            return (string)method.Invoke(_loginDetector!, new object[] { element, driver })!;
        }

        #endregion
    }
} 