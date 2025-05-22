using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace ChromeConnect.Core;

public class CredentialManager
{
    private readonly ILogger<CredentialManager> _logger;
    private readonly Random _random = new Random();

    public CredentialManager(ILogger<CredentialManager> logger)
    {
        _logger = logger;
    }

    public async Task<bool> EnterCredentialsAsync(
        IWebDriver driver, 
        LoginFormElements loginForm, 
        string username,
        string password, 
        string domain)
    {
        try
        {
            // Make sure we have the required fields
            if (loginForm.UsernameField == null || loginForm.PasswordField == null)
            {
                _logger.LogError("Missing required login form fields");
                return false;
            }

            _logger.LogInformation("Entering username");
            await EnterTextHumanLikeAsync(loginForm.UsernameField, username);

            _logger.LogInformation("Entering password");
            await EnterTextHumanLikeAsync(loginForm.PasswordField, password);

            // Enter domain if the field exists
            if (loginForm.DomainField != null && !string.IsNullOrEmpty(domain))
            {
                _logger.LogInformation("Entering domain");
                
                // Check if it's a dropdown (select) or a text field
                if (loginForm.DomainField.TagName.ToLower() == "select")
                {
                    try
                    {
                        var selectElement = new SelectElement(loginForm.DomainField);
                        selectElement.SelectByText(domain);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Couldn't select domain value from dropdown, trying direct text entry");
                        await EnterTextHumanLikeAsync(loginForm.DomainField, domain);
                    }
                }
                else
                {
                    await EnterTextHumanLikeAsync(loginForm.DomainField, domain);
                }
            }

            // Click submit button if it exists, otherwise submit the form via the password field
            if (loginForm.SubmitButton != null)
            {
                _logger.LogInformation("Clicking submit button");
                try
                {
                    loginForm.SubmitButton.Click();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error clicking submit button, trying JavaScript click");
                    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                    js.ExecuteScript("arguments[0].click();", loginForm.SubmitButton);
                }
            }
            else
            {
                _logger.LogInformation("No submit button found, submitting via password field");
                loginForm.PasswordField.SendKeys(Keys.Return);
            }

            // Wait a moment for submission to complete
            await Task.Delay(2000);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error entering credentials");
            return false;
        }
    }

    private async Task EnterTextHumanLikeAsync(IWebElement element, string text)
    {
        try
        {
            // Clear the field first (if it has content)
            element.Clear();
            
            // Focus on the element
            element.Click();
            
            // Type each character with a small random delay
            foreach (char c in text)
            {
                element.SendKeys(c.ToString());
                
                // Random delay between 50-150ms to simulate human typing
                int delay = _random.Next(50, 150);
                await Task.Delay(delay);
            }
            
            // Slight pause after typing
            await Task.Delay(200);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in human-like text entry, falling back to direct send keys");
            element.Clear();
            element.SendKeys(text);
        }
    }
}
