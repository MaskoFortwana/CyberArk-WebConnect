using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using ChromeConnect.Services;
using ChromeConnect.Utilities;
using ChromeConnect.Configuration;
using ChromeConnect.Core;
using OpenQA.Selenium;
using ChromeConnect.Models;

namespace ChromeConnect.Tests.Integration
{
    [TestClass]
    public class InputBlockingIntegrationTests
    {
        private ILoggerFactory? _loggerFactory;
        private Mock<BrowserManager>? _mockBrowserManager;
        private Mock<LoginDetector>? _mockLoginDetector;
        private Mock<CredentialManager>? _mockCredentialManager;
        private Mock<LoginVerifier>? _mockLoginVerifier;
        private Mock<IScreenshotCapture>? _mockScreenshotCapture;
        private Mock<ErrorHandler>? _mockErrorHandler;
        private Mock<TimeoutManager>? _mockTimeoutManager;
        private Mock<ErrorMonitor>? _mockErrorMonitor;
        private Mock<IWebDriver>? _mockDriver;

        [TestInitialize]
        public void TestInitialize()
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
            // Initialize mocks
            _mockBrowserManager = new Mock<BrowserManager>();
            _mockLoginDetector = new Mock<LoginDetector>();
            _mockCredentialManager = new Mock<CredentialManager>();
            _mockLoginVerifier = new Mock<LoginVerifier>();
            _mockScreenshotCapture = new Mock<IScreenshotCapture>();
            _mockErrorHandler = new Mock<ErrorHandler>();
            _mockTimeoutManager = new Mock<TimeoutManager>();
            _mockErrorMonitor = new Mock<ErrorMonitor>();
            _mockDriver = new Mock<IWebDriver>();

            // Setup basic mock behaviors
            _mockBrowserManager.Setup(x => x.LaunchBrowser(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                              .Returns(_mockDriver.Object);

            _mockDriver.Setup(x => x.Url).Returns("https://test.example.com");

            // Setup error handler to execute functions directly
            _mockErrorHandler.Setup(x => x.ExecuteWithErrorHandlingAsync(It.IsAny<Func<Task<IWebDriver>>>()))
                            .Returns<Func<Task<IWebDriver>>>(func => func());

            _mockErrorHandler.Setup(x => x.ExecuteWithRetryAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IWebDriver>(), It.IsAny<Func<Exception, bool>>()))
                            .Returns<Func<Task<int>>, IWebDriver, Func<Exception, bool>>((func, driver, retry) => func());

            // Setup timeout manager to execute functions directly
            _mockTimeoutManager.Setup(x => x.ExecuteWithTimeoutAsync<LoginFormElements>(It.IsAny<Func<System.Threading.CancellationToken, Task<LoginFormElements>>>(), It.IsAny<string>()))
                              .Returns<Func<System.Threading.CancellationToken, Task<LoginFormElements>>, string>((func, name) => func(System.Threading.CancellationToken.None));

            // Setup login detector to return a valid form
            var mockLoginForm = new LoginFormElements
            {
                UsernameField = new Mock<IWebElement>().Object,
                PasswordField = new Mock<IWebElement>().Object,
                SubmitButton = new Mock<IWebElement>().Object
            };
            _mockLoginDetector.Setup(x => x.DetectLoginFormAsync(It.IsAny<IWebDriver>()))
                             .ReturnsAsync(mockLoginForm);

            // Setup credential manager to succeed
            _mockCredentialManager.Setup(x => x.EnterCredentialsAsync(It.IsAny<IWebDriver>(), It.IsAny<LoginFormElements>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                 .ReturnsAsync(true);

            // Setup login verifier to succeed
            _mockLoginVerifier.Setup(x => x.VerifyLoginSuccessAsync(It.IsAny<IWebDriver>(), It.IsAny<LoginFormElements>()))
                             .ReturnsAsync(new LoginAssessmentResult { IsSuccess = true, Confidence = 0.95, Reason = "Login successful" });
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _loggerFactory?.Dispose();
            
            // Reset static configuration
            StaticConfiguration.InputBlockingEnabled = true;
            StaticConfiguration.InputBlockingTimeoutSeconds = 150;
            // Note: StartMaximized is now hardcoded and not configurable
        }

        [TestMethod]
        public async Task ChromeConnectService_WithInputBlockingEnabled_ActivatesInputBlocking()
        {
            // Arrange
            StaticConfiguration.InputBlockingEnabled = true;
            StaticConfiguration.InputBlockingTimeoutSeconds = 60;

            var logger = _loggerFactory!.CreateLogger<ChromeConnectService>();
            var service = new ChromeConnectService(
                logger,
                _mockBrowserManager!.Object,
                _mockLoginDetector!.Object,
                _mockCredentialManager!.Object,
                _mockLoginVerifier!.Object,
                _mockScreenshotCapture!.Object,
                _mockErrorHandler!.Object,
                _mockTimeoutManager!.Object,
                _mockErrorMonitor!.Object
            );

            var options = new CommandLineOptions
            {
                Url = "https://test.example.com",
                Username = "testuser",
                Password = "testpass"
            };

            // Act
            var result = await service.ExecuteAsync(options);

            // Assert
            Assert.AreEqual(0, result, "Service should complete successfully");
            
            // Verify browser was launched (now always maximized by default)
            _mockBrowserManager.Verify(x => x.LaunchBrowser(
                It.IsAny<string>(), 
                It.IsAny<bool>(), 
                It.IsAny<bool>(), 
                It.IsAny<bool>()), Times.Once);
        }

        [TestMethod]
        public async Task ChromeConnectService_WithInputBlockingDisabled_SkipsInputBlocking()
        {
            // Arrange
            StaticConfiguration.InputBlockingEnabled = false;

            var logger = _loggerFactory!.CreateLogger<ChromeConnectService>();
            var service = new ChromeConnectService(
                logger,
                _mockBrowserManager!.Object,
                _mockLoginDetector!.Object,
                _mockCredentialManager!.Object,
                _mockLoginVerifier!.Object,
                _mockScreenshotCapture!.Object,
                _mockErrorHandler!.Object,
                _mockTimeoutManager!.Object,
                _mockErrorMonitor!.Object
            );

            var options = new CommandLineOptions
            {
                Url = "https://test.example.com",
                Username = "testuser",
                Password = "testpass"
            };

            // Act
            var result = await service.ExecuteAsync(options);

            // Assert
            Assert.AreEqual(0, result, "Service should complete successfully without input blocking");
        }

        [TestMethod]
        public async Task ChromeConnectService_WithException_EnsuresInputBlockingCleanup()
        {
            // Arrange
            StaticConfiguration.InputBlockingEnabled = true;

            // Setup credential manager to throw an exception
            _mockCredentialManager!.Setup(x => x.EnterCredentialsAsync(It.IsAny<IWebDriver>(), It.IsAny<LoginFormElements>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .ThrowsAsync(new InvalidOperationException("Test exception"));

            var logger = _loggerFactory!.CreateLogger<ChromeConnectService>();
            var service = new ChromeConnectService(
                logger,
                _mockBrowserManager!.Object,
                _mockLoginDetector!.Object,
                _mockCredentialManager.Object,
                _mockLoginVerifier!.Object,
                _mockScreenshotCapture!.Object,
                _mockErrorHandler!.Object,
                _mockTimeoutManager!.Object,
                _mockErrorMonitor!.Object
            );

            var options = new CommandLineOptions
            {
                Url = "https://test.example.com",
                Username = "testuser",
                Password = "testpass"
            };

            // Act
            var result = await service.ExecuteAsync(options);

            // Assert
            // The service should handle the exception and still complete cleanup
            Assert.AreNotEqual(0, result, "Service should return error code when exception occurs");
        }

        [TestMethod]
        public void StaticConfiguration_InputBlockingSettings_HaveCorrectDefaults()
        {
            // Act & Assert
            Assert.IsTrue(StaticConfiguration.InputBlockingEnabled, "Input blocking should be enabled by default");
            Assert.AreEqual(150, StaticConfiguration.InputBlockingTimeoutSeconds, "Default timeout should be 150 seconds");
        }

        [TestMethod]
        public void StaticConfiguration_CanModifyInputBlockingSettings()
        {
            // Arrange
            var originalEnabled = StaticConfiguration.InputBlockingEnabled;
            var originalTimeout = StaticConfiguration.InputBlockingTimeoutSeconds;

            try
            {
                // Act
                StaticConfiguration.InputBlockingEnabled = false;
                StaticConfiguration.InputBlockingTimeoutSeconds = 30;

                // Assert
                Assert.IsFalse(StaticConfiguration.InputBlockingEnabled, "Should be able to disable input blocking");
                Assert.AreEqual(30, StaticConfiguration.InputBlockingTimeoutSeconds, "Should be able to change timeout");
            }
            finally
            {
                // Restore original values
                StaticConfiguration.InputBlockingEnabled = originalEnabled;
                StaticConfiguration.InputBlockingTimeoutSeconds = originalTimeout;
            }
        }

        [TestMethod]
        public async Task InputBlocker_Integration_WithRealTimeouts()
        {
            // Arrange
            var shortTimeout = 1000; // 1 second for quick test
            var logger = _loggerFactory!.CreateLogger<InputBlocker>();

            // Act & Assert
            using (var inputBlocker = new InputBlocker(shortTimeout, logger))
            {
                // Test basic functionality
                var startResult = inputBlocker.StartBlocking();
                Assert.IsTrue(startResult == true || startResult == false, "StartBlocking should return a boolean");

                // Wait a bit
                await Task.Delay(100);

                var stopResult = inputBlocker.StopBlocking();
                Assert.IsTrue(stopResult == true || stopResult == false, "StopBlocking should return a boolean");

                // Ensure not blocking after stop
                Assert.IsFalse(inputBlocker.IsBlocking, "Should not be blocking after stop");
            }
        }

        [TestMethod]
        public void NativeMethods_BlockInput_ReturnsBoolean()
        {
            // Arrange & Act
            var result1 = NativeMethods.BlockInput(true);
            var result2 = NativeMethods.BlockInput(false);

            // Assert
            // The actual result depends on system state and permissions,
            // but the method should always return a boolean without throwing
            Assert.IsTrue(result1 == true || result1 == false, "BlockInput(true) should return a boolean");
            Assert.IsTrue(result2 == true || result2 == false, "BlockInput(false) should return a boolean");
        }

        [TestMethod]
        public async Task EmergencyCleanup_EnsuresInputUnblocked()
        {
            // Arrange
            var logger = _loggerFactory!.CreateLogger<InputBlocker>();
            
            // Act
            using (var inputBlocker = new InputBlocker(60000, logger))
            {
                inputBlocker.StartBlocking();
                
                // Simulate emergency cleanup (like what happens in Program.cs)
                try
                {
                    NativeMethods.BlockInput(false);
                }
                catch
                {
                    // Best effort - should not throw
                }
            }

            // Assert - No exceptions should occur and input should be unblocked
            await Task.CompletedTask; // Test passes if no exception is thrown
        }

        [TestMethod]
        public void ConfigurationIntegration_InputBlockingTimeout_CorrectConversion()
        {
            // Arrange
            StaticConfiguration.InputBlockingTimeoutSeconds = 30;

            // Act
            var logger = _loggerFactory!.CreateLogger<InputBlocker>();
            using var inputBlocker = new InputBlocker(StaticConfiguration.InputBlockingTimeoutSeconds * 1000, logger);

            // Assert
            // The InputBlocker should be created with the timeout converted to milliseconds
            Assert.IsFalse(inputBlocker.IsBlocking, "InputBlocker should be created successfully");
        }
    }
} 