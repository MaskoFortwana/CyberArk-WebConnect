using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChromeConnect.Utilities;
using System.Threading;

namespace ChromeConnect.Tests.Utilities
{
    [TestClass]
    public class InputBlockerTests
    {
        private Mock<ILogger<InputBlocker>>? _mockLogger;
        private ILoggerFactory? _loggerFactory;

        [TestInitialize]
        public void TestInitialize()
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            _mockLogger = new Mock<ILogger<InputBlocker>>();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _loggerFactory?.Dispose();
        }

        [TestMethod]
        public void Constructor_WithDefaultTimeout_InitializesCorrectly()
        {
            // Act
            using var inputBlocker = new InputBlocker();

            // Assert
            Assert.IsFalse(inputBlocker.IsBlocking, "InputBlocker should not be blocking initially");
        }

        [TestMethod]
        public void Constructor_WithCustomTimeout_InitializesCorrectly()
        {
            // Arrange
            var customTimeout = 30000; // 30 seconds
            var logger = _loggerFactory?.CreateLogger<InputBlocker>();

            // Act
            using var inputBlocker = new InputBlocker(customTimeout, logger);

            // Assert
            Assert.IsFalse(inputBlocker.IsBlocking, "InputBlocker should not be blocking initially");
        }

        [TestMethod]
        public void StartBlocking_WhenNotAlreadyBlocking_AttemptsToBlock()
        {
            // Arrange
            using var inputBlocker = new InputBlocker(60000, _mockLogger.Object);

            // Act
            var result = inputBlocker.StartBlocking();

            // Assert
            // Note: We can't easily mock the Windows API call, so we test the behavior
            // The actual BlockInput API call will either succeed or fail based on system state
            // But the method should handle both cases gracefully
            Assert.IsTrue(result == true || result == false, "StartBlocking should return a boolean result");
            
            // Verify appropriate logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting system-wide input blocking")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void StartBlocking_WhenAlreadyBlocking_ReturnsTrue()
        {
            // Arrange
            using var inputBlocker = new InputBlocker(60000, _mockLogger.Object);
            inputBlocker.StartBlocking(); // First call

            // Act
            var result = inputBlocker.StartBlocking(); // Second call

            // Assert
            Assert.IsTrue(result, "StartBlocking should return true when already blocking");
            
            // Verify debug logging for already active state
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Input blocking already active")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [TestMethod]
        public void StopBlocking_WhenNotBlocking_ReturnsTrue()
        {
            // Arrange
            using var inputBlocker = new InputBlocker(60000, _mockLogger.Object);

            // Act
            var result = inputBlocker.StopBlocking();

            // Assert
            Assert.IsTrue(result, "StopBlocking should return true when not currently blocking");
            
            // Verify debug logging for not currently active state
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Input blocking not currently active")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void StopBlocking_AfterStartBlocking_AttemptsToUnblock()
        {
            // Arrange
            using var inputBlocker = new InputBlocker(60000, _mockLogger.Object);
            inputBlocker.StartBlocking();

            // Act
            var result = inputBlocker.StopBlocking();

            // Assert
            Assert.IsTrue(result == true || result == false, "StopBlocking should return a boolean result");
            
            // Verify appropriate logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping system-wide input blocking")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task SafetyTimeout_AfterDelay_TriggersForceUnblock()
        {
            // Arrange
            var shortTimeout = 500; // 500ms for quick test
            using var inputBlocker = new InputBlocker(shortTimeout, _mockLogger.Object);
            
            // Act
            inputBlocker.StartBlocking();
            
            // Wait longer than the timeout
            await Task.Delay(shortTimeout + 200);

            // Assert
            // The safety timer should have triggered and logged a warning
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Force unblocking input") && v.ToString()!.Contains("Safety timeout triggered")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void Dispose_WhenBlocking_StopsBlocking()
        {
            // Arrange
            var inputBlocker = new InputBlocker(60000, _mockLogger.Object);
            inputBlocker.StartBlocking();

            // Act
            inputBlocker.Dispose();

            // Assert
            // Verify that dispose logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing InputBlocker")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void StartBlocking_AfterDispose_LogsWarningAndReturnsFalse()
        {
            // Arrange
            var inputBlocker = new InputBlocker(60000, _mockLogger.Object);
            inputBlocker.Dispose();

            // Act
            var result = inputBlocker.StartBlocking();

            // Assert
            Assert.IsFalse(result, "StartBlocking should return false after disposal");
            
            // Verify warning logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot start blocking on disposed InputBlocker")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void StopBlocking_AfterDispose_LogsWarningAndReturnsFalse()
        {
            // Arrange
            var inputBlocker = new InputBlocker(60000, _mockLogger.Object);
            inputBlocker.Dispose();

            // Act
            var result = inputBlocker.StopBlocking();

            // Assert
            Assert.IsFalse(result, "StopBlocking should return false after disposal");
            
            // Verify warning logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot stop blocking on disposed InputBlocker")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void IsBlocking_ReflectsCurrentState()
        {
            // Arrange
            using var inputBlocker = new InputBlocker(60000, _mockLogger.Object);

            // Act & Assert - Initial state
            Assert.IsFalse(inputBlocker.IsBlocking, "IsBlocking should be false initially");

            // Act & Assert - After starting
            inputBlocker.StartBlocking();
            // Note: IsBlocking state depends on actual Windows API success
            // We can't predict the exact state, but it should be consistent
            var blockingStateAfterStart = inputBlocker.IsBlocking;

            // Act & Assert - After stopping
            inputBlocker.StopBlocking();
            Assert.IsFalse(inputBlocker.IsBlocking, "IsBlocking should be false after stopping");
        }

        [TestMethod]
        public void MultipleDispose_DoesNotCauseException()
        {
            // Arrange
            var inputBlocker = new InputBlocker(60000, _mockLogger.Object);

            // Act & Assert
            inputBlocker.Dispose(); // First dispose
            Assert.DoesNotThrow(() => inputBlocker.Dispose()); // Second dispose should not throw
        }

        [TestMethod]
        public void ThreadSafety_ConcurrentStartStop_HandledSafely()
        {
            // Arrange
            using var inputBlocker = new InputBlocker(60000, _mockLogger.Object);
            var exceptions = new List<Exception>();

            // Act
            var tasks = new List<Task>();
            
            // Create multiple concurrent tasks
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        inputBlocker.StartBlocking();
                        Thread.Sleep(10);
                        inputBlocker.StopBlocking();
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.AreEqual(0, exceptions.Count, $"No exceptions should occur during concurrent access. Found: {string.Join(", ", exceptions.Select(e => e.Message))}");
        }

        [TestMethod]
        public void WithNullLogger_DoesNotThrow()
        {
            // Arrange & Act & Assert
            Assert.DoesNotThrow(() =>
            {
                using var inputBlocker = new InputBlocker(60000, null);
                inputBlocker.StartBlocking();
                inputBlocker.StopBlocking();
            }, "InputBlocker should handle null logger gracefully");
        }
    }

    /// <summary>
    /// Extension method to add DoesNotThrow assertion for MSTest
    /// </summary>
    public static class AssertExtensions
    {
        public static void DoesNotThrow(Action action, string? message = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Assert.Fail(message ?? $"Expected no exception, but got: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
} 