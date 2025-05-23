using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using ChromeConnect.Exceptions;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Centralized error handling service for ChromeConnect.
    /// </summary>
    public class ErrorHandler
    {
        private readonly ILogger<ErrorHandler> _logger;
        private readonly IScreenshotCapture _screenshotCapture;
        private readonly Dictionary<Type, Func<Exception, IWebDriver, Task>> _exceptionHandlers;
        private readonly ErrorHandlerSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorHandler"/> class.
        /// </summary>
        /// <param name="logger">The logger to use for error messages.</param>
        /// <param name="screenshotCapture">The screenshot capture service.</param>
        /// <param name="settings">Optional settings for the error handler.</param>
        public ErrorHandler(
            ILogger<ErrorHandler> logger,
            IScreenshotCapture screenshotCapture,
            ErrorHandlerSettings settings = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _screenshotCapture = screenshotCapture ?? throw new ArgumentNullException(nameof(screenshotCapture));
            _settings = settings ?? new ErrorHandlerSettings();
            
            // Initialize exception type handlers
            _exceptionHandlers = new Dictionary<Type, Func<Exception, IWebDriver, Task>>
            {
                { typeof(BrowserException), HandleBrowserExceptionAsync },
                { typeof(LoginException), HandleLoginExceptionAsync },
                { typeof(NetworkException), HandleNetworkExceptionAsync },
                { typeof(AppSystemException), HandleSystemExceptionAsync }
            };
        }

        /// <summary>
        /// Handles an exception with appropriate logging and actions.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <param name="driver">The WebDriver instance associated with the exception, if any.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task HandleExceptionAsync(Exception exception, IWebDriver driver = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            string screenshotPath = null;

            try
            {
                // Capture screenshot if driver is available
                if (driver != null && _settings.CaptureScreenshots)
                {
                    string prefix = DetermineScreenshotPrefix(exception);
                    screenshotPath = _screenshotCapture.CaptureScreenshot(driver, prefix);
                }

                // Log the exception with appropriate level
                LogException(exception, screenshotPath);

                // Handle specific exception types
                await HandleSpecificExceptionAsync(exception, driver);

                // Execute global error actions (e.g., cleanup)
                await ExecuteGlobalErrorActionsAsync(exception, driver);
            }
            catch (Exception handlerException)
            {
                // Log errors in the error handler itself
                _logger.LogError(handlerException, "Error occurred while handling another exception: {OriginalError}", 
                    exception.Message);
                
                // Make sure we clean up resources even if handling fails
                CleanupResources(driver);
            }
        }

        /// <summary>
        /// Wraps an asynchronous action with error handling.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="driver">The WebDriver instance to use for screenshots if an error occurs.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExecuteWithErrorHandlingAsync(Func<Task> action, IWebDriver driver = null)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, driver);
                throw; // Rethrow to let caller know an error occurred
            }
        }

        /// <summary>
        /// Wraps an asynchronous function with error handling.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <param name="driver">The WebDriver instance to use for screenshots if an error occurs.</param>
        /// <returns>A task representing the asynchronous operation, with the function's result.</returns>
        public async Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> func, IWebDriver driver = null)
        {
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, driver);
                throw; // Rethrow to let caller know an error occurred
            }
        }

        /// <summary>
        /// Executes an action with retry logic for transient failures.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="driver">The WebDriver instance to use for screenshots if an error occurs.</param>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="retryDelayMs">The delay in milliseconds between retries.</param>
        /// <param name="shouldRetryFunc">A function that determines if a particular exception should be retried.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExecuteWithRetryAsync(
            Func<Task> action,
            IWebDriver driver = null,
            int? retryCount = null,
            int? retryDelayMs = null,
            Func<Exception, bool> shouldRetryFunc = null,
            CancellationToken cancellationToken = default)
        {
            int maxRetries = retryCount ?? _settings.DefaultRetryCount;
            int retryDelay = retryDelayMs ?? _settings.DefaultRetryDelayMs;
            
            shouldRetryFunc ??= IsTransientException;
            
            int attempt = 0;
            
            while (true)
            {
                attempt++;
                
                try
                {
                    await action();
                    return; // Success
                }
                catch (Exception ex)
                {
                    if (attempt > maxRetries || !shouldRetryFunc(ex) || cancellationToken.IsCancellationRequested)
                    {
                        // We've exhausted our retries, or this exception type is not suitable for retry
                        await HandleExceptionAsync(ex, driver);
                        throw; // Rethrow to let caller know an error occurred
                    }

                    _logger.LogWarning(ex, "Attempt {Attempt}/{MaxRetries} failed: {Message}. Retrying after {Delay}ms...", 
                        attempt, maxRetries, ex.Message, retryDelay);
                    
                    // Wait and try again
                    await Task.Delay(retryDelay, cancellationToken);
                    
                    // Increase delay for next retry (exponential backoff)
                    retryDelay = CalculateExponentialBackoff(retryDelay, attempt);
                }
            }
        }

        /// <summary>
        /// Executes a function with retry logic for transient failures.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <param name="driver">The WebDriver instance to use for screenshots if an error occurs.</param>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="retryDelayMs">The delay in milliseconds between retries.</param>
        /// <param name="shouldRetryFunc">A function that determines if a particular exception should be retried.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation, with the function's result.</returns>
        public async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> func,
            IWebDriver driver = null,
            int? retryCount = null,
            int? retryDelayMs = null,
            Func<Exception, bool> shouldRetryFunc = null,
            CancellationToken cancellationToken = default)
        {
            int maxRetries = retryCount ?? _settings.DefaultRetryCount;
            int retryDelay = retryDelayMs ?? _settings.DefaultRetryDelayMs;
            
            shouldRetryFunc ??= IsTransientException;
            
            int attempt = 0;
            
            while (true)
            {
                attempt++;
                
                try
                {
                    return await func();
                }
                catch (Exception ex)
                {
                    if (attempt > maxRetries || !shouldRetryFunc(ex) || cancellationToken.IsCancellationRequested)
                    {
                        // We've exhausted our retries, or this exception type is not suitable for retry
                        await HandleExceptionAsync(ex, driver);
                        throw; // Rethrow to let caller know an error occurred
                    }

                    _logger.LogWarning(ex, "Attempt {Attempt}/{MaxRetries} failed: {Message}. Retrying after {Delay}ms...", 
                        attempt, maxRetries, ex.Message, retryDelay);
                    
                    // Wait and try again
                    await Task.Delay(retryDelay, cancellationToken);
                    
                    // Increase delay for next retry (exponential backoff)
                    retryDelay = CalculateExponentialBackoff(retryDelay, attempt);
                }
            }
        }

        #region Private Methods

        /// <summary>
        /// Handles a specific exception type based on its type.
        /// </summary>
        private async Task HandleSpecificExceptionAsync(Exception exception, IWebDriver driver)
        {
            // Find the most specific exception handler for this exception type
            Type exceptionType = exception.GetType();
            
            foreach (var handler in _exceptionHandlers)
            {
                if (handler.Key.IsAssignableFrom(exceptionType))
                {
                    await handler.Value(exception, driver);
                    return;
                }
            }
            
            // If no specific handler is found, treat as a general exception
            await HandleGeneralExceptionAsync(exception, driver);
        }

        /// <summary>
        /// Handles a browser-related exception.
        /// </summary>
        private Task HandleBrowserExceptionAsync(Exception exception, IWebDriver driver)
        {
            if (exception is BrowserInitializationException)
            {
                _logger.LogError(exception, "Browser initialization failed. Ensure Google Chrome is installed and up-to-date.");
            }
            else if (exception is BrowserNavigationException)
            {
                _logger.LogError(exception, "Browser navigation failed. Please check the URL and internet connection.");
            }
            else if (exception is BrowserTimeoutException timeoutEx)
            {
                _logger.LogError(exception, "Browser operation timed out after {Timeout}ms: {Operation}", 
                    timeoutEx.TimeoutMilliseconds, timeoutEx.Operation);
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a login-related exception.
        /// </summary>
        private Task HandleLoginExceptionAsync(Exception exception, IWebDriver driver)
        {
            if (exception is LoginFormNotFoundException)
            {
                _logger.LogError(exception, "Login form not found. The website structure may have changed.");
            }
            else if (exception is CredentialEntryException credEx)
            {
                _logger.LogError(exception, "Failed to enter credentials in {FieldType} field. The form structure may have changed.", 
                    credEx.FieldType);
            }
            else if (exception is InvalidCredentialsException invalidCredsEx)
            {
                _logger.LogError(exception, "Login failed due to invalid credentials. Errors: {Errors}", 
                    string.Join(", ", invalidCredsEx.ErrorMessages));
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a network-related exception.
        /// </summary>
        private Task HandleNetworkExceptionAsync(Exception exception, IWebDriver driver)
        {
            if (exception is ConnectionFailedException connEx)
            {
                _logger.LogError(exception, "Failed to connect to {Url}. Please check your internet connection.", 
                    connEx.TargetUrl);
            }
            else if (exception is CertificateException certEx)
            {
                _logger.LogError(exception, "Certificate validation failed for {Url}. {Errors}", 
                    certEx.TargetUrl, certEx.ValidationErrors);
            }
            else if (exception is RequestTimeoutException timeoutEx)
            {
                _logger.LogError(exception, "Request to {Url} timed out after {Timeout}ms", 
                    timeoutEx.RequestUrl, timeoutEx.TimeoutMilliseconds);
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a system-related exception.
        /// </summary>
        private Task HandleSystemExceptionAsync(Exception exception, IWebDriver driver)
        {
            if (exception is ConfigurationException configEx)
            {
                _logger.LogError(exception, "Configuration error with parameter: {Parameter}", 
                    configEx.ParameterName);
            }
            else if (exception is FileOperationException fileEx)
            {
                _logger.LogError(exception, "File operation '{Operation}' failed on {Path}", 
                    fileEx.OperationType, fileEx.FilePath);
            }
            else if (exception is ResourceNotFoundException resourceEx)
            {
                _logger.LogError(exception, "Resource not found: {Resource} at {Path}", 
                    resourceEx.ResourceName, resourceEx.ResourcePath);
            }
            else if (exception is AppOperationCanceledException cancelEx)
            {
                _logger.LogWarning(exception, "Operation '{Operation}' was canceled at {Time}", 
                    cancelEx.OperationName, cancelEx.CancelTime);
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a general exception that doesn't match any specific type.
        /// </summary>
        private Task HandleGeneralExceptionAsync(Exception exception, IWebDriver driver)
        {
            _logger.LogError(exception, "An unexpected error occurred: {Message}", exception.Message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes global error actions that should happen for all exception types.
        /// </summary>
        private async Task ExecuteGlobalErrorActionsAsync(Exception exception, IWebDriver driver)
        {
            // Cleanup resources
            CleanupResources(driver);
            
            // Fire error event if registered
            if (_settings.OnError != null)
            {
                try
                {
                    await _settings.OnError(exception);
                }
                catch (Exception eventEx)
                {
                    _logger.LogError(eventEx, "Error in error event handler");
                }
            }
        }

        /// <summary>
        /// Logs an exception with an appropriate log level.
        /// </summary>
        private void LogException(Exception exception, string screenshotPath)
        {
            // Determine log level based on exception type
            LogLevel logLevel = DetermineLogLevel(exception);
            
            // Add screenshot path to exception context if available
            string screenshotInfo = string.IsNullOrEmpty(screenshotPath) 
                ? string.Empty 
                : $" Screenshot saved to: {screenshotPath}";
            
            // Extract additional context information
            string context = ExtractExceptionContext(exception);
            
            // Log the exception
            _logger.Log(logLevel, exception, "{ExceptionType}: {Message}{Context}{ScreenshotInfo}", 
                exception.GetType().Name, 
                exception.Message,
                context,
                screenshotInfo);
        }

        /// <summary>
        /// Cleans up resources associated with the error.
        /// </summary>
        private void CleanupResources(IWebDriver driver)
        {
            if (driver != null && _settings.CloseDriverOnError)
            {
                try
                {
                    driver.Quit();
                    _logger.LogDebug("Browser driver has been closed after error");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to close browser driver after error");
                }
            }
        }

        /// <summary>
        /// Determines the screenshot prefix to use based on the exception type.
        /// </summary>
        private string DetermineScreenshotPrefix(Exception exception)
        {
            string exceptionName = exception.GetType().Name;
            
            // Remove "Exception" suffix if present
            if (exceptionName.EndsWith("Exception", StringComparison.Ordinal))
                exceptionName = exceptionName.Substring(0, exceptionName.Length - 9);
            
            return $"Error_{exceptionName}";
        }

        /// <summary>
        /// Determines the appropriate log level for an exception.
        /// </summary>
        private LogLevel DetermineLogLevel(Exception exception)
        {
            // Canceled operations are warnings, not errors
            if (exception is AppOperationCanceledException)
                return LogLevel.Warning;
                
            // Invalid credentials is a warning (user error, not system error)
            if (exception is InvalidCredentialsException)
                return LogLevel.Warning;
                
            // All other exceptions are errors
            return LogLevel.Error;
        }

        /// <summary>
        /// Extracts context information from an exception, if available.
        /// </summary>
        private string ExtractExceptionContext(Exception exception)
        {
            if (exception is ChromeConnectException ccEx && !string.IsNullOrEmpty(ccEx.Context))
                return $" ({ccEx.Context})";
                
            return string.Empty;
        }

        /// <summary>
        /// Determines if an exception is transient and suitable for retry.
        /// </summary>
        private bool IsTransientException(Exception exception)
        {
            // Network-related exceptions are usually transient
            if (exception is ConnectionFailedException ||
                exception is RequestTimeoutException)
                return true;
                
            // WebDriver-specific transient exceptions
            if (exception is WebDriverTimeoutException ||
                exception is WebDriverException wdEx && 
                (wdEx.Message.Contains("timed out") ||
                 wdEx.Message.Contains("net::ERR_CONNECTION_") ||
                 wdEx.Message.Contains("net::ERR_NETWORK_")))
                return true;
                
            // System exceptions that are usually transient
            if (exception is System.Net.WebException ||
                exception is System.IO.IOException ||
                exception is System.TimeoutException)
                return true;
                
            return false;
        }

        /// <summary>
        /// Calculates the delay for the next retry using exponential backoff.
        /// </summary>
        private int CalculateExponentialBackoff(int currentDelay, int attempt)
        {
            double backoff = currentDelay * Math.Pow(_settings.BackoffMultiplier, attempt - 1);
            
            // Add jitter to avoid thundering herd problem
            if (_settings.AddJitter)
            {
                double jitter = new Random().NextDouble() * 0.3 - 0.15; // ±15%
                backoff *= (1 + jitter);
            }
            
            return (int)Math.Min(backoff, _settings.MaxRetryDelayMs);
        }

        #endregion
    }

    /// <summary>
    /// Settings for the ErrorHandler class.
    /// </summary>
    public class ErrorHandlerSettings
    {
        /// <summary>
        /// Gets or sets whether to capture screenshots on error.
        /// </summary>
        public bool CaptureScreenshots { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to close the WebDriver on error.
        /// </summary>
        public bool CloseDriverOnError { get; set; } = true;

        /// <summary>
        /// Gets or sets the default number of retry attempts.
        /// </summary>
        public int DefaultRetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the default retry delay in milliseconds.
        /// </summary>
        public int DefaultRetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum retry delay in milliseconds.
        /// </summary>
        public int MaxRetryDelayMs { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the backoff multiplier for retry delays.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets whether to add jitter to retry delays.
        /// </summary>
        public bool AddJitter { get; set; } = true;

        /// <summary>
        /// Gets or sets a callback that will be called when an error occurs.
        /// </summary>
        public Func<Exception, Task> OnError { get; set; }
    }
} 