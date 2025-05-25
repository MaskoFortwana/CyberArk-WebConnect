using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Models;
using System.Text.RegularExpressions;
using System.Linq;

namespace ChromeConnect.Core;

/// <summary>
/// Defines the typing strategy for credential entry
/// </summary>
public enum TypingMode
{
    /// <summary>
    /// Fast direct entry - fastest performance, less human-like
    /// </summary>
    Direct,
    
    /// <summary>
    /// Optimized human-like - balanced performance and detection avoidance
    /// </summary>
    OptimizedHuman,
    
    /// <summary>
    /// Full human simulation - slowest but most human-like
    /// </summary>
    FullHuman
}

/// <summary>
/// Configuration for credential entry performance
/// </summary>
public class CredentialEntryConfig
{
    public TypingMode TypingMode { get; set; } = TypingMode.OptimizedHuman;
    public int MinDelayMs { get; set; } = 10;
    public int MaxDelayMs { get; set; } = 30;
    public int PostEntryDelayMs { get; set; } = 50;
    public int SubmissionDelayMs { get; set; } = 500;
    public bool UseJavaScriptFallback { get; set; } = true;
    public bool LogTimingMetrics { get; set; } = true;
}

public class CredentialManager
{
    private readonly ILogger<CredentialManager> _logger;
    private readonly Random _random = new Random();
    private readonly CredentialEntryConfig _config;

    public CredentialManager(ILogger<CredentialManager> logger, CredentialEntryConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new CredentialEntryConfig();
    }

    public virtual async Task<bool> EnterCredentialsAsync(
        IWebDriver driver, 
        LoginFormElements loginForm, 
        string username,
        string password, 
        string domain)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Make sure we have the required fields
            if (loginForm.UsernameField == null || loginForm.PasswordField == null)
            {
                _logger.LogError("Missing required login form fields");
                return false;
            }

            var usernameTime = await MeasureEntryTime(async () =>
            {
                _logger.LogInformation("Entering username");
                await EnterUsernameAsync(loginForm.UsernameField, username);
            });

            var passwordTime = await MeasureEntryTime(async () =>
            {
                _logger.LogInformation("Entering password");
                await EnterTextOptimizedAsync(loginForm.PasswordField, password);
            });

            var domainTime = TimeSpan.Zero;
            // Enter domain ONLY if a dedicated domain field exists AND domain value is provided
            if (loginForm.DomainField != null && !string.IsNullOrEmpty(domain))
            {
                // Additional validation to ensure the domain field is different from username/password fields
                bool isDomainFieldValid = ValidateDomainField(loginForm);
                
                if (isDomainFieldValid)
                {
                    domainTime = await MeasureEntryTime(async () =>
                    {
                        _logger.LogInformation("Entering domain into dedicated domain field");
                        
                        // Check if it's a dropdown (select) or a text field
                        if (loginForm.DomainField.TagName.ToLower() == "select")
                        {
                            try
                            {
                                var selectElement = new SelectElement(loginForm.DomainField);
                                selectElement.SelectByText(domain);
                                _logger.LogDebug("Successfully selected domain from dropdown");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Couldn't select domain value from dropdown, trying direct text entry");
                                await EnterTextOptimizedAsync(loginForm.DomainField, domain);
                            }
                        }
                        else
                        {
                            await EnterTextOptimizedAsync(loginForm.DomainField, domain);
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Domain field validation failed - domain field appears to be the same as username or password field. Skipping domain entry.");
                }
            }
            else if (loginForm.DomainField == null && !string.IsNullOrEmpty(domain))
            {
                _logger.LogInformation("No dedicated domain field detected on the page. Domain value will not be entered.");
                _logger.LogDebug("This is normal for login pages that don't require domain authentication");
            }
            else if (string.IsNullOrEmpty(domain))
            {
                _logger.LogDebug("No domain value provided, skipping domain field entry");
            }

            // Wait a brief moment before submission
            await Task.Delay(_config.SubmissionDelayMs);

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
                    if (_config.UseJavaScriptFallback)
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        js.ExecuteScript("arguments[0].click();", loginForm.SubmitButton);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                _logger.LogInformation("No submit button found, submitting form via password field");
                loginForm.PasswordField.SendKeys(Keys.Return);
            }

            var totalTime = stopwatch.Elapsed;
            
            if (_config.LogTimingMetrics)
            {
                _logger.LogInformation("Credential entry completed in {TotalTime}ms - Username: {UsernameTime}ms, Password: {PasswordTime}ms, Domain: {DomainTime}ms", 
                    totalTime.TotalMilliseconds, usernameTime.TotalMilliseconds, passwordTime.TotalMilliseconds, domainTime.TotalMilliseconds);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error entering credentials");
            return false;
        }
    }

    private async Task<TimeSpan> MeasureEntryTime(Func<Task> action)
    {
        if (!_config.LogTimingMetrics)
        {
            await action();
            return TimeSpan.Zero;
        }
        
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private async Task EnterTextOptimizedAsync(IWebElement element, string text)
    {
        try
        {
            // Clear the field first (if it has content)
            element.Clear();
            
            // Focus on the element
            element.Click();
            
            switch (_config.TypingMode)
            {
                case TypingMode.Direct:
                    // Fastest method - direct text entry
                    element.SendKeys(text);
                    break;
                    
                case TypingMode.OptimizedHuman:
                    // Balanced approach - type in small chunks with minimal delays
                    await EnterTextInChunksAsync(element, text);
                    break;
                    
                case TypingMode.FullHuman:
                    // Character-by-character with human-like delays
                    await EnterTextCharacterByCharacterAsync(element, text);
                    break;
            }
            
            // Minimal pause after typing
            if (_config.PostEntryDelayMs > 0)
            {
                await Task.Delay(_config.PostEntryDelayMs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in optimized text entry, falling back to direct send keys");
            element.Clear();
            element.SendKeys(text);
        }
    }

    private async Task EnterTextInChunksAsync(IWebElement element, string text)
    {
        // Type in chunks of 2-4 characters for better performance while maintaining some human-likeness
        int chunkSize = Math.Min(5, Math.Max(1, (int)Math.Ceiling(text.Length / 2.0)));
        
        if (chunkSize == 0 && text.Length > 0) chunkSize = 1;
        else if (text.Length == 0) return;

        for (int i = 0; i < text.Length; i += chunkSize)
        {
            int remaining = text.Length - i;
            int currentChunkSize = Math.Min(chunkSize, remaining);
            string chunk = text.Substring(i, currentChunkSize);
            
            element.SendKeys(chunk);
            
            // Very short delay between chunks
            if (i + currentChunkSize < text.Length && _config.MaxDelayMs > 0)
            {
                int delay = _random.Next(_config.MinDelayMs, _config.MaxDelayMs);
                await Task.Delay(delay);
            }
        }
    }

    private async Task EnterTextCharacterByCharacterAsync(IWebElement element, string text)
    {
        // Original character-by-character method with configurable delays
        foreach (char c in text)
        {
            element.SendKeys(c.ToString());
            
            // Random delay between characters (much reduced from original 50-150ms)
            if (_config.MaxDelayMs > 0)
            {
                int delay = _random.Next(_config.MinDelayMs, _config.MaxDelayMs);
                await Task.Delay(delay);
            }
        }
    }

    /// <summary>
    /// Validates that the domain field is actually a separate field from username and password
    /// </summary>
    private bool ValidateDomainField(LoginFormElements loginForm)
    {
        try
        {
            if (loginForm.DomainField == null) return false;
            
            // Check if domain field is the same element as username field
            if (loginForm.UsernameField != null && AreSameElement(loginForm.DomainField, loginForm.UsernameField))
            {
                _logger.LogWarning("Domain field detected as same element as username field - this is incorrect detection");
                return false;
            }
            
            // Check if domain field is the same element as password field
            if (loginForm.PasswordField != null && AreSameElement(loginForm.DomainField, loginForm.PasswordField))
            {
                _logger.LogWarning("Domain field detected as same element as password field - this is incorrect detection");
                return false;
            }
            
            // Additional validation: Check field attributes for domain-specific indicators
            var id = loginForm.DomainField.GetAttribute("id")?.ToLower() ?? "";
            var name = loginForm.DomainField.GetAttribute("name")?.ToLower() ?? "";
            var placeholder = loginForm.DomainField.GetAttribute("placeholder")?.ToLower() ?? "";
            
            // Ensure it has domain-specific attributes and doesn't have username/password attributes
            bool hasDomainAttributes = Regex.IsMatch($"{id} {name} {placeholder}", @"\b(domain|tenant|organization|org|company|realm|authority)\b");
            bool hasUserPassAttributes = Regex.IsMatch($"{id} {name} {placeholder}", @"\b(username|user|password|pass|email|login)\b");
            
            if (!hasDomainAttributes || hasUserPassAttributes)
            {
                _logger.LogWarning("Domain field validation failed - field does not have domain-specific attributes or has username/password attributes");
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating domain field");
            return false;
        }
    }

    /// <summary>
    /// Checks if two WebElements are the same element
    /// </summary>
    private bool AreSameElement(IWebElement element1, IWebElement element2)
    {
        try
        {
            if (element1 == null || element2 == null) return false;
            
            // Compare element references
            if (ReferenceEquals(element1, element2)) return true;
            
            // Compare element properties
            var id1 = element1.GetAttribute("id");
            var id2 = element2.GetAttribute("id");
            var name1 = element1.GetAttribute("name");
            var name2 = element2.GetAttribute("name");
            var tag1 = element1.TagName;
            var tag2 = element2.TagName;
            
            // If both have IDs and they're the same, likely same element
            if (!string.IsNullOrEmpty(id1) && !string.IsNullOrEmpty(id2) && id1 == id2 && tag1 == tag2)
            {
                return true;
            }
            
            // If both have names and they're the same, likely same element
            if (!string.IsNullOrEmpty(name1) && !string.IsNullOrEmpty(name2) && name1 == name2 && tag1 == tag2)
            {
                return true;
            }
            
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Enters username into either an input field or a dropdown (select) element
    /// </summary>
    private async Task EnterUsernameAsync(IWebElement usernameField, string username)
    {
        try
        {
            var tagName = usernameField.TagName.ToLower();
            
            if (tagName == "select")
            {
                _logger.LogInformation("Username field is a dropdown, using dropdown interaction");
                await HandleUsernameDropdownAsync(usernameField, username);
            }
            else
            {
                _logger.LogInformation("Username field is an input, using standard text entry");
                await EnterTextOptimizedAsync(usernameField, username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error entering username");
            throw;
        }
    }

    /// <summary>
    /// Handles username entry for dropdown (select) elements with multiple selection strategies
    /// </summary>
    private async Task HandleUsernameDropdownAsync(IWebElement selectElement, string username)
    {
        try
        {
            var selectWrapper = new SelectElement(selectElement);
            var options = selectWrapper.Options;
            
            _logger.LogDebug($"Username dropdown has {options.Count} options");
            
            // Strategy 1: Try exact value match
            try
            {
                selectWrapper.SelectByValue(username);
                _logger.LogDebug($"Successfully selected username by exact value: {username}");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Exact value selection failed: {ex.Message}");
            }
            
            // Strategy 2: Try exact text match
            try
            {
                selectWrapper.SelectByText(username);
                _logger.LogDebug($"Successfully selected username by exact text: {username}");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Exact text selection failed: {ex.Message}");
            }
            
            // Strategy 3: Try case-insensitive text match
            try
            {
                var matchingOption = options.FirstOrDefault(opt => 
                    string.Equals(opt.Text, username, StringComparison.OrdinalIgnoreCase));
                
                if (matchingOption != null)
                {
                    matchingOption.Click();
                    _logger.LogDebug($"Successfully selected username by case-insensitive text match: {matchingOption.Text}");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Case-insensitive text selection failed: {ex.Message}");
            }
            
            // Strategy 4: Try partial matching (contains)
            try
            {
                var partialMatch = options.FirstOrDefault(opt => 
                    opt.Text.Contains(username, StringComparison.OrdinalIgnoreCase) ||
                    opt.GetAttribute("value")?.Contains(username, StringComparison.OrdinalIgnoreCase) == true);
                
                if (partialMatch != null)
                {
                    partialMatch.Click();
                    _logger.LogDebug($"Successfully selected username by partial match: {partialMatch.Text}");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Partial matching selection failed: {ex.Message}");
            }
            
            // Strategy 5: Log available options and throw meaningful error
            _logger.LogWarning("Failed to find matching username option in dropdown");
            _logger.LogWarning($"Available options: {string.Join(", ", options.Select(opt => $"'{opt.Text}' (value: '{opt.GetAttribute("value")}')"))}");
            
            throw new InvalidOperationException($"Could not find username '{username}' in dropdown options. Available options: {string.Join(", ", options.Select(opt => opt.Text))}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling username dropdown for value: {username}");
            throw;
        }
    }
}
