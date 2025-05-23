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

    public LoginVerifier(ILogger<LoginVerifier> logger)
    {
        _logger = logger;
    }

    public virtual async Task<bool> VerifyLoginSuccessAsync(IWebDriver driver)
    {
        try
        {
            // Wait for page to load after login attempt
            await Task.Delay(2000);
            
            _logger.LogInformation("Checking login status");

            // Try several strategies to determine if login was successful
            if (await CheckUrlChangedAsync(driver))
            {
                _logger.LogInformation("URL changed, login appears successful");
                return true;
            }

            if (await CheckLoginFormGoneAsync(driver))
            {
                _logger.LogInformation("Login form no longer present, login appears successful");
                return true;
            }

            if (await CheckForSuccessElementsAsync(driver))
            {
                _logger.LogInformation("Found elements indicating successful login");
                return true;
            }

            if (await CheckForErrorMessagesAsync(driver))
            {
                _logger.LogError("Found error messages indicating failed login");
                CaptureScreenshot(driver, "LoginFailed");
                return false;
            }

            // Default assumption - if we've gotten this far without determining success or failure, 
            // assume login was successful
            _logger.LogInformation("Unable to definitively determine login status, assuming success");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying login status");
            CaptureScreenshot(driver, "VerificationError");
            return false;
        }
    }

    private async Task<bool> CheckUrlChangedAsync(IWebDriver driver)
    {
        string currentUrl = driver.Url;
        _logger.LogDebug("Current URL: {Url}", currentUrl);

        // Check if the URL contains common signs of login failure
        if (currentUrl.Contains("login") || 
            currentUrl.Contains("signin") || 
            currentUrl.Contains("auth") ||
            currentUrl.Contains("error"))
        {
            // These are common URL patterns for login pages - still being on such a URL 
            // could mean login failed
            
            // Check the page source for error messages
            if (driver.PageSource.Contains("incorrect") ||
                driver.PageSource.Contains("failed") ||
                driver.PageSource.Contains("invalid") ||
                driver.PageSource.Contains("wrong"))
            {
                _logger.LogInformation("Found error indicators on login page");
                return false;
            }
            
            // If we're still on a login-like page but don't see errors, 
            // possibly just a multi-step login
            return false;
        }

        // If we're on a page that doesn't appear to be a login page anymore, 
        // login was probably successful
        return true;
    }

    private async Task<bool> CheckLoginFormGoneAsync(IWebDriver driver)
    {
        try
        {
            // Check if password field is still present
            var passwordFields = driver.FindElements(By.CssSelector("input[type='password']"));
            if (passwordFields.Count == 0)
            {
                return true; // No password field found, likely logged in
            }
            
            // If password field exists, check if it's visible
            // Sometimes login forms are still in DOM but hidden after successful login
            return !passwordFields[0].Displayed;
        }
        catch
        {
            // If we can't find password fields, that's potentially a good sign
            return true;
        }
    }

    private async Task<bool> CheckForSuccessElementsAsync(IWebDriver driver)
    {
        // Check for elements that typically appear after successful login
        string[] successIndicators = {
            "//a[contains(., 'Logout')]",
            "//a[contains(., 'Sign Out')]",
            "//button[contains(., 'Logout')]",
            "//button[contains(., 'Sign Out')]",
            "//div[contains(@class, 'welcome')]",
            "//span[contains(@class, 'user-name')]",
            "//div[contains(@class, 'dashboard')]",
            "//div[contains(@class, 'profile')]"
        };

        foreach (var xpath in successIndicators)
        {
            try
            {
                var elements = driver.FindElements(By.XPath(xpath));
                if (elements.Count > 0 && elements[0].Displayed)
                {
                    _logger.LogDebug("Found success indicator: {Element}", xpath);
                    return true;
                }
            }
            catch { /* Continue to next indicator */ }
        }

        return false;
    }

    private async Task<bool> CheckForErrorMessagesAsync(IWebDriver driver)
    {
        // Check for elements that typically appear after failed login
        string[] errorIndicators = {
            "//div[contains(@class, 'error')]",
            "//span[contains(@class, 'error')]",
            "//p[contains(@class, 'error')]",
            "//div[contains(@class, 'alert')]",
            "//div[contains(@class, 'danger')]",
            "//p[contains(., 'incorrect')]",
            "//div[contains(., 'incorrect')]",
            "//span[contains(., 'incorrect')]",
            "//p[contains(., 'invalid')]",
            "//div[contains(., 'invalid')]",
            "//span[contains(., 'invalid')]",
            "//p[contains(., 'failed')]",
            "//div[contains(., 'failed')]",
            "//span[contains(., 'failed')]"
        };

        foreach (var xpath in errorIndicators)
        {
            try
            {
                var elements = driver.FindElements(By.XPath(xpath));
                if (elements.Count > 0 && elements[0].Displayed)
                {
                    _logger.LogDebug("Found error indicator: {Element} with text: {Text}", 
                        xpath, elements[0].Text);
                    return true;
                }
            }
            catch { /* Continue to next indicator */ }
        }

        return false;
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
            // Ensure screenshots directory exists
            string screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
            if (!Directory.Exists(screenshotDir))
            {
                Directory.CreateDirectory(screenshotDir);
            }

            // Generate unique filename with timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string screenshotPath = Path.Combine(
                screenshotDir, 
                $"{prefix}_{timestamp}.png");

            // Take screenshot
            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            screenshot.SaveAsFile(screenshotPath);

            _logger.LogInformation("Screenshot saved: {Path}", screenshotPath);
            
            return screenshotPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot");
            return null;
        }
    }
}
