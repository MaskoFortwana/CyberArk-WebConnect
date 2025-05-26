using System;
using System.Collections.ObjectModel;
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
            // Check if this is a progressive disclosure form (hidden fields that appear sequentially)
            bool isProgressiveForm = await DetectProgressiveDisclosureAsync(driver, loginForm);
            
            if (isProgressiveForm)
            {
                _logger.LogInformation("Detected progressive disclosure form - using sequential filling approach");
                return await EnterCredentialsSequentiallyAsync(driver, loginForm, username, password, domain);
            }
            
            // Make sure we have the required fields for standard forms
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
                            _logger.LogInformation("Domain field is a dropdown, using enhanced dropdown interaction");
                            await HandleDomainDropdownAsync(loginForm.DomainField, domain);
                        }
                        else
                        {
                            _logger.LogInformation("Domain field is an input, using enhanced text entry with validation");
                            await EnterDomainInputAsync(loginForm.DomainField, domain);
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

    /// <summary>
    /// Enhanced domain entry for input fields with additional validation and error handling
    /// </summary>
    private async Task EnterDomainInputAsync(IWebElement inputElement, string domain)
    {
        try
        {
            _logger.LogDebug($"Entering domain '{domain}' into input field");
            
            // Ensure element is interactable
            if (!inputElement.Enabled)
            {
                _logger.LogWarning("Domain input field is disabled, cannot enter value");
                throw new InvalidOperationException("Domain input field is disabled");
            }
            
            if (!inputElement.Displayed)
            {
                _logger.LogWarning("Domain input field is not visible, cannot enter value");
                throw new InvalidOperationException("Domain input field is not visible");
            }
            
            // Check for any input restrictions (maxlength, pattern, etc.)
            var maxLength = inputElement.GetAttribute("maxlength");
            if (!string.IsNullOrEmpty(maxLength) && int.TryParse(maxLength, out int maxLen))
            {
                if (domain.Length > maxLen)
                {
                    _logger.LogWarning($"Domain value '{domain}' ({domain.Length} chars) exceeds maxlength {maxLen}, truncating");
                    domain = domain.Substring(0, maxLen);
                }
            }
            
            // Use the standard optimized text entry with additional validation
            await EnterTextOptimizedAsync(inputElement, domain);
            
            // Validate that the value was actually entered
            var enteredValue = inputElement.GetAttribute("value");
            if (string.IsNullOrEmpty(enteredValue) || !enteredValue.Equals(domain, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning($"Domain value verification failed. Expected: '{domain}', Actual: '{enteredValue}'. Retrying once.");
                
                // Retry once with a different approach
                inputElement.Clear();
                inputElement.SendKeys(domain);
                
                // Check again
                enteredValue = inputElement.GetAttribute("value");
                if (string.IsNullOrEmpty(enteredValue) || !enteredValue.Equals(domain, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError($"Domain value verification failed after retry. Expected: '{domain}', Actual: '{enteredValue}'");
                    throw new InvalidOperationException($"Failed to enter domain value correctly. Expected: '{domain}', Actual: '{enteredValue}'");
                }
            }
            
            _logger.LogDebug($"Successfully entered domain '{domain}' into input field");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error entering domain '{domain}' into input field");
            throw;
        }
    }

    /// <summary>
    /// Handles domain entry for dropdown (select) elements with multiple selection strategies
    /// Enhanced version specifically designed for domain dropdowns like "masko.local", "picovina"
    /// </summary>
    private async Task HandleDomainDropdownAsync(IWebElement selectElement, string domain)
    {
        try
        {
            var selectWrapper = new SelectElement(selectElement);
            var options = selectWrapper.Options;
            
            _logger.LogDebug($"Domain dropdown has {options.Count} options");
            
            // Fast path: Pre-analyze options to find the best match strategy
            var bestMatch = FindBestDomainMatch(options, domain);
            if (bestMatch != null)
            {
                bestMatch.Click();
                _logger.LogDebug($"Successfully selected domain using optimized matching: {bestMatch.Text} (value: {bestMatch.GetAttribute("value")})");
                return;
            }
            
            // Fallback: Try Selenium's built-in methods for exact matches
            try
            {
                selectWrapper.SelectByValue(domain);
                _logger.LogDebug($"Successfully selected domain by exact value: {domain}");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Exact value selection failed: {ex.Message}");
            }
            
            try
            {
                selectWrapper.SelectByText(domain);
                _logger.LogDebug($"Successfully selected domain by exact text: {domain}");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Exact text selection failed: {ex.Message}");
            }
            
            // Log available options and provide helpful error
            _logger.LogWarning("Failed to find matching domain option in dropdown");
            _logger.LogWarning($"Requested domain: '{domain}'");
            _logger.LogWarning($"Available options: {string.Join(", ", options.Select(opt => $"'{opt.Text}' (value: '{opt.GetAttribute("value")}')"))}");
            
            // Final fallback: try to enter the domain as text (some dropdowns allow typing)
            try
            {
                _logger.LogInformation("Attempting fallback: entering domain as text in dropdown");
                await EnterTextOptimizedAsync(selectElement, domain);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Text entry fallback failed: {ex.Message}");
            }
            
            throw new InvalidOperationException($"Could not find domain '{domain}' in dropdown options. Available options: {string.Join(", ", options.Select(opt => opt.Text))}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling domain dropdown for value: {domain}");
            throw;
        }
    }

    /// <summary>
    /// Optimized domain matching that analyzes all options once and finds the best match
    /// </summary>
            private IWebElement? FindBestDomainMatch(IList<IWebElement> options, string domain)
    {
        if (string.IsNullOrEmpty(domain))
        {
            // If domain is empty, select first non-placeholder option
            return options.FirstOrDefault(opt => 
                !string.IsNullOrEmpty(opt.GetAttribute("value")) && 
                !opt.Text.Contains("Select", StringComparison.OrdinalIgnoreCase) && 
                !opt.Text.Contains("Choose", StringComparison.OrdinalIgnoreCase) &&
                !opt.Text.StartsWith("--"));
        }

        var domainLower = domain.ToLower();
        
        // Score each option and return the best match
        var scoredOptions = options.Select(opt => new
        {
            Element = opt,
            Score = CalculateDomainMatchScore(opt, domain, domainLower)
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .ToList();

        return scoredOptions.FirstOrDefault()?.Element;
    }

    /// <summary>
    /// Calculates a match score for a domain option (higher score = better match)
    /// </summary>
    private int CalculateDomainMatchScore(IWebElement option, string domain, string domainLower)
    {
        try
        {
            var text = option.Text?.ToLower() ?? "";
            var value = option.GetAttribute("value")?.ToLower() ?? "";
            
            // Exact matches get highest score
            if (text == domainLower || value == domainLower) return 100;
            
            // Case-insensitive exact matches
            if (string.Equals(text, domain, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, domain, StringComparison.OrdinalIgnoreCase)) return 90;
            
            // Word boundary matches (domain appears as complete word)
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(domainLower)}\b") ||
                Regex.IsMatch(value, $@"\b{Regex.Escape(domainLower)}\b")) return 80;
            
            // Contains matches
            if (text.Contains(domainLower) || value.Contains(domainLower)) return 70;
            
            // Partial matches (domain contains option or vice versa)
            if (domainLower.Contains(text) && text.Length > 2) return 60;
            if (domainLower.Contains(value) && value.Length > 2) return 60;
            
            return 0; // No match
        }
        catch
        {
            return 0; // Error accessing element properties
        }
    }

    /// <summary>
    /// Detects if the form uses progressive disclosure (fields appear sequentially)
    /// </summary>
    private async Task<bool> DetectProgressiveDisclosureAsync(IWebDriver driver, LoginFormElements loginForm)
    {
        try
        {
            // Progressive disclosure indicators:
            // 1. Password field is null or not visible when username field is visible
            // 2. Submit button is null or not visible when password field is not visible
            // 3. Only username field is initially visible
            
            bool usernameVisible = loginForm.UsernameField != null && IsElementVisible(loginForm.UsernameField);
            bool passwordVisible = loginForm.PasswordField != null && IsElementVisible(loginForm.PasswordField);
            bool submitVisible = loginForm.SubmitButton != null && IsElementVisible(loginForm.SubmitButton);
            
            _logger.LogDebug($"Progressive disclosure detection: Username={usernameVisible}, Password={passwordVisible}, Submit={submitVisible}");
            
            // Classic progressive disclosure pattern: only username is visible initially
            if (usernameVisible && !passwordVisible)
            {
                _logger.LogDebug("Detected progressive disclosure: username visible but password not visible");
                return true;
            }
            
            // Alternative pattern: username and password visible but submit button hidden
            if (usernameVisible && passwordVisible && !submitVisible)
            {
                _logger.LogDebug("Detected progressive disclosure: username and password visible but submit button hidden");
                return true;
            }
            
            // Check for hidden elements in DOM that might become visible
            if (await HasHiddenFormElementsAsync(driver))
            {
                _logger.LogDebug("Detected progressive disclosure: found hidden form elements that may become visible");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting progressive disclosure, assuming standard form");
            return false;
        }
    }

    /// <summary>
    /// Checks for hidden form elements that might become visible after interaction
    /// </summary>
    private async Task<bool> HasHiddenFormElementsAsync(IWebDriver driver)
    {
        try
        {
            // Look for password fields that exist in DOM but are hidden
            var hiddenPasswordFields = driver.FindElements(By.CssSelector("input[type='password']"))
                .Where(e => !IsElementVisible(e))
                .ToList();
                
            // Look for submit buttons that exist in DOM but are hidden
            var hiddenSubmitButtons = driver.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button"))
                .Where(e => !IsElementVisible(e))
                .Where(e => {
                    var text = e.Text?.ToLower() ?? "";
                    var value = e.GetAttribute("value")?.ToLower() ?? "";
                    return text.Contains("login") || text.Contains("submit") || text.Contains("sign") ||
                           value.Contains("login") || value.Contains("submit") || value.Contains("sign");
                })
                .ToList();
            
            _logger.LogDebug($"Found {hiddenPasswordFields.Count} hidden password fields and {hiddenSubmitButtons.Count} hidden submit buttons");
            
            return hiddenPasswordFields.Any() || hiddenSubmitButtons.Any();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking for hidden form elements");
            return false;
        }
    }

    /// <summary>
    /// Handles credential entry for progressive disclosure forms where fields appear sequentially
    /// Enhanced with adaptive workflow and comprehensive error handling
    /// </summary>
    private async Task<bool> EnterCredentialsSequentiallyAsync(
        IWebDriver driver, 
        LoginFormElements loginForm, 
        string username,
        string password, 
        string domain)
    {
        var progressiveConfig = new ProgressiveDisclosureConfig();
        var stepResults = new List<ProgressiveStepResult>();
        
        try
        {
            _logger.LogInformation("Starting enhanced sequential credential entry for progressive disclosure form");
            
            // Step 1: Enter username and wait for password field to appear
            var step1Result = await ExecuteProgressiveStepAsync("EnterUsername", async () =>
            {
                if (loginForm.UsernameField == null)
                {
                    throw new InvalidOperationException("Username field is required for progressive disclosure forms");
                }
                
                _logger.LogInformation("Step 1: Entering username");
                await EnterUsernameAsync(loginForm.UsernameField, username);
                
                // Fast path: Check if password field is already available and visible
                var passwordField = loginForm.PasswordField;
                if (passwordField != null && IsElementVisible(passwordField))
                {
                    _logger.LogDebug("Password field already visible, skipping wait");
                    return passwordField;
                }
                
                // Wait for password field to become visible
                passwordField = await WaitForPasswordFieldAsync(driver, loginForm);
                if (passwordField == null)
                {
                    throw new TimeoutException("Password field did not become visible after entering username");
                }
                
                return passwordField;
            });
            
            stepResults.Add(step1Result);
            if (!step1Result.Success)
            {
                return await HandleProgressiveFailure(stepResults, "Username entry failed");
            }
            
            var passwordField = (IWebElement)step1Result.Result!;
            
            // Step 2: Enter password and wait for submit button to appear
            var step2Result = await ExecuteProgressiveStepAsync("EnterPassword", async () =>
            {
                _logger.LogInformation("Step 2: Entering password");
                await EnterTextOptimizedAsync(passwordField, password);
                
                // Wait for submit button to appear (or check if form is ready for submission)
                var submitButton = await WaitForSubmitButtonAsync(driver, loginForm);
                return submitButton; // Can be null if form submits via Enter key
            });
            
            stepResults.Add(step2Result);
            if (!step2Result.Success)
            {
                return await HandleProgressiveFailure(stepResults, "Password entry failed");
            }
            
            // Step 3: Handle domain if needed (optional step)
            if (!string.IsNullOrEmpty(domain))
            {
                var step3Result = await ExecuteProgressiveStepAsync("EnterDomain", async () =>
                {
                    // Fast path: Check if domain field is already available from initial detection
                    var domainField = loginForm.DomainField;
                    
                    // If not available or not visible, wait for it to appear
                    if (domainField == null || !IsElementVisible(domainField))
                    {
                        domainField = await WaitForDomainFieldAsync(driver, loginForm);
                    }
                    
                    if (domainField != null)
                    {
                        _logger.LogInformation("Step 3: Entering domain");
                        if (domainField.TagName.ToLower() == "select")
                        {
                            await HandleDomainDropdownAsync(domainField, domain);
                        }
                        else
                        {
                            await EnterTextOptimizedAsync(domainField, domain);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No domain field found, skipping domain entry");
                    }
                    return domainField;
                });
                
                stepResults.Add(step3Result);
                // Domain step failure is not critical, continue with submission
                if (!step3Result.Success)
                {
                    _logger.LogWarning("Domain entry failed, continuing with form submission");
                }
            }
            
            // Step 4: Submit the form
            var step4Result = await ExecuteProgressiveStepAsync("SubmitForm", async () =>
            {
                // Try multiple sources for submit button (prioritize already detected ones)
                var submitButton = (IWebElement?)step2Result.Result ?? loginForm.SubmitButton;
                
                // If no submit button found, try to find one quickly
                if (submitButton == null || !IsElementVisible(submitButton))
                {
                    submitButton = await WaitForSubmitButtonAsync(driver, loginForm);
                }
                
                if (submitButton != null && IsElementVisibleAndStable(submitButton))
                {
                    _logger.LogInformation("Step 4: Clicking submit button");
                    await Task.Delay(_config.SubmissionDelayMs);
                    
                    try
                    {
                        submitButton.Click();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error clicking submit button, trying JavaScript click");
                        if (_config.UseJavaScriptFallback)
                        {
                            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                            js.ExecuteScript("arguments[0].click();", submitButton);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Step 4: No submit button available, submitting via password field");
                    passwordField.SendKeys(Keys.Return);
                }
                
                return true;
            });
            
            stepResults.Add(step4Result);
            
            if (step4Result.Success)
            {
                _logger.LogInformation("Enhanced sequential credential entry completed successfully");
                LogProgressiveStepSummary(stepResults);
                return true;
            }
            else
            {
                return await HandleProgressiveFailure(stepResults, "Form submission failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in enhanced sequential credential entry");
            return await HandleProgressiveFailure(stepResults, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a progressive disclosure step with error handling and timing
    /// </summary>
    private async Task<ProgressiveStepResult> ExecuteProgressiveStepAsync<T>(
        string stepName, 
        Func<Task<T>> stepAction)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug($"Executing progressive step: {stepName}");
            var result = await stepAction();
            stopwatch.Stop();
            
            _logger.LogDebug($"Progressive step {stepName} completed in {stopwatch.ElapsedMilliseconds}ms");
            
            return new ProgressiveStepResult
            {
                StepName = stepName,
                Success = true,
                Duration = stopwatch.Elapsed,
                Result = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, $"Progressive step {stepName} failed after {stopwatch.ElapsedMilliseconds}ms");
            
            return new ProgressiveStepResult
            {
                StepName = stepName,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex.Message,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Handles progressive disclosure failures with fallback strategies
    /// </summary>
    private async Task<bool> HandleProgressiveFailure(
        List<ProgressiveStepResult> stepResults, 
        string failureReason)
    {
        _logger.LogWarning($"Progressive disclosure failed: {failureReason}");
        LogProgressiveStepSummary(stepResults);
        
        // Attempt fallback to standard form filling if early steps succeeded
        var successfulSteps = stepResults.Where(s => s.Success).ToList();
        
        if (successfulSteps.Any() && successfulSteps.First().StepName == "EnterUsername")
        {
            _logger.LogInformation("Attempting fallback to standard form submission");
            
            try
            {
                // Try to submit via Enter key on the last successful field
                var lastSuccessfulStep = successfulSteps.Last();
                if (lastSuccessfulStep.Result is IWebElement element && IsElementVisibleAndStable(element))
                {
                    element.SendKeys(Keys.Return);
                    _logger.LogInformation("Fallback submission attempted via Enter key");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallback submission also failed");
            }
        }
        
        return false;
    }

    /// <summary>
    /// Logs a summary of progressive disclosure step results
    /// </summary>
    private void LogProgressiveStepSummary(List<ProgressiveStepResult> stepResults)
    {
        _logger.LogInformation("Progressive Disclosure Step Summary:");
        
        foreach (var step in stepResults)
        {
            var status = step.Success ? "✓" : "✗";
            var duration = step.Duration.TotalMilliseconds;
            
            if (step.Success)
            {
                _logger.LogInformation($"  {status} {step.StepName}: {duration:F0}ms");
            }
            else
            {
                _logger.LogWarning($"  {status} {step.StepName}: {duration:F0}ms - {step.Error}");
            }
        }
        
        var totalDuration = stepResults.Sum(s => s.Duration.TotalMilliseconds);
        var successCount = stepResults.Count(s => s.Success);
        
        _logger.LogInformation($"Total: {successCount}/{stepResults.Count} steps successful in {totalDuration:F0}ms");
    }

    /// <summary>
    /// Waits for password field to become visible after username entry with enhanced DOM monitoring
    /// </summary>
    private async Task<IWebElement?> WaitForPasswordFieldAsync(IWebDriver driver, LoginFormElements loginForm)
    {
        try
        {
            // If password field is already available and visible, return it
            if (loginForm.PasswordField != null && IsElementVisibleAndStable(loginForm.PasswordField))
            {
                _logger.LogDebug("Password field already visible");
                return loginForm.PasswordField;
            }
            
            _logger.LogDebug("Waiting for password field to become visible with DOM monitoring...");
            
            // Wait up to 10 seconds for password field to appear
            var timeout = TimeSpan.FromSeconds(10);
            var endTime = DateTime.Now.Add(timeout);
            var lastDomState = GetDomStateHash(driver);
            var stableCount = 0;
            
            while (DateTime.Now < endTime)
            {
                try
                {
                    // Monitor DOM changes
                    var currentDomState = GetDomStateHash(driver);
                    if (currentDomState != lastDomState)
                    {
                        _logger.LogDebug("DOM change detected, re-scanning for password field");
                        lastDomState = currentDomState;
                        stableCount = 0;
                    }
                    else
                    {
                        stableCount++;
                    }
                    
                    // Look for password fields that have become visible
                    var passwordFields = driver.FindElements(By.CssSelector("input[type='password']"))
                        .Where(IsElementVisibleAndStable)
                        .ToList();
                    
                    if (passwordFields.Any())
                    {
                        var passwordField = passwordFields.First();
                        _logger.LogDebug("Password field became visible");
                        
                        // Wait for element to be stable before returning
                        if (await WaitForElementStability(passwordField, 500))
                        {
                            return passwordField;
                        }
                    }
                    
                    // Also check if the original password field became visible (handle staleness)
                    if (loginForm.PasswordField != null)
                    {
                        try
                        {
                            if (IsElementVisibleAndStable(loginForm.PasswordField))
                            {
                                _logger.LogDebug("Original password field became visible");
                                if (await WaitForElementStability(loginForm.PasswordField, 500))
                                {
                                    return loginForm.PasswordField;
                                }
                            }
                        }
                        catch (StaleElementReferenceException)
                        {
                            _logger.LogDebug("Original password field became stale, continuing with fresh search");
                            loginForm.PasswordField = null; // Clear stale reference
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error checking for password field visibility");
                }
                
                await Task.Delay(200); // Poll every 200ms
            }
            
            _logger.LogWarning("Password field did not become visible within timeout");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for password field");
            return null;
        }
    }

    /// <summary>
    /// Waits for domain field to become visible (if needed)
    /// </summary>
    private async Task<IWebElement?> WaitForDomainFieldAsync(IWebDriver driver, LoginFormElements loginForm)
    {
        try
        {
            // If domain field is already available, return it immediately (visible or hidden for progressive disclosure)
            if (loginForm.DomainField != null)
            {
                var isVisible = IsElementVisible(loginForm.DomainField);
                _logger.LogDebug($"Domain field already detected (visible: {isVisible})");
                return loginForm.DomainField;
            }
            
            _logger.LogDebug("Waiting for domain field to become visible...");
            
            // Reduced timeout to 2 seconds since domain fields are usually detected immediately
            var timeout = TimeSpan.FromSeconds(2);
            var endTime = DateTime.Now.Add(timeout);
            var pollCount = 0;
            
            while (DateTime.Now < endTime)
            {
                try
                {
                    pollCount++;
                    
                    // Use optimized selector for better performance
                    var domainFields = driver.FindElements(By.CssSelector("select[name*='domain'], select[id*='domain'], select[name*='realm'], select[id*='realm']"))
                        .Where(IsElementVisible)
                        .ToList();
                    
                    if (domainFields.Any())
                    {
                        var domainField = domainFields.First();
                        _logger.LogDebug($"Domain field became visible after {pollCount} polls");
                        return domainField;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error checking for domain field visibility");
                }
                
                await Task.Delay(100); // Reduced polling interval to 100ms for faster response
            }
            
            _logger.LogDebug($"Domain field did not become visible within {timeout.TotalSeconds}s timeout (this is normal for many forms)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for domain field");
            return null;
        }
    }

    /// <summary>
    /// Waits for submit button to become visible after password entry with enhanced DOM monitoring
    /// </summary>
    private async Task<IWebElement?> WaitForSubmitButtonAsync(IWebDriver driver, LoginFormElements loginForm)
    {
        try
        {
            // If submit button is already available and visible, return it
            if (loginForm.SubmitButton != null && IsElementVisibleAndStable(loginForm.SubmitButton))
            {
                _logger.LogDebug("Submit button already visible");
                return loginForm.SubmitButton;
            }
            
            _logger.LogDebug("Waiting for submit button to become visible with DOM monitoring...");
            
            // Wait up to 10 seconds for submit button to appear
            var timeout = TimeSpan.FromSeconds(10);
            var endTime = DateTime.Now.Add(timeout);
            var lastDomState = GetDomStateHash(driver);
            
            while (DateTime.Now < endTime)
            {
                try
                {
                    // Monitor DOM changes
                    var currentDomState = GetDomStateHash(driver);
                    if (currentDomState != lastDomState)
                    {
                        _logger.LogDebug("DOM change detected, re-scanning for submit button");
                        lastDomState = currentDomState;
                    }
                    
                    // Look for submit buttons that have become visible
                    var submitButtons = driver.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button"))
                        .Where(IsElementVisibleAndStable)
                        .Where(e => {
                            try
                            {
                                var text = e.Text?.ToLower() ?? "";
                                var value = e.GetAttribute("value")?.ToLower() ?? "";
                                return text.Contains("login") || text.Contains("submit") || text.Contains("sign") ||
                                       value.Contains("login") || value.Contains("submit") || value.Contains("sign") ||
                                       string.IsNullOrEmpty(text); // Generic buttons
                            }
                            catch (StaleElementReferenceException)
                            {
                                return false;
                            }
                        })
                        .ToList();
                    
                    if (submitButtons.Any())
                    {
                        var submitButton = submitButtons.First();
                        _logger.LogDebug("Submit button became visible");
                        
                        // Wait for element to be stable before returning
                        if (await WaitForElementStability(submitButton, 500))
                        {
                            return submitButton;
                        }
                    }
                    
                    // Also check if the original submit button became visible (handle staleness)
                    if (loginForm.SubmitButton != null)
                    {
                        try
                        {
                            if (IsElementVisibleAndStable(loginForm.SubmitButton))
                            {
                                _logger.LogDebug("Original submit button became visible");
                                if (await WaitForElementStability(loginForm.SubmitButton, 500))
                                {
                                    return loginForm.SubmitButton;
                                }
                            }
                        }
                        catch (StaleElementReferenceException)
                        {
                            _logger.LogDebug("Original submit button became stale, continuing with fresh search");
                            loginForm.SubmitButton = null; // Clear stale reference
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error checking for submit button visibility");
                }
                
                await Task.Delay(100); // Reduced polling interval to 100ms for faster response
            }
            
            _logger.LogWarning("Submit button did not become visible within timeout");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for submit button");
            return null;
        }
    }

    /// <summary>
    /// Helper method to check if an element is visible and enabled
    /// </summary>
    private bool IsElementVisible(IWebElement element)
    {
        try
        {
            return element.Displayed && element.Enabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enhanced visibility check that also verifies element stability (not stale)
    /// </summary>
    private bool IsElementVisibleAndStable(IWebElement element)
    {
        try
        {
            // Check if element is stale by accessing a property
            var tagName = element.TagName;
            return element.Displayed && element.Enabled;
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a hash representing the current DOM state for change detection
    /// </summary>
    private string GetDomStateHash(IWebDriver driver)
    {
        try
        {
            var js = (IJavaScriptExecutor)driver;
            
            // Get a simplified DOM state representation
            var domInfo = js.ExecuteScript(@"
                return {
                    inputCount: document.querySelectorAll('input').length,
                    passwordCount: document.querySelectorAll('input[type=""password""]').length,
                    buttonCount: document.querySelectorAll('button').length,
                    visibleInputs: Array.from(document.querySelectorAll('input')).filter(el => 
                        el.offsetParent !== null && getComputedStyle(el).visibility !== 'hidden'
                    ).length,
                    bodyHash: document.body ? document.body.innerHTML.length : 0
                };
            ");
            
            return domInfo?.ToString() ?? DateTime.Now.Ticks.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting DOM state hash");
            return DateTime.Now.Ticks.ToString();
        }
    }

    /// <summary>
    /// Waits for an element to become stable (not changing) for a specified duration
    /// </summary>
    private async Task<bool> WaitForElementStability(IWebElement element, int stabilityMs = 500)
    {
        try
        {
            var startTime = DateTime.Now;
            var lastState = GetElementState(element);
            
            while (DateTime.Now.Subtract(startTime).TotalMilliseconds < stabilityMs)
            {
                await Task.Delay(100);
                
                var currentState = GetElementState(element);
                if (currentState != lastState)
                {
                    // Element changed, reset stability timer
                    startTime = DateTime.Now;
                    lastState = currentState;
                }
            }
            
            return true;
        }
        catch (StaleElementReferenceException)
        {
            _logger.LogDebug("Element became stale while waiting for stability");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error waiting for element stability");
            return false;
        }
    }

    /// <summary>
    /// Gets a state representation of an element for stability checking
    /// </summary>
    private string GetElementState(IWebElement element)
    {
        try
        {
            return $"{element.Displayed}|{element.Enabled}|{element.Location}|{element.Size}";
        }
        catch (StaleElementReferenceException)
        {
            return "stale";
        }
        catch
        {
            return "error";
        }
    }

    /// <summary>
    /// Configuration for progressive disclosure form handling
    /// </summary>
    internal class ProgressiveDisclosureConfig
    {
        public int MaxStepTimeoutMs { get; set; } = 5000; // Reduced from 10s to 5s
        public int StepPollingIntervalMs { get; set; } = 100; // Reduced from 200ms to 100ms
        public int ElementStabilityMs { get; set; } = 250; // Reduced from 500ms to 250ms
        public bool EnableFallbackStrategies { get; set; } = true;
        public bool LogDetailedStepTiming { get; set; } = true;
        public bool EnableEarlyExit { get; set; } = true; // New: Allow early exit when conditions are met
        public bool EnableParallelChecks { get; set; } = true; // New: Enable parallel element checks
    }

    /// <summary>
    /// Result of a progressive disclosure step execution
    /// </summary>
    internal class ProgressiveStepResult
    {
        public string StepName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public object? Result { get; set; }
        public string? Error { get; set; }
        public Exception? Exception { get; set; }
    }
}
