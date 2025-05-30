using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;
using OpenQA.Selenium;
using System;
using System.Threading.Tasks;
using WebConnect.Exceptions;
using WebConnect.Services;

namespace WebConnect.Tests.ErrorHandling
{
    [TestClass]
    public class ErrorHandlerTests
    {
        private Mock<ILogger<ErrorHandler>> _mockLogger;
        private Mock<IScreenshotCapture> _mockScreenshotCapture;
        private Mock<IWebDriver> _mockDriver;
        private ErrorHandlerSettings _settings;
        private ErrorHandler _errorHandler;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockLogger = new Mock<ILogger<ErrorHandler>>();
            _mockScreenshotCapture = new Mock<IScreenshotCapture>();
            _mockDriver = new Mock<IWebDriver>();
            _settings = new ErrorHandlerSettings
            { 
                CaptureScreenshots = true,
                CloseDriverOnError = true,
                DefaultRetryCount = 3,
                DefaultRetryDelayMs = 10,
                BackoffMultiplier = 2.0,
                AddJitter = false 
            };
            _errorHandler = new ErrorHandler(_mockLogger.Object, _mockScreenshotCapture.Object, _settings);
            
            _mockScreenshotCapture
                .Setup(x => x.CaptureScreenshot(It.IsAny<IWebDriver>(), It.IsAny<string>()))
                .Returns("test_screenshot.png");
        }

        [TestMethod]
        public async Task HandleExceptionAsync_CapturesScreenshot_WhenDriverProvided()
        {
            // Arrange
            var exception = new LoginException("Test login exception");
            
            // Act
            await _errorHandler.HandleExceptionAsync(exception, _mockDriver.Object);
            
            // Assert
            _mockScreenshotCapture.Verify(
                x => x.CaptureScreenshot(
                    _mockDriver.Object, 
                    It.Is<string>(s => s.Contains("Error_Login"))),
                Times.Once);
        }
        
        [TestMethod]
        public async Task HandleExceptionAsync_DoesNotCaptureScreenshot_WhenDriverIsNull()
        {
            // Arrange
            var exception = new LoginException("Test login exception");
            
            // Act
            await _errorHandler.HandleExceptionAsync(exception, null);
            
            // Assert
            _mockScreenshotCapture.Verify(
                x => x.CaptureScreenshot(It.IsAny<IWebDriver>(), It.IsAny<string>()),
                Times.Never);
        }
        
        [TestMethod]
        public async Task ExecuteWithErrorHandlingAsync_ExecutesAction_WhenNoExceptions()
        {
            // Arrange
            bool actionExecuted = false;
            
            // Act
            await _errorHandler.ExecuteWithErrorHandlingAsync(async () => {
                actionExecuted = true;
                await Task.CompletedTask;
            });
            
            // Assert
            Assert.IsTrue(actionExecuted);
        }
        
        [TestMethod]
        public async Task ExecuteWithErrorHandlingAsync_HandlesException_WhenExceptionThrown()
        {
            // Arrange
            var exception = new LoginException("Test login exception");
            
            // Act & Assert
            await Assert.ThrowsExceptionAsync<LoginException>(async () => {
                await _errorHandler.ExecuteWithErrorHandlingAsync(async () => {
                    throw exception;
                }, _mockDriver.Object);
            });
            
            // Verify exception was handled
            _mockScreenshotCapture.Verify(
                x => x.CaptureScreenshot(It.IsAny<IWebDriver>(), It.IsAny<string>()),
                Times.Once);
        }
        
        [TestMethod]
        public async Task ExecuteWithRetryAsync_RetriesCorrectNumberOfTimes_BeforeFailure()
        {
            // Arrange
            int attemptCount = 0;
            
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ConnectionFailedException>(async () => {
                await _errorHandler.ExecuteWithRetryAsync(async () => {
                    attemptCount++;
                    throw new ConnectionFailedException();
                });
                await Task.CompletedTask;
            });
            
            Assert.AreEqual(_settings.DefaultRetryCount + 1, attemptCount);
        }
        
        [TestMethod]
        public async Task ExecuteWithRetryAsync_DoesNotRetry_ForNonTransientExceptions()
        {
            // Arrange
            int attemptCount = 0;
            
            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidCredentialsException>(async () => {
                await _errorHandler.ExecuteWithRetryAsync(async () => {
                    attemptCount++;
                    throw new InvalidCredentialsException("Test invalid credentials");
                });
                await Task.CompletedTask;
            });
            
            Assert.AreEqual(1, attemptCount);
        }
        
        [TestMethod]
        public async Task ExecuteWithRetryAsync_SucceedsEventually_WhenErrorsAreTransient()
        {
            // Arrange
            int attemptCount = 0;
            const int SuccessfulAttempt = 3;
            
            // Act
            await _errorHandler.ExecuteWithRetryAsync(async () => {
                attemptCount++;
                
                if (attemptCount < SuccessfulAttempt)
                {
                    throw new ConnectionFailedException();
                }
                
                await Task.CompletedTask;
            });
            
            Assert.AreEqual(SuccessfulAttempt, attemptCount);
        }
        
        [TestMethod]
        public async Task ExecuteWithRetryAsync_UsesExponentialBackoff_BetweenRetries()
        {
            // Arrange
            int attemptCount = 0;
            DateTime lastAttemptTime = DateTime.UtcNow;
            TimeSpan[] delaysBetweenAttempts = new TimeSpan[_settings.DefaultRetryCount]; 
            
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ConnectionFailedException>(async () => {
                await _errorHandler.ExecuteWithRetryAsync(async () => {
                    if (attemptCount > 0 && attemptCount <= _settings.DefaultRetryCount)
                    {
                        TimeSpan delay = DateTime.UtcNow - lastAttemptTime;
                        delaysBetweenAttempts[attemptCount - 1] = delay;
                    }
                    
                    lastAttemptTime = DateTime.UtcNow;
                    attemptCount++;
                    
                    throw new ConnectionFailedException();
                });
                await Task.CompletedTask;
            });
            
            if (_settings.DefaultRetryCount >= 1) Assert.IsTrue(delaysBetweenAttempts[0].TotalMilliseconds >= _settings.DefaultRetryDelayMs * 0.8);
            if (_settings.DefaultRetryCount >= 2) Assert.IsTrue(delaysBetweenAttempts[1].TotalMilliseconds > delaysBetweenAttempts[0].TotalMilliseconds * (_settings.BackoffMultiplier - 0.5));
            if (_settings.DefaultRetryCount >= 3) Assert.IsTrue(delaysBetweenAttempts[2].TotalMilliseconds > delaysBetweenAttempts[1].TotalMilliseconds * (_settings.BackoffMultiplier - 0.5));
        }

        [TestMethod]
        public async Task HandleExceptionAsync_GeneralException_LogsErrorAndCapturesScreenshotAndClosesBrowser()
        {
            // Arrange
            var exception = new Exception("Test Exception");
            var mockWebDriver = new Mock<IWebDriver>();
            _settings.CaptureScreenshots = true;
            _settings.CloseDriverOnError = true;

            // Act
            await _errorHandler.HandleExceptionAsync(exception, mockWebDriver.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<object>(v => v != null && v.ToString().Contains("Test Exception")),
                    exception,
                    It.Is<Func<object, Exception?, string>>((v, t) => true)),
                Times.Once
            );
            _mockScreenshotCapture.Verify(s => s.CaptureScreenshot(mockWebDriver.Object, "Error_"), Times.Once);
            mockWebDriver.Verify(d => d.Quit(), Times.Once);
        }

        [TestMethod]
        public async Task ExecuteWithErrorHandlingAsync_ActionSucceeds_ReturnsResultAndNoLogging()
        {
            // Arrange
            var expectedResult = "Success";
            Func<Task<string>> action = () => Task.FromResult(expectedResult);

            // Act
            var result = await _errorHandler.ExecuteWithErrorHandlingAsync(action);

            // Assert
            Assert.AreEqual(expectedResult, result);
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    It.IsAny<Exception?>(),
                    It.Is<Func<object, Exception?, string>>((v,t) => true)),
                Times.Never
            );
        }

        [TestMethod]
        public async Task ExecuteWithErrorHandlingAsync_ActionThrowsKnownException_LogsErrorAndReturnsDefault()
        {
            // Arrange
            var exception = new BrowserInitializationException();
            Func<Task<string>> action = () => throw exception;
            _settings.CaptureScreenshots = false;
            _settings.CloseDriverOnError = false;

            // Act & Assert
            await Assert.ThrowsExceptionAsync<BrowserInitializationException>(async () => {
                await _errorHandler.ExecuteWithErrorHandlingAsync(action);
            });

            // Assert exception was logged
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    exception,
                    It.Is<Func<object, Exception?, string>>((v, t) => true)),
                Times.Once
            );
        }
        
        [TestMethod]
        public async Task ExecuteWithRetryAsync_ActionSucceedsOnFirstTry_ReturnsResult()
        {
            // Arrange
            int attempt = 0;
            Func<Task<string>> action = () => 
            {
                attempt++;
                return Task.FromResult("Success");
            };
            var mockWebDriver = new Mock<IWebDriver>();

            // Act
            var result = await _errorHandler.ExecuteWithRetryAsync(action, mockWebDriver.Object);

            // Assert
            Assert.AreEqual("Success", result);
            Assert.AreEqual(1, attempt);
        }

        [TestMethod]
        public async Task ExecuteWithRetryAsync_ActionFailsInitiallyThenSucceeds_ReturnsResultAndLogsWarning()
        {
            // Arrange
            int attempt = 0;
            Func<Task<string>> action = () => 
            {
                attempt++;
                if (attempt < 2) throw new ConnectionFailedException();
                return Task.FromResult("Success");
            };
            var mockWebDriver = new Mock<IWebDriver>();

            // Act
            var result = await _errorHandler.ExecuteWithRetryAsync(action, mockWebDriver.Object);

            // Assert
            Assert.AreEqual("Success", result);
            Assert.AreEqual(2, attempt);
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    It.IsAny<ConnectionFailedException>(),
                    It.Is<Func<object, Exception?, string>>((v, t) => true)),
                Times.Once
            );
        }

        [TestMethod]
        public async Task ExecuteWithRetryAsync_ActionFailsConsistently_ThrowsAndLogsError()
        {
            // Arrange
            int attempt = 0;
            var exception = new ConnectionFailedException();
            Func<Task<string>> action = () => 
            {
                attempt++;
                throw exception;
            };
            var mockWebDriver = new Mock<IWebDriver>();
            // Use local settings for this specific test scenario to control retries
            var localSettings = new ErrorHandlerSettings {
                DefaultRetryCount = 2,
                DefaultRetryDelayMs = 10,
                CaptureScreenshots = true,
                CloseDriverOnError = true
            };
            // Important: Create a new ErrorHandler instance with these localSettings for this test
            var localErrorHandler = new ErrorHandler(_mockLogger.Object, _mockScreenshotCapture.Object, localSettings);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ConnectionFailedException>(() => localErrorHandler.ExecuteWithRetryAsync(action, mockWebDriver.Object));
            Assert.AreEqual(localSettings.DefaultRetryCount + 1, attempt);
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    exception,
                    It.Is<Func<object, Exception?, string>>((v, t) => true)),
                Times.AtLeastOnce
            );
            _mockScreenshotCapture.Verify(s => s.CaptureScreenshot(mockWebDriver.Object, It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleExceptionAsync_ActionThrowsKnownException_LogsErrorAndClosesBrowser_WhenConfigured()
        {
            // Arrange
            var exception = new BrowserInitializationException();
            var mockWebDriver = new Mock<IWebDriver>();
            var localSettings = new ErrorHandlerSettings
            {
                CaptureScreenshots = false,
                CloseDriverOnError = true,
                DefaultRetryCount = 0
            };
            var localErrorHandler = new ErrorHandler(_mockLogger.Object, _mockScreenshotCapture.Object, localSettings);

            // Act
            await localErrorHandler.HandleExceptionAsync(exception, mockWebDriver.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    exception,
                    It.Is<Func<object, Exception?, string>>((v, t) => true)),
                Times.Once
            );
            mockWebDriver.Verify(d => d.Quit(), Times.Once);
            _mockScreenshotCapture.Verify(s => s.CaptureScreenshot(It.IsAny<IWebDriver>(), It.IsAny<string>()), Times.Never);
        }
    }
} 
