using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Models;

namespace ChromeConnect.Core;

public class LoginDetector
{
    private readonly ILogger<LoginDetector> _logger;

    public LoginDetector(ILogger<LoginDetector> logger)
    {
        _logger = logger;
    }

    public async Task<LoginFormElements?> DetectLoginFormAsync(IWebDriver driver)
    {
        _logger.LogInformation("Starting login form detection");
        
        try
        {
            // Wait for page to load completely
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            await Task.Delay(1000); // Give the page a moment to render

            // Try multiple detection strategies
            var loginForm = DetectByCommonAttributes(driver);
            
            if (IsValidLoginForm(loginForm))
            {
                _logger.LogInformation("Login form detected using common attributes strategy");
                return loginForm;
            }

            loginForm = DetectByXPath(driver);
            
            if (IsValidLoginForm(loginForm))
            {
                _logger.LogInformation("Login form detected using XPath strategy");
                return loginForm;
            }

            _logger.LogWarning("Unable to detect login form using any strategy");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting login form");
            return null;
        }
    }

    private LoginFormElements DetectByCommonAttributes(IWebDriver driver)
    {
        _logger.LogDebug("Trying to detect login form using common attributes");
        
        var loginForm = new LoginFormElements();

        try
        {
            // Common username field selectors
            string[] usernameSelectors = {
                "input[type='text'][id*='user' i]",
                "input[type='text'][name*='user' i]",
                "input[type='text'][placeholder*='user' i]",
                "input[type='text'][id*='login' i]",
                "input[type='text'][name*='login' i]",
                "input[type='email']",
                "input[id*='email' i]",
                "input[name*='email' i]"
            };

            // Common password field selectors
            string[] passwordSelectors = {
                "input[type='password']"
            };

            // Common domain field selectors (may not always be present)
            string[] domainSelectors = {
                "input[id*='domain' i]",
                "input[name*='domain' i]",
                "select[id*='domain' i]",
                "select[name*='domain' i]",
                "input[id*='tenant' i]",
                "input[name*='tenant' i]"
            };

            // Common submit button selectors
            string[] submitButtonSelectors = {
                "button[type='submit']",
                "input[type='submit']",
                "button[id*='login' i]",
                "button[id*='submit' i]",
                "button[name*='login' i]",
                "button[name*='submit' i]",
                "button[class*='login' i]",
                "button[class*='submit' i]",
                "a[id*='login' i]",
                "a[class*='login' i]"
            };

            // Try to find username field
            foreach (var selector in usernameSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.UsernameField = elements[0];
                        break;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            // Try to find password field
            foreach (var selector in passwordSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.PasswordField = elements[0];
                        break;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            // Try to find domain field (optional)
            foreach (var selector in domainSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.DomainField = elements[0];
                        break;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            // Try to find submit button
            foreach (var selector in submitButtonSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.SubmitButton = elements[0];
                        break;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in common attributes detection strategy");
            return new LoginFormElements();
        }
    }

    private LoginFormElements DetectByXPath(IWebDriver driver)
    {
        _logger.LogDebug("Trying to detect login form using XPath");
        
        var loginForm = new LoginFormElements();

        try
        {
            // XPath expressions for login form elements
            string[] usernameXPaths = {
                "//input[contains(translate(@id, 'USERNAME', 'username'), 'username')]",
                "//input[contains(translate(@name, 'USERNAME', 'username'), 'username')]",
                "//input[contains(translate(@placeholder, 'USERNAME', 'username'), 'username')]",
                "//label[contains(translate(., 'USERNAME', 'username'), 'username')]/following::input[1]",
                "//label[contains(translate(., 'USERNAME', 'username'), 'username')]/..//input"
            };

            string[] passwordXPaths = {
                "//input[contains(translate(@id, 'PASSWORD', 'password'), 'password')]",
                "//input[contains(translate(@name, 'PASSWORD', 'password'), 'password')]",
                "//input[contains(translate(@placeholder, 'PASSWORD', 'password'), 'password')]",
                "//label[contains(translate(., 'PASSWORD', 'password'), 'password')]/following::input[1]",
                "//label[contains(translate(., 'PASSWORD', 'password'), 'password')]/..//input"
            };

            string[] domainXPaths = {
                "//input[contains(translate(@id, 'DOMAIN', 'domain'), 'domain')]",
                "//input[contains(translate(@name, 'DOMAIN', 'domain'), 'domain')]",
                "//select[contains(translate(@id, 'DOMAIN', 'domain'), 'domain')]",
                "//select[contains(translate(@name, 'DOMAIN', 'domain'), 'domain')]",
                "//label[contains(translate(., 'DOMAIN', 'domain'), 'domain')]/following::input[1]",
                "//label[contains(translate(., 'DOMAIN', 'domain'), 'domain')]/..//input"
            };

            string[] submitXPaths = {
                "//input[@type='submit']",
                "//button[@type='submit']",
                "//button[contains(translate(@id, 'LOGIN', 'login'), 'login')]",
                "//button[contains(translate(., 'LOGIN', 'login'), 'login')]",
                "//button[contains(translate(., 'SIGN IN', 'sign in'), 'sign in')]",
                "//input[contains(translate(@value, 'LOGIN', 'login'), 'login')]",
                "//a[contains(translate(., 'LOGIN', 'login'), 'login')]"
            };

            // Try to find username field
            foreach (var xpath in usernameXPaths)
            {
                try
                {
                    var elements = driver.FindElements(By.XPath(xpath));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.UsernameField = elements[0];
                        break;
                    }
                }
                catch { /* Continue to next XPath */ }
            }

            // Try to find password field
            foreach (var xpath in passwordXPaths)
            {
                try
                {
                    var elements = driver.FindElements(By.XPath(xpath));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.PasswordField = elements[0];
                        break;
                    }
                }
                catch { /* Continue to next XPath */ }
            }

            // Try to find domain field (optional)
            foreach (var xpath in domainXPaths)
            {
                try
                {
                    var elements = driver.FindElements(By.XPath(xpath));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.DomainField = elements[0];
                        break;
                    }
                }
                catch { /* Continue to next XPath */ }
            }

            // Try to find submit button
            foreach (var xpath in submitXPaths)
            {
                try
                {
                    var elements = driver.FindElements(By.XPath(xpath));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.SubmitButton = elements[0];
                        break;
                    }
                }
                catch { /* Continue to next XPath */ }
            }

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in XPath detection strategy");
            return new LoginFormElements();
        }
    }

    private bool IsValidLoginForm(LoginFormElements loginForm)
    {
        // A valid login form must have at least a username field and password field
        return loginForm != null && 
               loginForm.UsernameField != null && 
               loginForm.PasswordField != null;
    }
}
