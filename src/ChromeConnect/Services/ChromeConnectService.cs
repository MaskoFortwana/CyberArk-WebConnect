using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using ChromeConnect.Core;
using ChromeConnect.Models;
using ChromeConnect.Exceptions;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

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
        
        // NEW: Flag to control browser cleanup behavior
        private bool _shouldCloseBrowserOnCleanup = true;

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
                    var loginForm = await _timeoutManager.ExecuteWithTimeoutAsync<LoginFormElements>(async (CancellationToken tokenFromManager) =>
                    {
                        _logger.LogInformation("Detecting login form");
                        return await _loginDetector.DetectLoginFormAsync(driver);
                    }, operationName: "DetectLoginForm");

                    if (loginForm == null)
                    {
                        throw new LoginFormNotFoundException("Could not detect login form", driver.Url);
                    }

                    // Capture initial state for fast verification (before login attempt)
                    string initialUrl = driver.Url;
                    string initialTitle = driver.Title;
                    
                    // Convert LoginFormElements to By[] array for fast verification (BEFORE credential entry to avoid stale elements)
                    var loginFormSelectors = new List<By>();
                    
                    // Build selectors safely with error handling for stale elements
                    try
                    {
                        if (loginForm.UsernameField != null)
                        {
                            try
                            {
                                var usernameId = loginForm.UsernameField.GetAttribute("id");
                                var usernameName = loginForm.UsernameField.GetAttribute("name");
                                if (!string.IsNullOrEmpty(usernameId))
                                    loginFormSelectors.Add(By.Id(usernameId));
                                else if (!string.IsNullOrEmpty(usernameName))
                                    loginFormSelectors.Add(By.Name(usernameName));
                                else
                                    loginFormSelectors.Add(By.CssSelector("input[type='text'], input[type='email']"));
                            }
                            catch (StaleElementReferenceException)
                            {
                                _logger.LogWarning("Username field became stale, using fallback selector");
                                loginFormSelectors.Add(By.CssSelector("input[type='text'], input[type='email']"));
                            }
                        }
                        
                        if (loginForm.PasswordField != null)
                        {
                            try
                            {
                                var passwordId = loginForm.PasswordField.GetAttribute("id");
                                var passwordName = loginForm.PasswordField.GetAttribute("name");
                                if (!string.IsNullOrEmpty(passwordId))
                                    loginFormSelectors.Add(By.Id(passwordId));
                                else if (!string.IsNullOrEmpty(passwordName))
                                    loginFormSelectors.Add(By.Name(passwordName));
                                else
                                    loginFormSelectors.Add(By.CssSelector("input[type='password']"));
                            }
                            catch (StaleElementReferenceException)
                            {
                                _logger.LogWarning("Password field became stale, using fallback selector");
                                loginFormSelectors.Add(By.CssSelector("input[type='password']"));
                            }
                        }
                        
                        if (loginForm.SubmitButton != null)
                        {
                            try
                            {
                                var submitId = loginForm.SubmitButton.GetAttribute("id");
                                var submitName = loginForm.SubmitButton.GetAttribute("name");
                                if (!string.IsNullOrEmpty(submitId))
                                    loginFormSelectors.Add(By.Id(submitId));
                                else if (!string.IsNullOrEmpty(submitName))
                                    loginFormSelectors.Add(By.Name(submitName));
                                else
                                    loginFormSelectors.Add(By.CssSelector("button[type='submit'], input[type='submit']"));
                            }
                            catch (StaleElementReferenceException)
                            {
                                _logger.LogWarning("Submit button became stale, using fallback selector");
                                loginFormSelectors.Add(By.CssSelector("button[type='submit'], input[type='submit']"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error building form selectors, using fallback selectors");
                        // Use generic fallback selectors if all else fails
                        loginFormSelectors.Clear();
                        loginFormSelectors.Add(By.CssSelector("input[type='text'], input[type='email']"));
                        loginFormSelectors.Add(By.CssSelector("input[type='password']"));
                        loginFormSelectors.Add(By.CssSelector("button[type='submit'], input[type='submit']"));
                    }

                    // Enter credentials with timeout handling
                    bool credentialsEntered = await _timeoutManager.ExecuteWithTimeoutAsync<bool>(
                        async (CancellationToken tokenFromManager) =>
                        {
                            _logger.LogInformation("Entering credentials");
                            bool result = await _credentialManager.EnterCredentialsAsync(
                                driver, loginForm, options.Username, options.Password, options.Domain);
                            return result;
                        }, 
                        operationName: "EnterCredentials");

                    if (!credentialsEntered)
                    {
                        throw new CredentialEntryException("Failed to enter credentials", 
                            loginForm.UsernameField != null ? "username" : "password");
                    }

                    // Use fast verification instead of old timeout-heavy method
                    _logger.LogInformation("Verifying login success using fast detection");
                    var sessionId = Guid.NewGuid().ToString("N")[..8];
                    bool loginSuccess = await _loginVerifier.FastVerifyLoginSuccess(
                        driver, initialUrl, initialTitle, loginFormSelectors.ToArray(), sessionId);

                    if (loginSuccess)
                    {
                        _logger.LogInformation("Login successful!");
                        _logger.LogInformation("Browser will remain open. Script exiting.");
                        
                        // Preserve browser session by preventing cleanup
                        _shouldCloseBrowserOnCleanup = false;
                        
                        return 0; // Success
                    }
                    else
                    {
                        // ENHANCED: Instead of immediately failing, assess the verification context
                        _screenshotCapture.CaptureScreenshot(driver, "LoginVerificationUncertain");
                        
                        // Check if this was a timeout/uncertainty vs definitive failure
                        // If the page looks like it might be successfully logged in, preserve browser
                        var assessmentResult = await AssessLoginUncertaintyAsync(driver);
                        
                        if (assessmentResult.ShouldPreserveBrowser)
                        {
                            _logger.LogWarning("Login verification failed but indicators suggest possible success. " +
                                              "Preserving browser session and exiting with uncertainty code. " +
                                              "Reason: {Reason}", assessmentResult.Reason);
                            
                            // Preserve browser session by preventing cleanup
                            _shouldCloseBrowserOnCleanup = false;
                            
                            // Return special exit code for uncertain cases (preserves browser)
                            return 3; // Uncertain result - browser preserved
                        }
                        else
                        {
                            _logger.LogError("Login verification failed with high confidence of failure. " +
                                           "Reason: {Reason}", assessmentResult.Reason);
                            throw new InvalidCredentialsException($"Login verification failed with high confidence: {assessmentResult.Reason}");
                        }
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
            // 3 - Uncertain verification result (browser preserved for manual inspection)
            // 4 - Configuration error (invalid settings, missing files, etc.)

            if (ex is LoginException)
            {
                return 1; // Login-related error
            }
            else if (ex is AppSystemException || ex is ConfigurationException)
            {
                return 4; // Configuration error (updated from 3)
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
            if (driver != null && _shouldCloseBrowserOnCleanup)
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
            
            // Always clean up ChromeDriver processes, regardless of browser cleanup setting
            try
            {
                BrowserManager.CleanupDriverProcesses();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up ChromeDriver processes");
            }
        }

        /// <summary>
        /// Assesses whether login verification failure is due to uncertainty vs definitive failure
        /// </summary>
        private async Task<LoginAssessmentResult> AssessLoginUncertaintyAsync(IWebDriver driver)
        {
            try
            {
                _logger.LogDebug("Assessing login uncertainty to determine browser preservation strategy");
                
                var currentUrl = driver.Url;
                var pageTitle = driver.Title;
                
                // Check for definitive error indicators first
                var definitiveErrors = await CheckForDefinitiveErrorsAsync(driver);
                if (definitiveErrors.HasDefinitiveErrors)
                {
                    return new LoginAssessmentResult
                    {
                        ShouldPreserveBrowser = false,
                        Reason = $"Definitive error indicators found: {string.Join(", ", definitiveErrors.ErrorIndicators)}"
                    };
                }
                
                // Check for positive success indicators
                var positiveIndicators = await CheckForPositiveIndicatorsAsync(driver);
                if (positiveIndicators.HasPositiveIndicators)
                {
                    return new LoginAssessmentResult
                    {
                        ShouldPreserveBrowser = true,
                        Reason = $"Positive success indicators found: {string.Join(", ", positiveIndicators.SuccessIndicators)}"
                    };
                }
                
                // Check URL for navigation away from login page
                var urlIndicators = CheckUrlForSuccessIndicators(currentUrl);
                if (urlIndicators.IndicatesSuccess)
                {
                    return new LoginAssessmentResult
                    {
                        ShouldPreserveBrowser = true,
                        Reason = urlIndicators.Reason
                    };
                }
                
                // Check for page structure changes
                var structureIndicators = await CheckPageStructureAsync(driver);
                if (structureIndicators.IndicatesSuccess)
                {
                    return new LoginAssessmentResult
                    {
                        ShouldPreserveBrowser = true,
                        Reason = structureIndicators.Reason
                    };
                }
                
                // If no definitive indicators either way, preserve browser on uncertainty
                return new LoginAssessmentResult
                {
                    ShouldPreserveBrowser = true,
                    Reason = "No definitive failure indicators found - preserving browser session due to uncertainty"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during login uncertainty assessment - defaulting to preserve browser");
                return new LoginAssessmentResult
                {
                    ShouldPreserveBrowser = true,
                    Reason = $"Assessment error occurred: {ex.Message} - preserving browser as safety measure"
                };
            }
        }

        /// <summary>
        /// Checks for definitive error indicators that confirm login failure
        /// </summary>
        private async Task<(bool HasDefinitiveErrors, List<string> ErrorIndicators)> CheckForDefinitiveErrorsAsync(IWebDriver driver)
        {
            var errorIndicators = new List<string>();
            
            try
            {
                // Check for explicit error messages
                var errorElements = driver.FindElements(By.CssSelector(
                    "div.error:not(:empty), span.error:not(:empty), p.error:not(:empty), " +
                    "div.alert-danger:not(:empty), div.alert-error:not(:empty), " +
                    "[role='alert']:not(:empty)[class*='error'], [role='alert']:not(:empty)[class*='danger'], " +
                    ".login-error:not(:empty), .authentication-error:not(:empty), " +
                    ".signin-error:not(:empty), .auth-error:not(:empty)"));
                
                foreach (var element in errorElements)
                {
                    try
                    {
                        if (element.Displayed && !string.IsNullOrWhiteSpace(element.Text))
                        {
                            var errorText = element.Text.ToLower();
                            
                            // Check for high-confidence error patterns
                            var definitiveErrorPatterns = new[]
                            {
                                "invalid credentials", "invalid username", "invalid password",
                                "incorrect password", "incorrect username", "login failed",
                                "authentication failed", "access denied", "account locked",
                                "account disabled", "account suspended"
                            };
                            
                            if (definitiveErrorPatterns.Any(pattern => errorText.Contains(pattern)))
                            {
                                errorIndicators.Add($"Error message: '{element.Text.Trim()}'");
                            }
                        }
                    }
                    catch (StaleElementReferenceException)
                    {
                        continue; // Element became stale, skip
                    }
                }
                
                // Check page source for error patterns
                var pageSource = driver.PageSource;
                if (pageSource.Length > 10000) 
                {
                    pageSource = pageSource.Substring(0, 10000); // Analyze first 10k chars for performance
                }
                
                var sourceErrorPatterns = new[]
                {
                    "invalid credentials", "login failed", "authentication failed",
                    "access denied", "incorrect password", "account locked"
                };
                
                foreach (var pattern in sourceErrorPatterns)
                {
                    if (pageSource.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        errorIndicators.Add($"Page source pattern: '{pattern}'");
                    }
                }
                
                return (errorIndicators.Any(), errorIndicators);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking for definitive error indicators");
                return (false, errorIndicators);
            }
        }

        /// <summary>
        /// Checks for positive indicators that suggest login success
        /// </summary>
        private async Task<(bool HasPositiveIndicators, List<string> SuccessIndicators)> CheckForPositiveIndicatorsAsync(IWebDriver driver)
        {
            var successIndicators = new List<string>();
            
            try
            {
                // Check for logout/signout elements (strong success indicators)
                var logoutElements = driver.FindElements(By.CssSelector(
                    "a[href*='logout'], a[href*='signout'], a[href*='sign-out'], " +
                    "button[onclick*='logout'], button[onclick*='signout'], " +
                    "*[data-action*='logout'], *[data-action*='signout']"));
                
                foreach (var element in logoutElements)
                {
                    try
                    {
                        if (element.Displayed)
                        {
                            successIndicators.Add($"Logout element found: {element.TagName}");
                            break; // One is enough
                        }
                    }
                    catch (StaleElementReferenceException)
                    {
                        continue;
                    }
                }
                
                // Check for user profile/menu elements
                var profileElements = driver.FindElements(By.CssSelector(
                    ".user-profile, .profile-menu, #user-menu, .user-dropdown, " +
                    ".account-menu, .user-header, .username, .user-name"));
                
                foreach (var element in profileElements)
                {
                    try
                    {
                        if (element.Displayed && !string.IsNullOrWhiteSpace(element.Text))
                        {
                            successIndicators.Add($"User element found: {element.TagName} with text '{element.Text.Trim()}'");
                            break; // One is enough
                        }
                    }
                    catch (StaleElementReferenceException)
                    {
                        continue;
                    }
                }
                
                // Check for dashboard/main content areas
                var contentElements = driver.FindElements(By.CssSelector(
                    ".dashboard, .main-content, .app-content, #main-content, " +
                    ".workspace, .workarea, .member-area"));
                
                foreach (var element in contentElements)
                {
                    try
                    {
                        if (element.Displayed)
                        {
                            successIndicators.Add($"Main content area found: {element.TagName}");
                            break; // One is enough
                        }
                    }
                    catch (StaleElementReferenceException)
                    {
                        continue;
                    }
                }
                
                return (successIndicators.Any(), successIndicators);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking for positive indicators");
                return (false, successIndicators);
            }
        }

        /// <summary>
        /// Checks URL for success indicators
        /// </summary>
        private (bool IndicatesSuccess, string Reason) CheckUrlForSuccessIndicators(string currentUrl)
        {
            try
            {
                var lowerUrl = currentUrl.ToLower();
                
                // Check for positive URL patterns
                var successPatterns = new[]
                {
                    "dashboard", "welcome", "home", "main", "portal", "app",
                    "logged", "authenticated", "success"
                };
                
                foreach (var pattern in successPatterns)
                {
                    if (lowerUrl.Contains(pattern))
                    {
                        return (true, $"URL contains success indicator: '{pattern}'");
                    }
                }
                
                // Check for movement away from login-specific URLs
                var loginPatterns = new[] { "login", "signin", "sign-in", "auth", "logon" };
                var hasLoginPattern = loginPatterns.Any(pattern => lowerUrl.Contains(pattern));
                
                if (!hasLoginPattern)
                {
                    return (true, "URL no longer contains login-specific patterns");
                }
                
                return (false, "URL analysis inconclusive");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error analyzing URL for success indicators");
                return (false, $"URL analysis error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks page structure for changes that indicate successful login
        /// </summary>
        private async Task<(bool IndicatesSuccess, string Reason)> CheckPageStructureAsync(IWebDriver driver)
        {
            try
            {
                // Check if login forms are no longer visible
                var loginElements = driver.FindElements(By.CssSelector(
                    "input[type='password'], form[action*='login'], form[action*='signin'], " +
                    ".login-form, .signin-form, #login-form, #signin-form"));
                
                var visibleLoginElements = loginElements.Where(e => {
                    try { return e.Displayed; } catch { return false; }
                }).ToList();
                
                if (!visibleLoginElements.Any())
                {
                    return (true, "No visible login form elements found - suggests successful navigation");
                }
                
                // Check for navigation/menu structures (common post-login)
                var navElements = driver.FindElements(By.CssSelector(
                    "nav, .navbar, .navigation, .sidebar, .menu, header nav"));
                
                var significantNavElements = navElements.Where(e => {
                    try 
                    { 
                        return e.Displayed && e.FindElements(By.TagName("a")).Count >= 3; // Has multiple links
                    } 
                    catch { return false; }
                }).ToList();
                
                if (significantNavElements.Any())
                {
                    return (true, $"Found {significantNavElements.Count} navigation structure(s) suggesting logged-in state");
                }
                
                return (false, "Page structure analysis inconclusive");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error analyzing page structure");
                return (false, $"Page structure analysis error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Result of assessing login uncertainty
    /// </summary>
    internal class LoginAssessmentResult
    {
        public bool ShouldPreserveBrowser { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
} 