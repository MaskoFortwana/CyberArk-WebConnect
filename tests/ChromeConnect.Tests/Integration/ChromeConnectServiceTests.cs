using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using OpenQA.Selenium;
using ChromeConnect.Core;
using ChromeConnect.Models;
using ChromeConnect.Services;
using ChromeConnect.Exceptions;

namespace ChromeConnect.Tests.Integration
{
    public class ChromeConnectServiceTests
    {
        private readonly Mock<ILogger<ChromeConnectService>> _mockServiceLogger;
        private readonly Mock<BrowserManager> _mockBrowserManager;
        private readonly Mock<LoginDetector> _mockLoginDetector;
        private readonly Mock<CredentialManager> _mockCredentialManager;
        private readonly Mock<LoginVerifier> _mockLoginVerifier;
        private readonly Mock<IScreenshotCapture> _mockScreenshotCapture;
        private readonly Mock<ErrorHandler> _mockErrorHandler;
        private readonly Mock<TimeoutManager> _mockTimeoutManager;
        private readonly Mock<ErrorMonitor> _mockErrorMonitor;
        private readonly ChromeConnectService _service;

        public ChromeConnectServiceTests()
        {
            _mockServiceLogger = new Mock<ILogger<ChromeConnectService>>();
            
            var mockBrowserManagerLogger = new Mock<ILogger<BrowserManager>>();
            _mockBrowserManager = new Mock<BrowserManager>(mockBrowserManagerLogger.Object);

            var mockLoginDetectorLogger = new Mock<ILogger<LoginDetector>>();
            _mockLoginDetector = new Mock<LoginDetector>(mockLoginDetectorLogger.Object);

            var mockCredentialManagerLogger = new Mock<ILogger<CredentialManager>>();
            _mockCredentialManager = new Mock<CredentialManager>(mockCredentialManagerLogger.Object);

            var mockLoginVerifierLogger = new Mock<ILogger<LoginVerifier>>();
            _mockLoginVerifier = new Mock<LoginVerifier>(mockLoginVerifierLogger.Object);

            var mockErrorHandlerLogger = new Mock<ILogger<ErrorHandler>>();
            var errorHandlerSettings = new ErrorHandlerSettings();
            _mockErrorHandler = new Mock<ErrorHandler>(mockErrorHandlerLogger.Object, _mockScreenshotCapture.Object, errorHandlerSettings);

            var mockTimeoutManagerLogger = new Mock<ILogger<TimeoutManager>>();
            var timeoutSettings = new TimeoutSettings();
            _mockTimeoutManager = new Mock<TimeoutManager>(mockTimeoutManagerLogger.Object, timeoutSettings);

            var mockErrorMonitorLogger = new Mock<ILogger<ErrorMonitor>>();
            var errorMonitorSettings = new ErrorMonitorSettings();
            _mockErrorMonitor = new Mock<ErrorMonitor>(mockErrorMonitorLogger.Object, errorMonitorSettings);

            _mockScreenshotCapture = new Mock<IScreenshotCapture>();

            _service = new ChromeConnectService(
                _mockServiceLogger.Object,
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

        [Fact]
        public async Task ExecuteAsync_SuccessfulLogin_ReturnsZero()
        {
            // Arrange
            var options = new CommandLineOptions { Url = "http://test.com", Username = "user", Password = "pass", Domain = "dom", CertString = "ignore" };
            var mockDriver = new Mock<IWebDriver>();

            _mockBrowserManager.Setup(b => b.LaunchBrowser(options.Url, options.Incognito, options.Kiosk, options.IgnoreCertErrors)).Returns(mockDriver.Object);
            _mockLoginDetector.Setup(d => d.DetectLoginFormAsync(mockDriver.Object)).ReturnsAsync(new LoginFormElements()); // Assuming LoginFormElements is a valid type
            _mockCredentialManager.Setup(c => c.EnterCredentialsAsync(mockDriver.Object, It.IsAny<LoginFormElements>(), options.Username, options.Password, options.Domain)).ReturnsAsync(true);
            _mockLoginVerifier.Setup(v => v.VerifyLoginSuccessAsync(mockDriver.Object)).ReturnsAsync(true);

            // Setup ErrorHandler to pass through the action
            _mockErrorHandler.Setup(eh => eh.ExecuteWithRetryAsync(
                It.IsAny<Func<Task<int>>>(), 
                mockDriver.Object, 
                It.IsAny<int?>(), // retryCount
                It.IsAny<int?>(), // retryDelayMs
                It.IsAny<Func<Exception, bool>?>(), // shouldRetryFunc
                It.IsAny<CancellationToken>() // cancellationToken
            ))
               .Returns<Func<Task<int>>, IWebDriver, int?, int?, Func<Exception, bool>?, CancellationToken>((action, driver, rc, rdm, srf, ct) => action());
            
            _mockErrorHandler.Setup(eh => eh.ExecuteWithErrorHandlingAsync(
                It.IsAny<Func<Task<IWebDriver>>>(), 
                It.IsAny<IWebDriver?>() // driver
            ))
                .Returns<Func<Task<IWebDriver>>, IWebDriver?>((action, drv) => action());

            // Setup TimeoutManager to pass through actions, now including CancellationToken
            _mockTimeoutManager.Setup(tm => tm.ExecuteWithTimeoutAsync(It.IsAny<Func<Task<LoginFormElements>>>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task<LoginFormElements>>, int?, string, CancellationToken>((action, timeout, opName, ct) => action());
            _mockTimeoutManager.Setup(tm => tm.ExecuteWithTimeoutAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task<bool>>, int?, string, CancellationToken>((action, timeout, opName, ct) => action());

            // Act
            var result = await _service.ExecuteAsync(options);

            // Assert
            Xunit.Assert.Equal(0, result);
            _mockServiceLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Login successful"))!,
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_LoginFails_ThrowsInvalidCredentialsExceptionAndReturnsOne()
        {
            // Arrange
            var options = new CommandLineOptions { Url = "http://test.com", Username = "user", Password = "wrong", Domain = "dom", CertString = "ignore" };
            var mockDriver = new Mock<IWebDriver>();

            _mockBrowserManager.Setup(b => b.LaunchBrowser(options.Url, options.Incognito, options.Kiosk, options.IgnoreCertErrors)).Returns(mockDriver.Object);
            _mockLoginDetector.Setup(d => d.DetectLoginFormAsync(mockDriver.Object)).ReturnsAsync(new LoginFormElements());
            _mockCredentialManager.Setup(c => c.EnterCredentialsAsync(mockDriver.Object, It.IsAny<LoginFormElements>(), options.Username, options.Password, options.Domain)).ReturnsAsync(true);
            _mockLoginVerifier.Setup(v => v.VerifyLoginSuccessAsync(mockDriver.Object)).ReturnsAsync(false); // Simulate login failure

            // Setup ErrorHandler for the retry block
            _mockErrorHandler.Setup(eh => eh.ExecuteWithRetryAsync(
                It.IsAny<Func<Task<int>>>(), 
                mockDriver.Object, 
                It.IsAny<int?>(), // retryCount
                It.IsAny<int?>(), // retryDelayMs
                It.IsAny<Func<Exception, bool>?>(), // shouldRetryFunc
                It.IsAny<CancellationToken>() // cancellationToken
            ))
                .Callback<Func<Task<int>>, IWebDriver, int?, int?, Func<Exception, bool>?, CancellationToken>(async (action, driver, rc, rdm, srf, ct) => 
                {
                    try { await action(); } 
                    catch (InvalidCredentialsException) { /* Expected from service logic */ throw; } 
                })
                .ThrowsAsync(new InvalidCredentialsException("Simulated from test setup")); 
            
            _mockErrorHandler.Setup(eh => eh.ExecuteWithErrorHandlingAsync(
                It.IsAny<Func<Task<IWebDriver>>>(), 
                It.IsAny<IWebDriver?>() // driver
            ))
                .Returns<Func<Task<IWebDriver>>, IWebDriver?>((action, drv) => action());

            // Setup TimeoutManager, now including CancellationToken
            _mockTimeoutManager.Setup(tm => tm.ExecuteWithTimeoutAsync(It.IsAny<Func<Task<LoginFormElements>>>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task<LoginFormElements>>, int?, string, CancellationToken>((action, timeout, opName, ct) => action());
            _mockTimeoutManager.Setup(tm => tm.ExecuteWithTimeoutAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task<bool>>, int?, string, CancellationToken>((action, timeout, opName, ct) => action());

            // Act
            var result = await _service.ExecuteAsync(options);

            // Assert
            Xunit.Assert.Equal(1, result); // Expect exit code 1 for login failure
             _mockErrorHandler.Verify(eh => eh.HandleExceptionAsync(It.IsAny<InvalidCredentialsException>(), mockDriver.Object), Times.Never); // Error should be caught and exit code returned, not re-thrown to top handler
            _mockServiceLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Login failed"))!,
                    null, // Exception is handled internally by ErrorHandler within ExecuteAsync
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
             _mockScreenshotCapture.Verify(s => s.CaptureScreenshot(mockDriver.Object, "LoginFailed"), Times.Once); // Verify screenshot on login failure
        }
    }
} 