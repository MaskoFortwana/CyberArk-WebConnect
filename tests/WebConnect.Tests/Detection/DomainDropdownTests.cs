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
            _mockDriver = new Mock<IWebDriver>();
            _mockUsernameElement = new Mock<IWebElement>();
            _mockPasswordElement = new Mock<IWebElement>();
            _mockDomainElement = new Mock<IWebElement>();
            _mockSubmitButton = new Mock<IWebElement>();

            // Setup logging
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));
            var logger = _loggerFactory.CreateLogger<LoginDetector>();

            // Setup driver basic properties
            _mockDriver.Setup(d => d.Url).Returns("https://test.example.com");
            _mockDriver.Setup(d => d.Title).Returns("Test Login Page");

            // Setup basic element properties for username field
            _mockUsernameElement.Setup(e => e.TagName).Returns("input");
            _mockUsernameElement.Setup(e => e.GetAttribute("type")).Returns("text");
            _mockUsernameElement.Setup(e => e.GetAttribute("id")).Returns("username");
            _mockUsernameElement.Setup(e => e.GetAttribute("name")).Returns("username");
            _mockUsernameElement.Setup(e => e.GetAttribute("placeholder")).Returns("");
            _mockUsernameElement.Setup(e => e.GetAttribute("class")).Returns("");
            _mockUsernameElement.Setup(e => e.GetAttribute("aria-label")).Returns("");
            _mockUsernameElement.Setup(e => e.GetAttribute("data-testid")).Returns("");
            _mockUsernameElement.Setup(e => e.GetAttribute("required")).Returns("");
            _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
            _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
            _mockUsernameElement.Setup(e => e.Text).Returns("");

            // Setup basic element properties for password field
            _mockPasswordElement.Setup(e => e.TagName).Returns("input");
            _mockPasswordElement.Setup(e => e.GetAttribute("type")).Returns("password");
            _mockPasswordElement.Setup(e => e.GetAttribute("id")).Returns("password");
            _mockPasswordElement.Setup(e => e.GetAttribute("name")).Returns("password");
            _mockPasswordElement.Setup(e => e.GetAttribute("placeholder")).Returns("");
            _mockPasswordElement.Setup(e => e.GetAttribute("class")).Returns("");
            _mockPasswordElement.Setup(e => e.GetAttribute("aria-label")).Returns("");
            _mockPasswordElement.Setup(e => e.GetAttribute("data-testid")).Returns("");
            _mockPasswordElement.Setup(e => e.GetAttribute("required")).Returns("");
            _mockPasswordElement.Setup(e => e.Displayed).Returns(true);
            _mockPasswordElement.Setup(e => e.Enabled).Returns(true);
            _mockPasswordElement.Setup(e => e.Text).Returns("");

            // Setup basic element properties for submit button
            _mockSubmitButton.Setup(e => e.TagName).Returns("button");
            _mockSubmitButton.Setup(e => e.GetAttribute("type")).Returns("submit");
            _mockSubmitButton.Setup(e => e.GetAttribute("id")).Returns("submit");
            _mockSubmitButton.Setup(e => e.GetAttribute("name")).Returns("submit");
            _mockSubmitButton.Setup(e => e.GetAttribute("class")).Returns("");
            _mockSubmitButton.Setup(e => e.GetAttribute("aria-label")).Returns("");
            _mockSubmitButton.Setup(e => e.GetAttribute("data-testid")).Returns("");
            _mockSubmitButton.Setup(e => e.Displayed).Returns(true);
            _mockSubmitButton.Setup(e => e.Enabled).Returns(true);
            _mockSubmitButton.Setup(e => e.Text).Returns("not-set");

            // Setup comprehensive CSS selector mocks (critical for fixing the failures)
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='password']"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockPasswordElement.Object }));
            
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='text']"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement> { _mockUsernameElement.Object }));
            
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='email']"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));
            
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='submit']"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));
            
            _mockDriver.Setup(d => d.FindElements(By.CssSelector("input[type='button']"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

            // Setup all TagName searches to prevent null reference exceptions
            _mockDriver.Setup(d => d.FindElements(By.TagName("a"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));
            
            _mockDriver.Setup(d => d.FindElements(By.TagName("form"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>()));

            // Create LoginDetector instance
            _loginDetector = new LoginDetector(logger);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _loggerFactory?.Dispose();
        }

        private void SetupDriverElementMocks(bool includeDomainElement = false, IWebElement? customDomainElement = null)
        {
            var inputElements = new List<IWebElement> { _mockUsernameElement.Object, _mockPasswordElement.Object };
            var selectElements = new List<IWebElement>();
            var buttonElements = new List<IWebElement> { _mockSubmitButton.Object };

            if (includeDomainElement)
            {
                var domainElement = customDomainElement ?? _mockDomainElement.Object;
                if (domainElement.TagName.ToLower() == "select")
                {
                    selectElements.Add(domainElement);
                }
                else
                {
                    inputElements.Add(domainElement);
                }
            }

            // Setup TagName-based element searches
            _mockDriver.Setup(d => d.FindElements(By.TagName("input"))).Returns(
                new ReadOnlyCollection<IWebElement>(inputElements));

            _mockDriver.Setup(d => d.FindElements(By.TagName("select"))).Returns(
                new ReadOnlyCollection<IWebElement>(selectElements));

            _mockDriver.Setup(d => d.FindElements(By.TagName("button"))).Returns(
                new ReadOnlyCollection<IWebElement>(buttonElements));
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
            _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("");
            _mockDomainElement.Setup(e => e.GetAttribute("aria-label")).Returns("");
            _mockDomainElement.Setup(e => e.GetAttribute("data-testid")).Returns("");
            _mockDomainElement.Setup(e => e.GetAttribute("type")).Returns("");
            _mockDomainElement.Setup(e => e.Displayed).Returns(true);
            _mockDomainElement.Setup(e => e.Enabled).Returns(true);
            _mockDomainElement.Setup(e => e.Text).Returns("");
            _mockDomainElement.Setup(e => e.FindElements(By.TagName("option"))).Returns(options);

            // Setup driver element mocks with domain element included
            SetupDriverElementMocks(includeDomainElement: true);

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

        [TestMethod]
        public async Task DomainDetection_WhenDomainIsNone_ShouldSkipDetection()
        {
            // Arrange - Setup a form with a detectable domain field
            var mockOption1 = new Mock<IWebElement>();
            mockOption1.Setup(o => o.Text).Returns("-- Select Domain --");
            mockOption1.Setup(o => o.GetAttribute("value")).Returns("");

            var mockOption2 = new Mock<IWebElement>();
            mockOption2.Setup(o => o.Text).Returns("example.com");
            mockOption2.Setup(o => o.GetAttribute("value")).Returns("example.com");

            var options = new ReadOnlyCollection<IWebElement>(new List<IWebElement>
            {
                mockOption1.Object,
                mockOption2.Object
            });

            _mockDomainElement.Setup(e => e.TagName).Returns("select");
            _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("class")).Returns("");
            _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("");
            _mockDomainElement.Setup(e => e.GetAttribute("aria-label")).Returns("");
            _mockDomainElement.Setup(e => e.GetAttribute("data-testid")).Returns("");
            _mockDomainElement.Setup(e => e.GetAttribute("type")).Returns("");
            _mockDomainElement.Setup(e => e.GetAttribute("required")).Returns("");
            _mockDomainElement.Setup(e => e.Displayed).Returns(true);
            _mockDomainElement.Setup(e => e.Enabled).Returns(true);
            _mockDomainElement.Setup(e => e.Text).Returns("");
            _mockDomainElement.Setup(e => e.FindElements(By.TagName("option"))).Returns(options);

            // Setup driver element mocks with domain element included (even though it should be skipped)
            SetupDriverElementMocks(includeDomainElement: true);

            // Act - Pass "none" as domain parameter to skip detection
            var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "none");

            // Assert
            Assert.IsNotNull(result, "Login form should be detected");
            Assert.IsNull(result.DomainField, "Domain field should NOT be detected when domain is 'none'");
            Assert.IsNotNull(result.UsernameField, "Username field should still be detected");
            Assert.IsNotNull(result.PasswordField, "Password field should still be detected");
            Assert.IsNotNull(result.SubmitButton, "Submit button should still be detected");
        }

        [TestMethod]
        public async Task DomainDetection_WhenDomainIsNoneCaseInsensitive_ShouldSkipDetection()
        {
            // Arrange - Test various case combinations of "none"
            var testCases = new[] { "none", "NONE", "None", "NoNe", "nOnE" };

            foreach (var domainValue in testCases)
            {
                // Setup a form with a detectable domain field for each test case
                var mockOption1 = new Mock<IWebElement>();
                mockOption1.Setup(o => o.Text).Returns("-- Select Domain --");
                
                var options = new ReadOnlyCollection<IWebElement>(new List<IWebElement> { mockOption1.Object });

                _mockDomainElement.Setup(e => e.TagName).Returns("select");
                _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain");
                _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
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

                // Act - Pass case variation of "none" as domain parameter
                var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, domainValue);

                // Assert
                Assert.IsNotNull(result, $"Login form should be detected for domain value '{domainValue}'");
                Assert.IsNull(result.DomainField, $"Domain field should NOT be detected when domain is '{domainValue}' (case-insensitive)");
                Assert.IsNotNull(result.UsernameField, $"Username field should still be detected for domain value '{domainValue}'");
                Assert.IsNotNull(result.PasswordField, $"Password field should still be detected for domain value '{domainValue}'");
            }
        }

        [TestMethod]
        public async Task DomainDetection_WhenDomainIsNotNone_ShouldPerformNormalDetection()
        {
            // Arrange - Setup a form with a detectable domain field
            var mockOption1 = new Mock<IWebElement>();
            mockOption1.Setup(o => o.Text).Returns("-- Select Domain --");
            mockOption1.Setup(o => o.GetAttribute("value")).Returns("");

            var mockOption2 = new Mock<IWebElement>();
            mockOption2.Setup(o => o.Text).Returns("company.com");
            mockOption2.Setup(o => o.GetAttribute("value")).Returns("company.com");

            var options = new ReadOnlyCollection<IWebElement>(new List<IWebElement>
            {
                mockOption1.Object,
                mockOption2.Object
            });

            _mockDomainElement.Setup(e => e.TagName).Returns("select");
            _mockDomainElement.Setup(e => e.GetAttribute("id")).Returns("domain");
            _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
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

            // Test with various regular domain values (not "none")
            var testDomainValues = new[] { "company", "example.com", "test", null, "", "domain123" };

            foreach (var domainValue in testDomainValues)
            {
                // Act - Pass regular domain parameter to perform normal detection
                var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, domainValue);

                // Assert - Domain field should be detected for all non-"none" values
                Assert.IsNotNull(result, $"Login form should be detected for domain value '{domainValue ?? "null"}'");
                Assert.IsNotNull(result.DomainField, $"Domain field SHOULD be detected when domain is '{domainValue ?? "null"}' (normal detection)");
                Assert.AreEqual("domain", result.DomainField.GetAttribute("id"), $"Should detect the correct domain field for domain value '{domainValue ?? "null"}'");
            }
        }

        [TestMethod]
        public async Task DomainDetection_PerformanceComparison_NoneVsNormal()
        {
            // Arrange - Setup a complex form with multiple domain-like elements to make detection slower
            var mockInputDomain = new Mock<IWebElement>();
            mockInputDomain.Setup(e => e.TagName).Returns("input");
            mockInputDomain.Setup(e => e.GetAttribute("id")).Returns("domain-input");
            mockInputDomain.Setup(e => e.GetAttribute("name")).Returns("domain");
            mockInputDomain.Setup(e => e.GetAttribute("type")).Returns("text");
            mockInputDomain.Setup(e => e.Displayed).Returns(true);
            mockInputDomain.Setup(e => e.Enabled).Returns(true);

            var mockSelectDomain = new Mock<IWebElement>();
            mockSelectDomain.Setup(e => e.TagName).Returns("select");
            mockSelectDomain.Setup(e => e.GetAttribute("id")).Returns("domain-select");
            mockSelectDomain.Setup(e => e.GetAttribute("name")).Returns("domain");
            mockSelectDomain.Setup(e => e.Displayed).Returns(true);
            mockSelectDomain.Setup(e => e.Enabled).Returns(true);
            mockSelectDomain.Setup(e => e.FindElements(By.TagName("option"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement> { new Mock<IWebElement>().Object }));

            // Setup driver with multiple elements that could slow down detection
            _mockDriver.Setup(d => d.FindElements(By.TagName("input"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockUsernameElement.Object,
                    _mockPasswordElement.Object,
                    mockInputDomain.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("select"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    mockSelectDomain.Object
                }));

            _mockDriver.Setup(d => d.FindElements(By.TagName("button"))).Returns(
                new ReadOnlyCollection<IWebElement>(new List<IWebElement>
                {
                    _mockSubmitButton.Object
                }));

            // Act & Assert - Measure time for "none" (should be faster)
            var stopwatchNone = System.Diagnostics.Stopwatch.StartNew();
            var resultNone = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "none");
            stopwatchNone.Stop();

            // Act & Assert - Measure time for normal detection (should be slower)
            var stopwatchNormal = System.Diagnostics.Stopwatch.StartNew();
            var resultNormal = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "company");
            stopwatchNormal.Stop();

            // Assert results
            Assert.IsNotNull(resultNone, "Form should be detected with domain='none'");
            Assert.IsNull(resultNone.DomainField, "Domain field should NOT be detected with domain='none'");

            Assert.IsNotNull(resultNormal, "Form should be detected with normal domain");
            Assert.IsNotNull(resultNormal.DomainField, "Domain field SHOULD be detected with normal domain");

            // Log performance comparison (the "none" case should typically be faster or equal)
            System.Console.WriteLine($"Detection time with domain='none': {stopwatchNone.ElapsedMilliseconds}ms");
            System.Console.WriteLine($"Detection time with normal domain: {stopwatchNormal.ElapsedMilliseconds}ms");
            
            // Note: In unit tests, performance improvement might not be significant due to mocking,
            // but this test verifies the optimization path is taken
        }
    }
} 
