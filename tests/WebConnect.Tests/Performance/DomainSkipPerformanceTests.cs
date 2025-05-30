using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebConnect.Core;
using WebConnect.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OpenQA.Selenium;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace WebConnect.Tests.Performance;

[TestClass]
public class DomainSkipPerformanceTests
{
    private ILoggerFactory? _loggerFactory;
    private Mock<IWebDriver>? _mockDriver;
    private Mock<IWebElement>? _mockUsernameElement;
    private Mock<IWebElement>? _mockPasswordElement;
    private Mock<IWebElement>? _mockDomainElement;
    private Mock<IWebElement>? _mockSubmitButton;
    private LoginDetector? _loginDetector;
    private CredentialManager? _credentialManager;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerFactory = LoggerFactory.Create(builder => 
        {
            builder
                .AddFilter("Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning)
                .AddFilter("System", Microsoft.Extensions.Logging.LogLevel.Warning)
                .AddFilter("WebConnect", Microsoft.Extensions.Logging.LogLevel.Information)
                .AddConsole();
        });

        // Setup mock WebDriver and elements with realistic delays
        _mockDriver = new Mock<IWebDriver>();
        _mockUsernameElement = new Mock<IWebElement>();
        _mockPasswordElement = new Mock<IWebElement>();
        _mockDomainElement = new Mock<IWebElement>();
        _mockSubmitButton = new Mock<IWebElement>();

        // Simulate DOM query delays that would occur in real browser
        _mockDriver.Setup(d => d.FindElements(It.IsAny<By>()))
               .Returns(() => 
               {
                   // Simulate DOM query time (this is what we're optimizing away)
                   Task.Delay(50).Wait(); 
                   return new List<IWebElement> { _mockDomainElement!.Object }.AsReadOnly();
               });

        // Configure mock elements with realistic interaction delays
        _mockUsernameElement.Setup(e => e.SendKeys(It.IsAny<string>()))
                          .Callback(() => Task.Delay(10).Wait());
        _mockPasswordElement.Setup(e => e.SendKeys(It.IsAny<string>()))
                          .Callback(() => Task.Delay(10).Wait());
        _mockDomainElement.Setup(e => e.SendKeys(It.IsAny<string>()))
                        .Callback(() => Task.Delay(10).Wait());
        
        _mockUsernameElement.Setup(e => e.Clear())
                          .Callback(() => Task.Delay(5).Wait());
        _mockPasswordElement.Setup(e => e.Clear())
                          .Callback(() => Task.Delay(5).Wait());
        _mockDomainElement.Setup(e => e.Clear())
                        .Callback(() => Task.Delay(5).Wait());

        _mockUsernameElement.Setup(e => e.Displayed).Returns(true);
        _mockPasswordElement.Setup(e => e.Displayed).Returns(true);
        _mockDomainElement.Setup(e => e.Displayed).Returns(true);

        _mockUsernameElement.Setup(e => e.Enabled).Returns(true);
        _mockPasswordElement.Setup(e => e.Enabled).Returns(true);
        _mockDomainElement.Setup(e => e.Enabled).Returns(true);

        // Setup domain element attributes to trigger detection logic
        _mockDomainElement.Setup(e => e.GetAttribute("type")).Returns("text");
        _mockDomainElement.Setup(e => e.GetAttribute("name")).Returns("domain");
        _mockDomainElement.Setup(e => e.GetAttribute("placeholder")).Returns("Domain");

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
    public async Task LoginDetector_DomainNone_ShouldBeSignificantlyFaster()
    {
        const int iterations = 10;
        var timesWithDomainDetection = new List<long>();
        var timesWithDomainSkip = new List<long>();

        // Measure time WITH domain detection (regular domain value)
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "testdomain");
            stopwatch.Stop();
            timesWithDomainDetection.Add(stopwatch.ElapsedMilliseconds);
        }

        // Measure time WITHOUT domain detection (domain = "none")
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "none");
            stopwatch.Stop();
            timesWithDomainSkip.Add(stopwatch.ElapsedMilliseconds);
        }

        // Calculate averages
        var avgWithDetection = timesWithDomainDetection.Average();
        var avgWithSkip = timesWithDomainSkip.Average();
        var timeSaved = avgWithDetection - avgWithSkip;
        var percentImprovement = (timeSaved / avgWithDetection) * 100;

        // Log results
        TestContext?.WriteLine($"Average time WITH domain detection: {avgWithDetection:F2}ms");
        TestContext?.WriteLine($"Average time WITHOUT domain detection (--DOM none): {avgWithSkip:F2}ms");
        TestContext?.WriteLine($"Time saved: {timeSaved:F2}ms");
        TestContext?.WriteLine($"Performance improvement: {percentImprovement:F1}%");

        // Assert significant performance improvement
        Assert.IsTrue(timeSaved > 0, 
            $"Domain skip should save time. With detection: {avgWithDetection:F2}ms, With skip: {avgWithSkip:F2}ms");
        Assert.IsTrue(percentImprovement > 10, 
            $"Should achieve at least 10% performance improvement, actual: {percentImprovement:F1}%");
    }

    [TestMethod]
    public async Task CredentialManager_DomainNone_ShouldAvoidDomainFieldOperations()
    {
        const int iterations = 10;
        var timesWithDomainEntry = new List<long>();
        var timesWithDomainSkip = new List<long>();

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object,
            SubmitButton = _mockSubmitButton!.Object
        };

        // Measure time WITH domain entry (regular domain value)
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
                "testuser", "testpass", "testdomain");
            stopwatch.Stop();
            timesWithDomainEntry.Add(stopwatch.ElapsedMilliseconds);
        }

        // Measure time WITHOUT domain entry (domain = "none")
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await _credentialManager!.EnterCredentialsAsync(_mockDriver!.Object, loginForm, 
                "testuser", "testpass", "none");
            stopwatch.Stop();
            timesWithDomainSkip.Add(stopwatch.ElapsedMilliseconds);
        }

        // Calculate averages
        var avgWithDomainEntry = timesWithDomainEntry.Average();
        var avgWithDomainSkip = timesWithDomainSkip.Average();
        var timeSaved = avgWithDomainEntry - avgWithDomainSkip;
        var percentImprovement = (timeSaved / avgWithDomainEntry) * 100;

        // Log results
        TestContext?.WriteLine($"Average time WITH domain entry: {avgWithDomainEntry:F2}ms");
        TestContext?.WriteLine($"Average time WITHOUT domain entry (--DOM none): {avgWithDomainSkip:F2}ms");
        TestContext?.WriteLine($"Time saved: {timeSaved:F2}ms");
        TestContext?.WriteLine($"Performance improvement: {percentImprovement:F1}%");

        // Assert measurable performance improvement
        Assert.IsTrue(timeSaved >= 0, 
            $"Domain skip should not be slower. With entry: {avgWithDomainEntry:F2}ms, With skip: {avgWithDomainSkip:F2}ms");
        
        // Verify domain field was never touched when domain="none"
        _mockDomainElement.Verify(e => e.Clear(), Times.Never);
        _mockDomainElement.Verify(e => e.SendKeys(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task EndToEnd_DomainNone_ShouldReduceTotalExecutionTime()
    {
        const int iterations = 5;
        var timesWithDomainProcessing = new List<long>();
        var timesWithDomainSkip = new List<long>();

        var loginForm = new LoginFormElements
        {
            UsernameField = _mockUsernameElement!.Object,
            PasswordField = _mockPasswordElement!.Object,
            DomainField = _mockDomainElement!.Object,
            SubmitButton = _mockSubmitButton!.Object
        };

        // Measure complete flow WITH domain processing
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate complete domain processing flow
            var detectionResult = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "testdomain");
            var credentialResult = await _credentialManager!.EnterCredentialsAsync(_mockDriver.Object, loginForm, 
                "testuser", "testpass", "testdomain");
            
            stopwatch.Stop();
            timesWithDomainProcessing.Add(stopwatch.ElapsedMilliseconds);
        }

        // Measure complete flow WITHOUT domain processing (domain = "none")
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate complete domain skip flow
            var detectionResult = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "none");
            var credentialResult = await _credentialManager!.EnterCredentialsAsync(_mockDriver.Object, loginForm, 
                "testuser", "testpass", "none");
            
            stopwatch.Stop();
            timesWithDomainSkip.Add(stopwatch.ElapsedMilliseconds);
        }

        // Calculate averages
        var avgWithDomainProcessing = timesWithDomainProcessing.Average();
        var avgWithDomainSkip = timesWithDomainSkip.Average();
        var totalTimeSaved = avgWithDomainProcessing - avgWithDomainSkip;
        var totalPercentImprovement = (totalTimeSaved / avgWithDomainProcessing) * 100;

        // Log results
        TestContext?.WriteLine($"Average total time WITH domain processing: {avgWithDomainProcessing:F2}ms");
        TestContext?.WriteLine($"Average total time WITHOUT domain processing (--DOM none): {avgWithDomainSkip:F2}ms");
        TestContext?.WriteLine($"Total time saved: {totalTimeSaved:F2}ms");
        TestContext?.WriteLine($"Total performance improvement: {totalPercentImprovement:F1}%");

        // Assert meaningful performance improvement for end-to-end flow
        Assert.IsTrue(totalTimeSaved > 0, 
            $"Domain skip should reduce total execution time. With processing: {avgWithDomainProcessing:F2}ms, With skip: {avgWithDomainSkip:F2}ms");
        Assert.IsTrue(totalPercentImprovement > 5, 
            $"Should achieve at least 5% total performance improvement, actual: {totalPercentImprovement:F1}%");
    }

    [TestMethod]
    public async Task ConsistencyTest_DomainNone_ShouldHaveConsistentBehavior()
    {
        const int iterations = 20;
        var executionTimes = new List<long>();

        // Test consistency of domain skip behavior
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _loginDetector!.DetectLoginFormAsync(_mockDriver!.Object, "none");
            stopwatch.Stop();
            
            executionTimes.Add(stopwatch.ElapsedMilliseconds);
            
            // Verify consistent behavior
            Assert.IsNotNull(result, $"Iteration {i + 1}: LoginDetector should return a result");
            Assert.IsNull(result.DomainField, $"Iteration {i + 1}: Domain field should be null when domain is 'none'");
        }

        // Calculate variance to ensure consistent performance
        var avgTime = executionTimes.Average();
        var variance = executionTimes.Select(t => Math.Pow(t - avgTime, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);
        var coefficientOfVariation = (standardDeviation / avgTime) * 100;

        TestContext?.WriteLine($"Average execution time: {avgTime:F2}ms");
        TestContext?.WriteLine($"Standard deviation: {standardDeviation:F2}ms");
        TestContext?.WriteLine($"Coefficient of variation: {coefficientOfVariation:F1}%");

        // Assert consistent performance (low variance)
        Assert.IsTrue(coefficientOfVariation < 50, 
            $"Domain skip should have consistent performance. Coefficient of variation: {coefficientOfVariation:F1}%");
    }

    public TestContext? TestContext { get; set; }
} 