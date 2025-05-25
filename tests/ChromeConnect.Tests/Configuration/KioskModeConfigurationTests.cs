using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using ChromeConnect.Core;
using ChromeConnect.Services;
using ChromeConnect.Models;
using Moq;
using System.Threading.Tasks;

namespace ChromeConnect.Tests.Configuration;

[TestClass]
public class KioskModeConfigurationTests
{
    private Mock<IBrowserManager>? _mockBrowserManager;
    private Mock<ILoginDetector>? _mockLoginDetector;
    private Mock<ICredentialManager>? _mockCredentialManager;
    private Mock<ILoginVerifier>? _mockLoginVerifier;
    private Mock<IScreenshotCapture>? _mockScreenshotCapture;
    private Mock<IErrorHandler>? _mockErrorHandler;
    private Mock<ITimeoutManager>? _mockTimeoutManager;
    private Mock<IErrorMonitor>? _mockErrorMonitor;
    private ChromeConnectService? _chromeConnectService;
    private ILoggerFactory? _loggerFactory;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        _mockBrowserManager = new Mock<IBrowserManager>();
        _mockLoginDetector = new Mock<ILoginDetector>();
        _mockCredentialManager = new Mock<ICredentialManager>();
        _mockLoginVerifier = new Mock<ILoginVerifier>();
        _mockScreenshotCapture = new Mock<IScreenshotCapture>();
        _mockErrorHandler = new Mock<IErrorHandler>();
        _mockTimeoutManager = new Mock<ITimeoutManager>();
        _mockErrorMonitor = new Mock<IErrorMonitor>();

        // Setup default behaviors for mocks to avoid null reference exceptions
        _mockBrowserManager.Setup(b => b.LaunchBrowser(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns((string url, bool incognito, bool kiosk, bool ignoreCert) => null); // Return null IWebDriver

        _mockLoginDetector.Setup(d => d.DetectLoginElementsAsync(It.IsAny<OpenQA.Selenium.IWebDriver>()))
            .ReturnsAsync(new LoginFormElements());

        _mockCredentialManager.Setup(c => c.EnterCredentialsAsync(It.IsAny<OpenQA.Selenium.IWebDriver>(), It.IsAny<LoginFormElements>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockLoginVerifier.Setup(v => v.VerifyLoginSuccessAsync(It.IsAny<OpenQA.Selenium.IWebDriver>()))
            .ReturnsAsync(true);
            
        _mockErrorHandler.Setup(e => e.HandleErrorAsync(It.IsAny<System.Exception>(), It.IsAny<string>(), It.IsAny<OpenQA.Selenium.IWebDriver?>()))
            .ReturnsAsync((System.Exception ex, string ctx, OpenQA.Selenium.IWebDriver? wd) => { /* Simulate handling */ });


        _chromeConnectService = new ChromeConnectService(
            _loggerFactory.CreateLogger<ChromeConnectService>(),
            _mockBrowserManager.Object,
            _mockLoginDetector.Object,
            _mockCredentialManager.Object,
            _mockLoginVerifier.Object,
            _mockScreenshotCapture.Object,
            _mockErrorHandler.Object,
            _mockTimeoutManager.Object,
            _mockErrorMonitor.Object
        );
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _loggerFactory?.Dispose();
    }

    [TestMethod]
    public async Task KioskMode_SetToNo_ShouldPassFalseToBrowserManager()
    {
        // Arrange
        var commandLineOptions = new CommandLineOptions
        {
            Url = "http://test.com",
            Username = "user",
            Password = "pass",
            Domain = "domain",
            KioskString = "no", // Explicitly "no"
            IncognitoString = "no",
            CertString = "ignore"
        };

        // Act
        await _chromeConnectService!.ExecuteAsync(commandLineOptions);

        // Assert
        _mockBrowserManager!.Verify(b => b.LaunchBrowser(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            false, // Expect kiosk to be false
            It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    public async Task KioskMode_SetToYes_ShouldPassTrueToBrowserManager()
    {
        // Arrange
        var commandLineOptions = new CommandLineOptions
        {
            Url = "http://test.com",
            Username = "user",
            Password = "pass",
            Domain = "domain",
            KioskString = "yes", // Explicitly "yes"
            IncognitoString = "no",
            CertString = "ignore"
        };

        // Act
        await _chromeConnectService!.ExecuteAsync(commandLineOptions);

        // Assert
        _mockBrowserManager!.Verify(b => b.LaunchBrowser(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            true, // Expect kiosk to be true
            It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    public async Task KioskMode_SetToInvalid_ShouldPassFalseToBrowserManager()
    {
        // Arrange (YesNoParser defaults to false for invalid strings)
        var commandLineOptions = new CommandLineOptions
        {
            Url = "http://test.com",
            Username = "user",
            Password = "pass",
            Domain = "domain",
            KioskString = "maybe", // Invalid value, should default to false
            IncognitoString = "no",
            CertString = "ignore"
        };

        // Act
        await _chromeConnectService!.ExecuteAsync(commandLineOptions);

        // Assert
        _mockBrowserManager!.Verify(b => b.LaunchBrowser(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            false, // Expect kiosk to be false (default for invalid)
            It.IsAny<bool>()), Times.Once);
    }
} 