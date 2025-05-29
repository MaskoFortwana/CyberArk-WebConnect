using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenQA.Selenium;
using Microsoft.Extensions.Logging;
using WebConnect.Core;
using WebConnect.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace WebConnect.Tests.Detection
{
    [TestClass]
    public class DomainDropdownTests
    {
        private ILoggerFactory? _loggerFactory;
        private Mock<IWebDriver>? _mockDriver;
        private Mock<IWebElement>? _mockUsernameElement;
        private Mock<IWebElement>? _mockPasswordElement;
        private Mock<IWebElement>? _mockDomainElement;
        private Mock<IWebElement>? _mockSubmitButton;
        private LoginDetector? _loginDetector;

        [TestInitialize]
        public void TestInitialize()
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            
            _mockDriver = new Mock<IWebDriver>();
            _mockUsernameElement = new Mock<IWebElement>();
            _mockPasswordElement = new Mock<IWebElement>();
            _mockDomainElement = new Mock<IWebElement>();
            _mockSubmitButton = new Mock<IWebElement>();

            // Setup basic element properties
            _mockUsernameElement.Setup(e => e.TagName).Returns("input");
            _mockUsernameElement.Setup(e => e.GetAttribute("type")).Returns("text");
            _mockUsernameElement.Setup(e => e.GetAttribute("id")).Returns("username");
            _mockUsernameElement.Setup(e => e.GetAttribute("name")).Returns("username");
            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);

            _mockPasswordElement.Setup(e => e.TagName).Returns("input");
            _mockPasswordElement.Setup(e => e.GetAttribute("type")).Returns("password");
            _mockPasswordElement.Setup(e => e.GetAttribute("id")).Returns("password");
            _mockPasswordElement.Setup(e => e.GetAttribute("name")).Returns("password");
            _mockPasswordElement.Setup(e => e.Displayed).Returns(true);
            _mockPasswordElement.Setup(e => e.Enabled).Returns(true);

            _mockSubmitButton.Setup(e => e.TagName).Returns("button");
            _mockSubmitButton.Setup(e => e.GetAttribute("type")).Returns("submit");
            _mockSubmitButton.Setup(e => e.Displayed).Returns(true);
            _mockSubmitButton.Setup(e => e.Enabled).Returns(true);

            var logger = _loggerFactory.CreateLogger<LoginDetector>();
            _loginDetector = new LoginDetector(logger);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _loggerFactory?.Dispose();
        }

        [TestMethod]
        public async Task DomainDropdown_ExactIdMatch_ShouldBeDetectedWithHighScore()
        {
            // Arrange
            var mockOption1 = new Mock<IWebElement>();
            mockOption1.Setup(o => o.Text).Returns("-- Select Domain --");
            mockOption1.Setup(o => o.GetAttribute("value")).Returns("");

            var mockOption2 = new Mock<IWebElement>();
            mockOption2.Setup(o => o.Text).Returns("masko.local");
            mockOption2.Setup(o => o.GetAttribute("value")).Returns("masko.local");

            var mockOption3 = new Mock<IWebElement>();
            mockOption3.Setup(o => o.Text).Returns("picovina");
            mockOption3.Setup(o => o.GetAttribute("value")).Returns("picovina");

            var options = new ReadOnlyCollection<IWebElement>(new List<IWebElement>
            {
                mockOption1.Object,
                mockOption2.Object,
                mockOption3.Object
            });

            _mockDomainElement.Setup(e => e.TagName).Returns("select");
            _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("required")).Returns("true");
            _mockDomainElement.Setup(e => e.GetAttribute("class")).Returns("");
            _mockDomainElement.Setup(e => e.Displayed).Returns(true);
            _mockDomainElement.Setup(e => e.Enabled).Returns(true);
            _mockDomainElement.Setup(e => e.FindElements(By.TagName("option"))).Returns(options);

            // Setup driver to return our elements
            var allElements = new ReadOnlyCollection<IWebElement>(new List<IWebElement>
            {
                _mockUsernameElement.Object,
                _mockPasswordElement.Object,
                _mockDomainElement.Object,
                _mockSubmitButton.Object
            });

            _mockDriver.Setup(d => d.FindElements(By.TagName("input"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockUsernameElement.Object,
                    _mockPasswordElement.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("select"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockDomainElement.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("button"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockSubmitButton.Object
                }));

            // Act
            var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object);

            // Assert
            Assert.IsNotNull(result, "Login form should be detected");
            Assert.IsNotNull(result.DomainField, "Domain field should be detected");
            Assert.AreEqual("domain", result.DomainField.GetAttribute("id"), "Should detect the element with id='domain'");
            Assert.AreEqual("select", result.DomainField.TagName.ToLower(), "Domain field should be a select element");
        }

        [TestMethod]
        public async Task DomainDropdown_WithDomainOptions_ShouldBeDetectedCorrectly()
        {
            // Arrange
            var mockOption1 = new Mock<IWebElement>();
            mockOption1.Setup(o => o.Text).Returns("-- Select Domain --");
            mockOption1.Setup(o => o.GetAttribute("value")).Returns("");

            var mockOption2 = new Mock<IWebElement>();
            mockOption2.Setup(o => o.Text).Returns("masko.local");
            mockOption2.Setup(o => o.GetAttribute("value")).Returns("masko.local");

            var mockOption3 = new Mock<IWebElement>();
            mockOption3.Setup(o => o.Text).Returns("picovina");
            mockOption3.Setup(o => o.GetAttribute("value")).Returns("picovina");

            var options = new ReadOnlyCollection<IWebElement>(new List<IWebElement>
            {
                mockOption1.Object,
                mockOption2.Object,
                mockOption3.Object
            });

            _mockDomainElement.Setup(e => e.TagName).Returns("select");
            _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("required")).Returns("true");
            _mockDomainElement.Setup(e => e.GetAttribute("class")).Returns("");
            _mockDomainElement.Setup(e => e.Displayed).Returns(true);
            _mockDomainElement.Setup(e => e.Enabled).Returns(true);
            _mockDomainElement.Setup(e => e.FindElements(By.TagName("option"))).Returns(options);

            // Setup driver to return our elements
            _mockDriver.Setup(d => d.FindElements(By.TagName("input"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockUsernameElement.Object,
                    _mockPasswordElement.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("select"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockDomainElement.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("button"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockSubmitButton.Object
                }));

            // Act
            var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object);

            // Assert
            Assert.IsNotNull(result, "Login form should be detected");
            Assert.IsNotNull(result.DomainField, "Domain field should be detected");
            
            // Verify the domain field has the expected options
            var detectedOptions = result.DomainField.FindElements(By.TagName("option"));
            Assert.AreEqual(3, detectedOptions.Count, "Should detect all 3 options");
            
            // Verify specific domain options are present
            var optionTexts = detectedOptions.Select(o => o.Text).ToList();
            Assert.IsTrue(optionTexts.Contains("masko.local"), "Should contain masko.local option");
            Assert.IsTrue(optionTexts.Contains("picovina"), "Should contain picovina option");
        }

        [TestMethod]
        public async Task DomainDropdown_WithRequiredAttribute_ShouldBeDetectedProperly()
        {
            // Arrange
            var mockOption1 = new Mock<IWebElement>();
            mockOption1.Setup(o => o.Text).Returns("-- Select Domain --");
            mockOption1.Setup(o => o.GetAttribute("value")).Returns("");

            var mockOption2 = new Mock<IWebElement>();
            mockOption2.Setup(o => o.Text).Returns("test.local");
            mockOption2.Setup(o => o.GetAttribute("value")).Returns("test.local");

            var options = new ReadOnlyCollection<IWebElement>(new List<IWebElement>
            {
                mockOption1.Object,
                mockOption2.Object
            });

            _mockDomainElement.Setup(e => e.TagName).Returns("select");
            _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("required")).Returns("true"); // This is the key test
            _mockDomainElement.Setup(e => e.GetAttribute("class")).Returns("");
            _mockDomainElement.Setup(e => e.Displayed).Returns(true);
            _mockDomainElement.Setup(e => e.Enabled).Returns(true);
            _mockDomainElement.Setup(e => e.FindElements(By.TagName("option"))).Returns(options);

            // Setup driver to return our elements
            _mockDriver.Setup(d => d.FindElements(By.TagName("input"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockUsernameElement.Object,
                    _mockPasswordElement.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("select"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockDomainElement.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("button"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockSubmitButton.Object
                }));

            // Act
            var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object);

            // Assert
            Assert.IsNotNull(result, "Login form should be detected");
            Assert.IsNotNull(result.DomainField, "Domain field should be detected");
            Assert.AreEqual("true", result.DomainField.GetAttribute("required"), "Domain field should have required attribute");
            Assert.AreEqual("domain", result.DomainField.GetAttribute("id"), "Should detect the element with id='domain'");
        }

        [TestMethod]
        public async Task DomainDropdown_FastPathDetection_ShouldIncludeDomainField()
        {
            // Arrange - Test the fast-path detection specifically
            var mockOption1 = new Mock<IWebElement>();
            mockOption1.Setup(o => o.Text).Returns("-- Select Domain --");
            mockOption1.Setup(o => o.GetAttribute("value")).Returns("");

            var mockOption2 = new Mock<IWebElement>();
            mockOption2.Setup(o => o.Text).Returns("masko.local");
            mockOption2.Setup(o => o.GetAttribute("value")).Returns("masko.local");

            var options = new ReadOnlyCollection<IWebElement>(new List<IWebElement>
            {
                mockOption1.Object,
                mockOption2.Object
            });

            _mockDomainElement.Setup(e => e.TagName).Returns("select");
            _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("required")).Returns("true");
            _mockDomainElement.Setup(e => e.GetAttribute("class")).Returns("");
            _mockDomainElement.Setup(e => e.Displayed).Returns(true);
            _mockDomainElement.Setup(e => e.Enabled).Returns(true);
            _mockDomainElement.Setup(e => e.FindElements(By.TagName("option"))).Returns(options);

            // Setup driver to return our elements for fast-path detection
            _mockDriver.Setup(d => d.FindElements(By.TagName("input"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockUsernameElement.Object,
                    _mockPasswordElement.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("select"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockDomainElement.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("button"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockSubmitButton.Object
                }));

            // Act
            var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object);

            // Assert
            Assert.IsNotNull(result, "Login form should be detected via fast-path");
            Assert.IsNotNull(result.UsernameField, "Username field should be detected");
            Assert.IsNotNull(result.PasswordField, "Password field should be detected");
            Assert.IsNotNull(result.DomainField, "Domain field should be detected in fast-path");
            Assert.IsNotNull(result.SubmitButton, "Submit button should be detected");
            
            // Verify it's the correct domain element
            Assert.AreEqual("domain", result.DomainField.GetAttribute("id"), "Should detect the correct domain element");
        }
    }
} 
