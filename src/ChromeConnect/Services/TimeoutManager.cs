using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Exceptions;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Manages timeouts for various operations in the application.
    /// </summary>
    public class TimeoutManager
    {
        private readonly ILogger<TimeoutManager> _logger;
        private readonly TimeoutSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutManager"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="settings">Optional timeout settings.</param>
        public TimeoutManager(
            ILogger<TimeoutManager> logger,
            TimeoutSettings? settings = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? new TimeoutSettings();
        }

        /// <summary>
        /// Executes an action with a timeout.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="timeoutMs">The timeout in milliseconds. If null, the default timeout is used.</param>
        /// <param name="operationName">The name of the operation for logging and error reporting.</param>
        /// <param name="cancellationToken">An optional token to cancel the operation.</param>
        /// <exception cref="BrowserTimeoutException">Thrown when the operation times out.</exception>
        public async Task ExecuteWithTimeoutAsync(
            Func<Task> action,
            int? timeoutMs = null,
            string operationName = "Operation",
            CancellationToken cancellationToken = default)
        {
            int timeout = timeoutMs ?? _settings.DefaultTimeoutMs;
            
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token, cancellationToken);
                
            try
            {
                _logger.LogDebug("Starting {Operation} with timeout of {Timeout}ms", operationName, timeout);
                await action.Invoke();
                _logger.LogDebug("{Operation} completed successfully", operationName);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                var timeoutException = new BrowserTimeoutException(
                    $"The operation '{operationName}' timed out after {timeout}ms", 
                    operationName, 
                    timeout);
                    
                _logger.LogWarning(timeoutException, "Operation timeout");
                throw timeoutException;
            }
            catch (TaskCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                var timeoutException = new BrowserTimeoutException(
                    $"The operation '{operationName}' timed out after {timeout}ms", 
                    operationName, 
                    timeout);
                    
                _logger.LogWarning(timeoutException, "Operation timeout");
                throw timeoutException;
            }
        }

        /// <summary>
        /// Executes a function with a timeout and returns its result.
        /// </summary>
        /// <typeparam name="T">The type of result returned by the function.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <param name="timeoutMs">The timeout in milliseconds. If null, the default timeout is used.</param>
        /// <param name="operationName">The name of the operation for logging and error reporting.</param>
        /// <param name="cancellationToken">An optional token to cancel the operation.</param>
        /// <returns>The result of the function.</returns>
        /// <exception cref="BrowserTimeoutException">Thrown when the operation times out.</exception>
        public async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<Task<T>> func,
            int? timeoutMs = null,
            string operationName = "Operation",
            CancellationToken cancellationToken = default)
        {
            int timeout = timeoutMs ?? _settings.DefaultTimeoutMs;
            
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token, cancellationToken);
                
            try
            {
                _logger.LogDebug("Starting {Operation} with timeout of {Timeout}ms", operationName, timeout);
                T result = await func.Invoke();
                _logger.LogDebug("{Operation} completed successfully", operationName);
                return result;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                var timeoutException = new BrowserTimeoutException(
                    $"The operation '{operationName}' timed out after {timeout}ms", 
                    operationName, 
                    timeout);
                    
                _logger.LogWarning(timeoutException, "Operation timeout");
                throw timeoutException;
            }
            catch (TaskCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                var timeoutException = new BrowserTimeoutException(
                    $"The operation '{operationName}' timed out after {timeout}ms", 
                    operationName, 
                    timeout);
                    
                _logger.LogWarning(timeoutException, "Operation timeout");
                throw timeoutException;
            }
        }

        /// <summary>
        /// Waits for an element to be visible with a timeout.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="by">The locator to find the element.</param>
        /// <param name="timeoutMs">The timeout in milliseconds. If null, the default element timeout is used.</param>
        /// <returns>The found web element.</returns>
        /// <exception cref="BrowserTimeoutException">Thrown when the element is not found within the timeout.</exception>
        public IWebElement WaitForElement(
            IWebDriver driver,
            By by,
            int? timeoutMs = null)
        {
            int timeout = timeoutMs ?? _settings.ElementTimeoutMs;
            string operationName = $"WaitForElement({by})";
            
            try
            {
                _logger.LogDebug("Waiting for element {Locator} with timeout of {Timeout}ms", by, timeout);
                
                var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(timeout));
                wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
                
                IWebElement element = wait.Until(d => {
                    var elem = d.FindElement(by);
                    return elem.Displayed ? elem : null;
                });
                
                _logger.LogDebug("Element {Locator} found", by);
                return element;
            }
            catch (WebDriverTimeoutException ex)
            {
                var timeoutException = new BrowserTimeoutException(
                    $"Timed out after {timeout}ms waiting for element {by} to be visible", 
                    operationName, 
                    timeout,
                    ex);
                    
                _logger.LogWarning(timeoutException, "Element timeout");
                throw timeoutException;
            }
        }

        /// <summary>
        /// Waits for a condition to be true with a timeout.
        /// </summary>
        /// <typeparam name="T">The type of result expected from the condition.</typeparam>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeoutMs">The timeout in milliseconds. If null, the default condition timeout is used.</param>
        /// <param name="operationName">The name of the operation for logging and error reporting.</param>
        /// <returns>The result of the condition.</returns>
        /// <exception cref="BrowserTimeoutException">Thrown when the condition is not met within the timeout.</exception>
        public T WaitForCondition<T>(
            IWebDriver driver,
            Func<IWebDriver, T> condition,
            int? timeoutMs = null,
            string operationName = "WaitForCondition")
        {
            int timeout = timeoutMs ?? _settings.ConditionTimeoutMs;
            
            try
            {
                _logger.LogDebug("Waiting for condition {Operation} with timeout of {Timeout}ms", operationName, timeout);
                
                var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(timeout));
                T result = wait.Until(condition);
                
                _logger.LogDebug("Condition {Operation} met", operationName);
                return result;
            }
            catch (WebDriverTimeoutException ex)
            {
                var timeoutException = new BrowserTimeoutException(
                    $"Timed out after {timeout}ms waiting for condition {operationName}", 
                    operationName, 
                    timeout,
                    ex);
                    
                _logger.LogWarning(timeoutException, "Condition timeout");
                throw timeoutException;
            }
        }

        /// <summary>
        /// Waits for a URL change with a timeout.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="origUrl">The original URL to compare against.</param>
        /// <param name="timeoutMs">The timeout in milliseconds. If null, the default URL change timeout is used.</param>
        /// <returns>True if the URL changed, false otherwise.</returns>
        public bool WaitForUrlChange(
            IWebDriver driver,
            string origUrl,
            int? timeoutMs = null)
        {
            int timeout = timeoutMs ?? _settings.UrlChangeTimeoutMs;
            string operationName = "WaitForUrlChange";
            
            try
            {
                _logger.LogDebug("Waiting for URL change from {OrigUrl} with timeout of {Timeout}ms", origUrl, timeout);
                
                var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(timeout));
                bool changed = wait.Until(d => d.Url != origUrl);
                
                _logger.LogDebug("URL changed to {NewUrl}", driver.Url);
                return true;
            }
            catch (WebDriverTimeoutException)
            {
                _logger.LogDebug("URL did not change from {OrigUrl} within {Timeout}ms", origUrl, timeout);
                return false;
            }
        }
    }

    /// <summary>
    /// Settings for the TimeoutManager class.
    /// </summary>
    public class TimeoutSettings
    {
        /// <summary>
        /// Gets or sets the default timeout for operations in milliseconds.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the timeout for element operations in milliseconds.
        /// </summary>
        public int ElementTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the timeout for waiting on conditions in milliseconds.
        /// </summary>
        public int ConditionTimeoutMs { get; set; } = 15000;

        /// <summary>
        /// Gets or sets the timeout for URL change operations in milliseconds.
        /// </summary>
        public int UrlChangeTimeoutMs { get; set; } = 5000;
    }
} 