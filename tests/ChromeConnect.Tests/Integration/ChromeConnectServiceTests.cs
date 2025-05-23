using System;
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
        private readonly Mock<ILogger<ChromeConnectService>> _loggerMock;
        private readonly Mock<BrowserManager> _browserManagerMock;
        private readonly Mock<LoginDetector> _loginDetectorMock;
        private readonly Mock<CredentialManager> _credentialManagerMock;
        private readonly Mock<LoginVerifier> _loginVerifierMock;
        private readonly Mock<IScreenshotCapture> _screenshotCaptureMock;
        private readonly Mock<ErrorHandler> _errorHandlerMock;
        private readonly Mock<TimeoutManager> _timeoutManagerMock;
        private readonly Mock<ErrorMonitor> _errorMonitorMock;
        private readonly Mock<IWebDriver> _webDriverMock;

        public ChromeConnectServiceTests()
        {
            _loggerMock = new Mock<ILogger<ChromeConnectService>>();
            _browserManagerMock = new Mock<BrowserManager>(Mock.Of<ILogger<BrowserManager>>());
            _loginDetectorMock = new Mock<LoginDetector>(Mock.Of<ILogger<LoginDetector>>());
            _credentialManagerMock = new Mock<CredentialManager>(Mock.Of<ILogger<CredentialManager>>());
            _loginVerifierMock = new Mock<LoginVerifier>(Mock.Of<ILogger<LoginVerifier>>());
            _screenshotCaptureMock = new Mock<IScreenshotCapture>();
            _errorHandlerMock = new Mock<ErrorHandler>(
                Mock.Of<ILogger<ErrorHandler>>(),
                _screenshotCaptureMock.Object,
                new ErrorHandlerSettings());
            _timeoutManagerMock = new Mock<TimeoutManager>(
                Mock.Of<ILogger<TimeoutManager>>(),
                new TimeoutSettings());
            _errorMonitorMock = new Mock<ErrorMonitor>(Mock.Of<ILogger<ErrorMonitor>>());
            _webDriverMock = new Mock<IWebDriver>();
        }

        [Fact]
        public async Task ExecuteAsync_SuccessfulLogin_ReturnsZero()
        {
            // Arrange
            var options = new CommandLineOptions
            {
                Url = "https://example.com",
                Username = "testuser",
                Password = "password",
                Domain = "domain"
            };

            var loginForm = new LoginFormElements
            {
                UsernameField = Mock.Of<IWebElement>(),
                PasswordField = Mock.Of<IWebElement>(),
                SubmitButton = Mock.Of<IWebElement>()
            };

            _browserManagerMock
                .Setup(x => x.LaunchBrowser(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(_webDriverMock.Object);

            // Set up error handler to execute the function directly
            _errorHandlerMock
                .Setup(x => x.ExecuteWithErrorHandlingAsync(It.IsAny<Func<Task<IWebDriver>>>()))
                .Returns<Func<Task<IWebDriver>>>(async func => await func());

            _errorHandlerMock
                .Setup(x => x.ExecuteWithRetryAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IWebDriver>(), It.IsAny<Func<Exception, bool>>()))
                .Returns<Func<Task<int>>, IWebDriver, Func<Exception, bool>>(async (func, driver, retryFunc) => await func());

            // Set up timeout manager to execute the function directly
            _timeoutManagerMock
                .Setup(x => x.ExecuteWithTimeoutAsync(It.IsAny<Func<Task<LoginFormElements>>>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns<Func<Task<LoginFormElements>>, string, int?, System.Threading.CancellationToken>(async (func, opName, timeout, token) => loginForm);

            _timeoutManagerMock
                .Setup(x => x.ExecuteWithTimeoutAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns<Func<Task<bool>>, string, int?, System.Threading.CancellationToken>(async (func, opName, timeout, token) => true);

            _loginDetectorMock
                .Setup(x => x.DetectLoginFormAsync(_webDriverMock.Object))
                .ReturnsAsync(loginForm);

            _credentialManagerMock
                .Setup(x => x.EnterCredentialsAsync(_webDriverMock.Object, loginForm, options.Username, options.Password, options.Domain))
                .ReturnsAsync(true);

            _loginVerifierMock
                .Setup(x => x.VerifyLoginSuccessAsync(_webDriverMock.Object))
                .ReturnsAsync(true);

            var service = new ChromeConnectService(
                _loggerMock.Object,
                _browserManagerMock.Object,
                _loginDetectorMock.Object,
                _credentialManagerMock.Object,
                _loginVerifierMock.Object,
                _screenshotCaptureMock.Object,
                _errorHandlerMock.Object,
                _timeoutManagerMock.Object,
                _errorMonitorMock.Object
            );

            // Act
            var result = await service.ExecuteAsync(options);

            // Assert
            Assert.Equal(0, result);
            _browserManagerMock.Verify(x => x.LaunchBrowser(options.Url, options.Incognito, options.Kiosk, options.IgnoreCertErrors), Times.Once);
            _loginDetectorMock.Verify(x => x.DetectLoginFormAsync(_webDriverMock.Object), Times.Once);
            _credentialManagerMock.Verify(x => x.EnterCredentialsAsync(_webDriverMock.Object, loginForm, options.Username, options.Password, options.Domain), Times.Once);
            _loginVerifierMock.Verify(x => x.VerifyLoginSuccessAsync(_webDriverMock.Object), Times.Once);
            _browserManagerMock.Verify(x => x.CloseBrowser(It.IsAny<IWebDriver>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_LoginFailed_ReturnsOne()
        {
            // Arrange
            var options = new CommandLineOptions
            {
                Url = "https://example.com",
                Username = "testuser",
                Password = "password",
                Domain = "domain"
            };

            var loginForm = new LoginFormElements
            {
                UsernameField = Mock.Of<IWebElement>(),
                PasswordField = Mock.Of<IWebElement>(),
                SubmitButton = Mock.Of<IWebElement>()
            };

            _browserManagerMock
                .Setup(x => x.LaunchBrowser(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(_webDriverMock.Object);

            _errorHandlerMock
                .Setup(x => x.ExecuteWithErrorHandlingAsync(It.IsAny<Func<Task<IWebDriver>>>()))
                .Returns<Func<Task<IWebDriver>>>(async func => await func());

            _errorHandlerMock
                .Setup(x => x.ExecuteWithRetryAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IWebDriver>(), It.IsAny<Func<Exception, bool>>()))
                .Callback<Func<Task<int>>, IWebDriver, Func<Exception, bool>>((func, driver, retryFunc) => 
                {
                    // Simulate the function throwing an exception
                    throw new InvalidCredentialsException("Login failed");
                })
                .ThrowsAsync(new InvalidCredentialsException("Login failed"));

            _timeoutManagerMock
                .Setup(x => x.ExecuteWithTimeoutAsync(It.IsAny<Func<Task<LoginFormElements>>>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns<Func<Task<LoginFormElements>>, string, int?, System.Threading.CancellationToken>(async (func, opName, timeout, token) => loginForm);

            var service = new ChromeConnectService(
                _loggerMock.Object,
                _browserManagerMock.Object,
                _loginDetectorMock.Object,
                _credentialManagerMock.Object,
                _loginVerifierMock.Object,
                _screenshotCaptureMock.Object,
                _errorHandlerMock.Object,
                _timeoutManagerMock.Object,
                _errorMonitorMock.Object
            );

            // Act
            var result = await service.ExecuteAsync(options);

            // Assert
            Assert.Equal(1, result);
            _browserManagerMock.Verify(x => x.LaunchBrowser(options.Url, options.Incognito, options.Kiosk, options.IgnoreCertErrors), Times.Once);
            _errorMonitorMock.Verify(x => x.RecordError(It.IsAny<InvalidCredentialsException>(), It.IsAny<string>()), Times.Once);
        }
    }
} 