using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using ChromeConnect.Core;
using ChromeConnect.Models;
using ChromeConnect.Exceptions;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Main service that orchestrates the ChromeConnect workflow.
    /// </summary>
    public class ChromeConnectService
    {
        private readonly ILogger<ChromeConnectService> _logger;
        private readonly BrowserManager _browserManager;
        private readonly LoginDetector _loginDetector;
        private readonly CredentialManager _credentialManager;
        private readonly LoginVerifier _loginVerifier;
        private readonly IScreenshotCapture _screenshotCapture;
        private readonly ErrorHandler _errorHandler;
        private readonly TimeoutManager _timeoutManager;
        private readonly ErrorMonitor _errorMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromeConnectService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="browserManager">The browser manager instance.</param>
        /// <param name="loginDetector">The login form detector instance.</param>
        /// <param name="credentialManager">The credential manager instance.</param>
        /// <param name="loginVerifier">The login verifier instance.</param>
        /// <param name="screenshotCapture">The screenshot capture service.</param>
        /// <param name="errorHandler">The error handler instance.</param>
        /// <param name="timeoutManager">The timeout manager instance.</param>
        /// <param name="errorMonitor">The error monitor instance.</param>
        public ChromeConnectService(
            ILogger<ChromeConnectService> logger,
            BrowserManager browserManager,
            LoginDetector loginDetector,
            CredentialManager credentialManager,
            LoginVerifier loginVerifier,
            IScreenshotCapture screenshotCapture,
            ErrorHandler errorHandler,
            TimeoutManager timeoutManager,
            ErrorMonitor errorMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _browserManager = browserManager ?? throw new ArgumentNullException(nameof(browserManager));
            _loginDetector = loginDetector ?? throw new ArgumentNullException(nameof(loginDetector));
            _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
            _loginVerifier = loginVerifier ?? throw new ArgumentNullException(nameof(loginVerifier));
            _screenshotCapture = screenshotCapture ?? throw new ArgumentNullException(nameof(screenshotCapture));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _timeoutManager = timeoutManager ?? throw new ArgumentNullException(nameof(timeoutManager));
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
        }

        /// <summary>
        /// Executes the ChromeConnect workflow with the specified options.
        /// </summary>
        /// <param name="options">The command-line options.</param>
        /// <returns>The exit code of the operation.</returns>
        public async Task<int> ExecuteAsync(CommandLineOptions options)
        {
            _logger.LogInformation("ChromeConnect starting");
            IWebDriver driver = null;

            // Log options (masking sensitive fields)
            LogCommandLineOptions(options);

            try
            {
                // Launch the browser with error handling
                driver = await _errorHandler.ExecuteWithErrorHandlingAsync(async () =>
                {
                    _logger.LogInformation("Launching Chrome browser");
                    return _browserManager.LaunchBrowser(
                        options.Url,
                        options.Incognito,
                        options.Kiosk,
                        options.IgnoreCertErrors);
                });

                if (driver == null)
                {
                    _logger.LogError("Failed to launch browser");
                    return 1;
                }

                // Perform the login process with error handling and retry for transient issues
                return await _errorHandler.ExecuteWithRetryAsync(async () =>
                {
                    // Detect login form with timeout handling
                    var loginForm = await _timeoutManager.ExecuteWithTimeoutAsync(async () =>
                    {
                        _logger.LogInformation("Detecting login form");
                        return await _loginDetector.DetectLoginFormAsync(driver);
                    }, operationName: "DetectLoginForm");

                    if (loginForm == null)
                    {
                        throw new LoginFormNotFoundException("Could not detect login form", driver.Url);
                    }

                    // Enter credentials with timeout handling
                    bool credentialsEntered = await _timeoutManager.ExecuteWithTimeoutAsync(async () =>
                    {
                        _logger.LogInformation("Entering credentials");
                        return await _credentialManager.EnterCredentialsAsync(
                            driver, loginForm, options.Username, options.Password, options.Domain);
                    }, operationName: "EnterCredentials");

                    if (!credentialsEntered)
                    {
                        throw new CredentialEntryException("Failed to enter credentials", 
                            loginForm.UsernameField != null ? "username" : "password");
                    }

                    // Verify login success with timeout handling
                    bool loginSuccess = await _timeoutManager.ExecuteWithTimeoutAsync(async () =>
                    {
                        _logger.LogInformation("Verifying login success");
                        return await _loginVerifier.VerifyLoginSuccessAsync(driver);
                    }, operationName: "VerifyLogin");

                    if (loginSuccess)
                    {
                        _logger.LogInformation("Login successful!");
                        _logger.LogInformation("Browser will remain open. Script exiting.");
                        return 0; // Success
                    }
                    else
                    {
                        _screenshotCapture.CaptureScreenshot(driver, "LoginFailed");
                        _logger.LogError("Login failed");
                        throw new InvalidCredentialsException("Login verification failed - invalid credentials or access denied");
                    }
                }, driver, shouldRetryFunc: ex => ex is ConnectionFailedException || ex is RequestTimeoutException);
            }
            catch (ChromeConnectException ex)
            {
                // Record the error in the monitoring system
                _errorMonitor.RecordError(ex, "ChromeConnectService");

                // Already logged by the error handler
                return DetermineExitCode(ex);
            }
            catch (Exception ex)
            {
                // Handle unexpected exceptions
                await _errorHandler.HandleExceptionAsync(ex, driver);
                return 2; // General error code
            }
            finally
            {
                // Clean up resources if needed
                CleanupResources(driver);
            }
        }

        /// <summary>
        /// Logs command-line options, masking sensitive information.
        /// </summary>
        private void LogCommandLineOptions(CommandLineOptions options)
        {
            _logger.LogInformation("URL: {Url}", options.Url);
            _logger.LogInformation("Username: {Username}", options.Username);
            _logger.LogInformation("Password: ****");
            _logger.LogInformation("Domain: {Domain}", options.Domain);
            _logger.LogInformation("Incognito: {Incognito}", options.Incognito ? "yes" : "no");
            _logger.LogInformation("Kiosk: {Kiosk}", options.Kiosk ? "yes" : "no");
            _logger.LogInformation("Certificate Validation: {CertVal}", options.IgnoreCertErrors ? "ignore" : "enforce");
        }

        /// <summary>
        /// Determines the appropriate exit code based on the exception type.
        /// </summary>
        private int DetermineExitCode(Exception ex)
        {
            // Exit codes:
            // 0 - Success
            // 1 - Login failure (invalid credentials, login form not found, etc.)
            // 2 - System error (browser not launching, network error, etc.)
            // 3 - Configuration error (invalid settings, missing files, etc.)

            if (ex is LoginException)
            {
                return 1; // Login-related error
            }
            else if (ex is AppSystemException || ex is ConfigurationException)
            {
                return 3; // Configuration error
            }
            else
            {
                return 2; // General system error
            }
        }

        /// <summary>
        /// Cleans up resources if needed.
        /// </summary>
        private void CleanupResources(IWebDriver driver)
        {
            if (driver != null)
            {
                try
                {
                    // Close browser if it's still open
                    _browserManager.CloseBrowser(driver);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing browser during cleanup");
                }
            }
        }
    }
} 