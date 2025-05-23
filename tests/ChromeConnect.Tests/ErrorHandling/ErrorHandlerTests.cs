using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using OpenQA.Selenium;
using Xunit;
using ChromeConnect.Exceptions;
using ChromeConnect.Services;

namespace ChromeConnect.Tests.ErrorHandling
{
    public class ErrorHandlerTests
    {
        private readonly Mock<ILogger<ErrorHandler>> _mockLogger;
        private readonly Mock<IScreenshotCapture> _mockScreenshotCapture;
        private readonly Mock<IWebDriver> _mockDriver;
        private readonly ErrorHandler _errorHandler;

        public ErrorHandlerTests()
        {
            _mockLogger = new Mock<ILogger<ErrorHandler>>();
            _mockScreenshotCapture = new Mock<IScreenshotCapture>();
            _mockDriver = new Mock<IWebDriver>();
            
            _mockScreenshotCapture
                .Setup(x => x.CaptureScreenshot(It.IsAny<IWebDriver>(), It.IsAny<string>()))
                .Returns("test_screenshot.png");
                
            _errorHandler = new ErrorHandler(_mockLogger.Object, _mockScreenshotCapture.Object);
        }

        [Fact]
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
        
        [Fact]
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
        
        [Fact]
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
            Assert.True(actionExecuted);
        }
        
        [Fact]
        public async Task ExecuteWithErrorHandlingAsync_HandlesException_WhenExceptionThrown()
        {
            // Arrange
            var exception = new LoginException("Test login exception");
            
            // Act & Assert
            await Assert.ThrowsAsync<LoginException>(async () => {
                await _errorHandler.ExecuteWithErrorHandlingAsync(async () => {
                    throw exception;
                }, _mockDriver.Object);
            });
            
            // Verify exception was handled
            _mockScreenshotCapture.Verify(
                x => x.CaptureScreenshot(It.IsAny<IWebDriver>(), It.IsAny<string>()),
                Times.Once);
        }
        
        [Fact]
        public async Task ExecuteWithRetryAsync_RetriesCorrectNumberOfTimes_BeforeFailure()
        {
            // Arrange
            int attemptCount = 0;
            var settings = new ErrorHandlerSettings { DefaultRetryCount = 3 };
            var retryHandler = new ErrorHandler(_mockLogger.Object, _mockScreenshotCapture.Object, settings);
            
            // Act & Assert
            await Assert.ThrowsAsync<NetworkException>(async () => {
                await retryHandler.ExecuteWithRetryAsync(async () => {
                    attemptCount++;
                    throw new ConnectionFailedException("Test connection exception");
                });
            });
            
            // Verify retry count (initial + 3 retries)
            Assert.Equal(4, attemptCount);
        }
        
        [Fact]
        public async Task ExecuteWithRetryAsync_DoesNotRetry_ForNonTransientExceptions()
        {
            // Arrange
            int attemptCount = 0;
            var settings = new ErrorHandlerSettings { DefaultRetryCount = 3 };
            var retryHandler = new ErrorHandler(_mockLogger.Object, _mockScreenshotCapture.Object, settings);
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidCredentialsException>(async () => {
                await retryHandler.ExecuteWithRetryAsync(async () => {
                    attemptCount++;
                    throw new InvalidCredentialsException("Test invalid credentials");
                });
            });
            
            // Verify only attempted once (no retries for non-transient exceptions)
            Assert.Equal(1, attemptCount);
        }
        
        [Fact]
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
                    throw new ConnectionFailedException("Simulated transient error");
                }
                
                // Success on the third attempt
                await Task.CompletedTask;
            });
            
            // Assert
            Assert.Equal(SuccessfulAttempt, attemptCount);
        }
        
        [Fact]
        public async Task ExecuteWithRetryAsync_UsesExponentialBackoff_BetweenRetries()
        {
            // This test verifies that the delay between retries increases exponentially
            // Note: This is a more complex test that would require tracking of the actual delay times
            
            // Arrange
            var settings = new ErrorHandlerSettings { 
                DefaultRetryCount = 3,
                DefaultRetryDelayMs = 10, // Small value for testing
                BackoffMultiplier = 2.0,
                AddJitter = false // Disable jitter for predictable testing
            };
            
            var retryHandler = new ErrorHandler(_mockLogger.Object, _mockScreenshotCapture.Object, settings);
            
            int attemptCount = 0;
            DateTime lastAttemptTime = DateTime.UtcNow;
            TimeSpan[] delaysBetweenAttempts = new TimeSpan[3]; // For 3 retries
            
            // Act & Assert
            await Assert.ThrowsAsync<ConnectionFailedException>(async () => {
                await retryHandler.ExecuteWithRetryAsync(async () => {
                    if (attemptCount > 0)
                    {
                        // Record the time since last attempt (i.e., the delay)
                        TimeSpan delay = DateTime.UtcNow - lastAttemptTime;
                        delaysBetweenAttempts[attemptCount - 1] = delay;
                    }
                    
                    lastAttemptTime = DateTime.UtcNow;
                    attemptCount++;
                    
                    throw new ConnectionFailedException("Simulated transient error");
                });
            });
            
            // Verify exponential backoff pattern
            // Each delay should be approximately double the previous one
            // Allow for some variance due to execution time
            Assert.True(delaysBetweenAttempts[1].TotalMilliseconds > delaysBetweenAttempts[0].TotalMilliseconds * 1.5);
            Assert.True(delaysBetweenAttempts[2].TotalMilliseconds > delaysBetweenAttempts[1].TotalMilliseconds * 1.5);
        }
    }
} 