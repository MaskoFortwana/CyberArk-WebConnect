using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Services;

namespace ChromeConnect.Core;

public class LoginVerifier : IScreenshotCapture
{
    private readonly ILogger<LoginVerifier> _logger;
    private readonly LoginVerificationConfig _config;

    public LoginVerifier(ILogger<LoginVerifier> logger, LoginVerificationConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new LoginVerificationConfig();
    }

    public virtual async Task<bool> VerifyLoginSuccessAsync(IWebDriver driver)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogInformation("Starting login verification with {MaxDuration}s timeout", _config.MaxVerificationTimeSeconds);
            
            // Immediate fast-fail checks (under 500ms)
            if (await QuickErrorDetectionAsync(driver))
            {
                _logger.LogError("Login failed - immediate error detected in {Duration}ms", 
                    (DateTime.Now - startTime).TotalMilliseconds);
                CaptureScreenshot(driver, "LoginFailed_QuickDetection");
                return false;
            }

            // Progressive verification with escalating timeouts
            var progressiveResult = await ProgressiveVerificationAsync(driver, startTime);
            
            var totalDuration = DateTime.Now - startTime;
            _logger.LogInformation("Login verification completed in {Duration}ms with result: {Result}", 
                totalDuration.TotalMilliseconds, progressiveResult);
                
            return progressiveResult;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Error during login verification after {Duration}ms", duration.TotalMilliseconds);
            CaptureScreenshot(driver, "VerificationError");
            return false;
        }
    }

    /// <summary>
    /// Quick error detection with under 500ms timeout for immediate failures
    /// </summary>
    private async Task<bool> QuickErrorDetectionAsync(IWebDriver driver)
    {
        try
        {
            // Quick check for immediate error indicators (100ms timeout)
            var quickWait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(100));
            quickWait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(WebDriverTimeoutException));
            
            // Check for immediate error messages
            var errorSelectors = new[]
            {
                "div.error:not(:empty)",
                "span.error:not(:empty)", 
                "p.error:not(:empty)",
                "div.alert-danger:not(:empty)",
                "div.alert-error:not(:empty)",
                "[role='alert']:not(:empty)"
            };

            foreach (var selector in errorSelectors)
            {
                try
                {
                    var errorElement = quickWait.Until(d => d.FindElement(By.CssSelector(selector)));
                    if (errorElement?.Displayed == true && !string.IsNullOrWhiteSpace(errorElement.Text))
                    {
                        _logger.LogDebug("Quick error detected: {Selector} - {Text}", selector, errorElement.Text);
                        return true;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            // Check page source for immediate error text patterns (only first 5000 chars for speed)
            var pageSource = driver.PageSource;
            if (pageSource.Length > 5000) pageSource = pageSource.Substring(0, 5000);
            
            var errorPatterns = new[] { "invalid credentials", "login failed", "incorrect password", "access denied" };
            foreach (var pattern in errorPatterns)
            {
                if (pageSource.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Quick error pattern detected: {Pattern}", pattern);
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If quick detection fails, continue with normal verification
            return false;
        }
    }

    /// <summary>
    /// Progressive verification with escalating timeouts for thorough checking
    /// </summary>
    private async Task<bool> ProgressiveVerificationAsync(IWebDriver driver, DateTime startTime)
    {
        var maxDuration = TimeSpan.FromSeconds(_config.MaxVerificationTimeSeconds);
        
        // Phase 1: Quick success indicators (2 seconds)
        await Task.Delay(_config.InitialDelayMs);
        
        if (DateTime.Now - startTime > maxDuration) return false;
        
        if (await CheckUrlChangedAsync(driver, 2))
        {
            _logger.LogInformation("URL changed, login appears successful");
            return true;
        }

        // Phase 2: Form disappearance check (3 seconds total)
        if (DateTime.Now - startTime > maxDuration) return false;
        
        if (await CheckLoginFormGoneAsync(driver))
        {
            _logger.LogInformation("Login form no longer present, login appears successful");
            return true;
        }

        // Phase 3: Success elements check (5 seconds total)
        if (DateTime.Now - startTime > maxDuration) return false;
        
        if (await CheckForSuccessElementsAsync(driver, 2))
        {
            _logger.LogInformation("Found elements indicating successful login");
            return true;
        }

        // Phase 4: Comprehensive error check (7 seconds total)  
        if (DateTime.Now - startTime > maxDuration) return false;
        
        if (await CheckForErrorMessagesAsync(driver, 2))
        {
            _logger.LogError("Found error messages indicating failed login");
            CaptureScreenshot(driver, "LoginFailed_DetailedCheck");
            return false;
        }

        // Phase 5: Final timeout check
        var remainingTime = maxDuration - (DateTime.Now - startTime);
        if (remainingTime.TotalSeconds > 1)
        {
            _logger.LogDebug("Waiting {RemainingSeconds}s for final verification", remainingTime.TotalSeconds);
            await Task.Delay((int)remainingTime.TotalMilliseconds);
        }

        // Default assumption - if no clear success or failure, assume success to avoid blocking
        _logger.LogInformation("No definitive result found within timeout, assuming success");
        return true;
    }

    private async Task<bool> CheckUrlChangedAsync(IWebDriver driver, int timeoutSeconds = 2)
    {
        try
        {
            string currentUrl = driver.Url;
            _logger.LogDebug("Current URL: {Url}", currentUrl);

            // Quick check if URL still looks like a login page
            var loginIndicators = new[] { "login", "signin", "auth", "logon" };
            bool stillOnLoginPage = loginIndicators.Any(indicator => 
                currentUrl.Contains(indicator, StringComparison.OrdinalIgnoreCase));

            if (!stillOnLoginPage)
            {
                return true; // Successfully navigated away from login page
            }

            // For login-like URLs, check for error indicators in page content
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                wait.Until(d => !d.PageSource.Contains("incorrect", StringComparison.OrdinalIgnoreCase) &&
                               !d.PageSource.Contains("invalid", StringComparison.OrdinalIgnoreCase) &&
                               !d.PageSource.Contains("failed", StringComparison.OrdinalIgnoreCase));
                return true;
            }
            catch (WebDriverTimeoutException)
            {
                // Still on login page with possible errors
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking URL change");
            return false;
        }
    }

    private async Task<bool> CheckLoginFormGoneAsync(IWebDriver driver)
    {
        try
        {
            // Quick check if password field is still present and visible
            var passwordFields = driver.FindElements(By.CssSelector("input[type='password']"));
            if (passwordFields.Count == 0)
            {
                return true; // No password field found, likely logged in
            }
            
            // Check if password field exists but is hidden (common after login)
            return !passwordFields[0].Displayed;
        }
        catch
        {
            // If we can't find password fields, that's likely a good sign
            return true;
        }
    }

    private async Task<bool> CheckForSuccessElementsAsync(IWebDriver driver, int timeoutSeconds = 2)
    {
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException));

            // Quick CSS selector checks for common success indicators
            var successSelectors = new[]
            {
                "a[href*='logout']", "a[href*='signout']", "button[onclick*='logout']",
                "div.welcome", "span.username", "div.dashboard", "nav.user-menu",
                ".logged-in", ".user-authenticated", "#user-menu"
            };

            foreach (var selector in successSelectors)
            {
                try
                {
                    var element = wait.Until(d => d.FindElement(By.CssSelector(selector)));
                    if (element?.Displayed == true)
                    {
                        _logger.LogDebug("Found success indicator: {Selector}", selector);
                        return true;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking for success elements");
            return false;
        }
    }

    private async Task<bool> CheckForErrorMessagesAsync(IWebDriver driver, int timeoutSeconds = 2)
    {
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException));

            // Comprehensive error selectors with visible text check
            var errorSelectors = new[]
            {
                "div.error:not(:empty)", "span.error:not(:empty)", "p.error:not(:empty)",
                "div.alert:not(:empty)", "div.alert-danger:not(:empty)", "div.alert-error:not(:empty)",
                "[role='alert']:not(:empty)", ".validation-error:not(:empty)", 
                ".field-error:not(:empty)", ".login-error:not(:empty)"
            };

            foreach (var selector in errorSelectors)
            {
                try
                {
                    var element = wait.Until(d => d.FindElement(By.CssSelector(selector)));
                    if (element?.Displayed == true && !string.IsNullOrWhiteSpace(element.Text))
                    {
                        _logger.LogDebug("Found error indicator: {Selector} with text: {Text}", 
                            selector, element.Text);
                        return true;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking for error messages");
            return false;
        }
    }

    /// <summary>
    /// Captures a screenshot of the current browser state.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture from.</param>
    /// <param name="prefix">A prefix for the screenshot filename.</param>
    /// <returns>The path to the saved screenshot, or null if the capture failed.</returns>
    public string CaptureScreenshot(IWebDriver driver, string prefix)
    {
        try
        {
            if (driver is ITakesScreenshot screenshotDriver)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
                string filename = $"{prefix}_{timestamp}.png";
                string filePath = Path.Combine("screenshots", filename);
                
                // Ensure directory exists
                Directory.CreateDirectory("screenshots");
                
                Screenshot screenshot = screenshotDriver.GetScreenshot();
                screenshot.SaveAsFile(filePath);
                
                _logger.LogInformation("Screenshot captured: {FilePath}", filePath);
                return filePath;
            }
            else
            {
                _logger.LogWarning("WebDriver does not support screenshot capture");
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot with prefix: {Prefix}", prefix);
            return string.Empty;
        }
    }
}

/// <summary>
/// Configuration for login verification behavior
/// </summary>
public class LoginVerificationConfig
{
    /// <summary>
    /// Maximum time to spend on login verification in seconds
    /// </summary>
    public int MaxVerificationTimeSeconds { get; set; } = 10;

    /// <summary>
    /// Initial delay before starting verification checks in milliseconds
    /// </summary>
    public int InitialDelayMs { get; set; } = 500;

    /// <summary>
    /// Whether to enable detailed timing logs
    /// </summary>
    public bool EnableTimingLogs { get; set; } = true;

    /// <summary>
    /// Whether to capture screenshots on verification failures
    /// </summary>
    public bool CaptureScreenshotsOnFailure { get; set; } = true;
}
