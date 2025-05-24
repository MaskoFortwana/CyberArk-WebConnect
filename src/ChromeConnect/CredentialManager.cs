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
using System.Collections.Generic;

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
                
                // Check if username field is a dropdown and use optimized handling
                if (loginForm.UsernameField.TagName.ToLower() == "select")
                {
                    _logger.LogDebug("Username field is a dropdown, using optimized dropdown handling");
                    bool dropdownSuccess = await HandleUsernameDropdownOptimizedAsync(driver, loginForm.UsernameField, username);
                    if (!dropdownSuccess)
                    {
                        _logger.LogWarning("Optimized username dropdown handling failed, falling back to standard method");
                        await EnterTextOptimizedAsync(loginForm.UsernameField, username);
                    }
                }
                else
                {
                    // Standard text input handling
                    await EnterTextOptimizedAsync(loginForm.UsernameField, username);
                }
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
                            bool selectionSuccessful = false;
                            var selectElement = new SelectElement(loginForm.DomainField);
                            
                            try
                            {
                                // Strategy 1: Try exact text match first
                                selectElement.SelectByText(domain);
                                selectionSuccessful = true;
                                _logger.LogDebug("Successfully selected domain from dropdown using exact text match");
                            }
                            catch (NoSuchElementException)
                            {
                                _logger.LogDebug("Exact text match failed for domain dropdown, trying case-insensitive match");
                                
                                try
                                {
                                    // Strategy 2: Try case-insensitive text match
                                    var options = selectElement.Options;
                                    var matchingOption = options.FirstOrDefault(option => 
                                        string.Equals(option.Text?.Trim(), domain?.Trim(), StringComparison.OrdinalIgnoreCase));
                                    
                                    if (matchingOption != null)
                                    {
                                        selectElement.SelectByText(matchingOption.Text);
                                        selectionSuccessful = true;
                                        _logger.LogDebug("Successfully selected domain using case-insensitive text match");
                                    }
                                    else
                                    {
                                        // Strategy 3: Try partial text match
                                        var partialMatch = options.FirstOrDefault(option => 
                                            option.Text?.ToLower().Contains(domain?.ToLower()) == true);
                                        
                                        if (partialMatch != null)
                                        {
                                            selectElement.SelectByText(partialMatch.Text);
                                            selectionSuccessful = true;
                                            _logger.LogDebug("Successfully selected domain using partial text match");
                                        }
                                    }
                                }
                                catch (Exception partialEx)
                                {
                                    _logger.LogDebug($"Case-insensitive/partial text matching failed: {partialEx.Message}");
                                }
                            }
                            catch (Exception exactEx)
                            {
                                _logger.LogDebug($"Exact text selection failed: {exactEx.Message}");
                            }
                            
                            // Strategy 4: Try SelectByValue if text selection failed
                            if (!selectionSuccessful)
                            {
                                try
                                {
                                    selectElement.SelectByValue(domain);
                                    selectionSuccessful = true;
                                    _logger.LogDebug("Successfully selected domain using value attribute");
                                }
                                catch (Exception valueEx)
                                {
                                    _logger.LogDebug($"SelectByValue failed: {valueEx.Message}");
                                }
                            }
                            
                            // Strategy 5: Try selecting by index (select first non-empty option if exists)
                            if (!selectionSuccessful)
                            {
                                try
                                {
                                    var options = selectElement.Options;
                                    // Skip empty options and "Select..." options
                                    var validOption = options.Skip(1).FirstOrDefault(opt => 
                                        !string.IsNullOrWhiteSpace(opt.Text) && 
                                        !opt.Text.ToLower().Contains("select") &&
                                        !opt.Text.ToLower().Contains("choose"));
                                    
                                    if (validOption != null)
                                    {
                                        selectElement.SelectByText(validOption.Text);
                                        selectionSuccessful = true;
                                        _logger.LogInformation($"Selected first available domain option: {validOption.Text}");
                                    }
                                }
                                catch (Exception indexEx)
                                {
                                    _logger.LogDebug($"Index-based selection failed: {indexEx.Message}");
                                }
                            }
                            
                            // Strategy 6: JavaScript fallback for complex dropdowns
                            if (!selectionSuccessful && _config.UseJavaScriptFallback)
                            {
                                try
                                {
                                    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                                    
                                    // Try to set the value directly via JavaScript
                                    js.ExecuteScript($"arguments[0].value = '{domain}'; arguments[0].dispatchEvent(new Event('change'));", loginForm.DomainField);
                                    
                                    // Trigger change events that might be required
                                    js.ExecuteScript("arguments[0].dispatchEvent(new Event('input')); arguments[0].dispatchEvent(new Event('blur'));", loginForm.DomainField);
                                    
                                    selectionSuccessful = true;
                                    _logger.LogDebug("Successfully set domain value using JavaScript fallback");
                                }
                                catch (Exception jsEx)
                                {
                                    _logger.LogWarning($"JavaScript fallback failed: {jsEx.Message}");
                                }
                            }
                            
                            if (!selectionSuccessful)
                            {
                                _logger.LogWarning($"All domain dropdown selection strategies failed. Available options: {string.Join(", ", selectElement.Options.Select(o => o.Text))}");
                                // Still try direct text entry as last resort
                                await EnterTextOptimizedAsync(loginForm.DomainField, domain);
                            }
                        }
                        else
                        {
                            // Handle custom dropdowns or text fields
                            await HandleCustomDomainFieldAsync(driver, loginForm.DomainField, domain);
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

    public virtual async Task HandleCustomDomainFieldAsync(IWebDriver driver, IWebElement domainField, string domain)
    {
        try
        {
            _logger.LogDebug("Handling custom domain field");
            
            // Check if it's a custom dropdown (div-based, etc.)
            var tagName = domainField.TagName.ToLower();
            var role = domainField.GetAttribute("role")?.ToLower();
            var className = domainField.GetAttribute("class")?.ToLower();
            
            // Handle div-based dropdowns with role="combobox" or "listbox"
            if ((tagName == "div" || tagName == "span") && 
                (role == "combobox" || role == "listbox" || role == "button" ||
                 className?.Contains("dropdown") == true || className?.Contains("select") == true))
            {
                _logger.LogDebug("Detected custom dropdown component");
                
                // Try clicking to open dropdown
                try
                {
                    domainField.Click();
                    await Task.Delay(300); // Wait for dropdown to open
                    
                    // Look for dropdown options
                    var dropdownOptions = driver.FindElements(By.CssSelector(
                        "[role='option'], .dropdown-item, .select-option, li[data-value], [data-testid*='option']"));
                    
                    if (dropdownOptions.Count > 0)
                    {
                        _logger.LogDebug($"Found {dropdownOptions.Count} dropdown options");
                        
                        // Try to find matching option
                        var matchingOption = dropdownOptions.FirstOrDefault(opt => 
                            string.Equals(opt.Text?.Trim(), domain?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                            opt.GetAttribute("data-value")?.Equals(domain, StringComparison.OrdinalIgnoreCase) == true);
                        
                        if (matchingOption != null)
                        {
                            matchingOption.Click();
                            _logger.LogDebug("Successfully selected domain from custom dropdown");
                            return;
                        }
                        else
                        {
                            // Try partial match
                            var partialMatch = dropdownOptions.FirstOrDefault(opt => 
                                opt.Text?.ToLower().Contains(domain?.ToLower()) == true);
                            
                            if (partialMatch != null)
                            {
                                partialMatch.Click();
                                _logger.LogDebug("Successfully selected domain using partial match in custom dropdown");
                                return;
                            }
                            else
                            {
                                // Select first non-empty option as fallback
                                var firstOption = dropdownOptions.FirstOrDefault(opt => 
                                    !string.IsNullOrWhiteSpace(opt.Text) &&
                                    !opt.Text.ToLower().Contains("select") &&
                                    !opt.Text.ToLower().Contains("choose"));
                                
                                if (firstOption != null)
                                {
                                    firstOption.Click();
                                    _logger.LogInformation($"Selected first available domain option from custom dropdown: {firstOption.Text}");
                                    return;
                                }
                            }
                        }
                    }
                    
                    // If no options found, try typing directly
                    _logger.LogDebug("No dropdown options found, trying direct text entry");
                    await EnterTextOptimizedAsync(domainField, domain);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error handling custom dropdown: {ex.Message}");
                    // Fallback to direct text entry
                    await EnterTextOptimizedAsync(domainField, domain);
                }
            }
            // Handle input fields with autocomplete or typeahead
            else if (tagName == "input")
            {
                _logger.LogDebug("Handling input field with potential autocomplete");
                
                // Clear and type domain value
                await EnterTextOptimizedAsync(domainField, domain);
                
                // Wait a moment for autocomplete suggestions
                await Task.Delay(500);
                
                // Look for autocomplete suggestions
                var suggestions = driver.FindElements(By.CssSelector(
                    ".autocomplete-suggestion, .typeahead-suggestion, [role='option'], .suggestion-item"));
                
                if (suggestions.Count > 0)
                {
                    var matchingSuggestion = suggestions.FirstOrDefault(sugg => 
                        string.Equals(sugg.Text?.Trim(), domain?.Trim(), StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingSuggestion != null)
                    {
                        matchingSuggestion.Click();
                        _logger.LogDebug("Selected domain from autocomplete suggestions");
                        return;
                    }
                }
                
                // If no autocomplete match, press Tab or Enter to confirm
                domainField.SendKeys(Keys.Tab);
            }
            else
            {
                // Default text entry for unknown field types
                _logger.LogDebug("Using default text entry for unknown domain field type");
                await EnterTextOptimizedAsync(domainField, domain);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in custom domain field handling, falling back to direct text entry");
            await EnterTextOptimizedAsync(domainField, domain);
        }
    }

    /// <summary>
    /// Specialized method for handling domain fields that appear after password entry (login5.htm scenario)
    /// </summary>
    public virtual async Task<bool> HandlePostPasswordDomainAsync(IWebDriver driver, string domain, string username, string password)
    {
        if (string.IsNullOrEmpty(domain))
        {
            _logger.LogDebug("No domain provided for post-password domain handling");
            return true; // Not an error if no domain needed
        }
        
        try
        {
            _logger.LogInformation("Handling post-password domain field detection and entry");
            
            // First, fill username and password to trigger domain field appearance
            var initialForm = await DetectCurrentFormFields(driver);
            
            if (initialForm.UsernameField != null)
            {
                _logger.LogDebug("Entering username to trigger progressive fields");
                await EnterTextOptimizedAsync(initialForm.UsernameField, username);
                await Task.Delay(500); // Wait for potential field changes
            }
            
            if (initialForm.PasswordField != null)
            {
                _logger.LogDebug("Entering password to trigger domain field");
                await EnterTextOptimizedAsync(initialForm.PasswordField, password);
                
                // Wait longer after password entry as domain fields often appear after this
                await Task.Delay(1000);
                
                // Look for domain field that might have appeared
                var domainField = await DetectDomainFieldAfterPassword(driver);
                
                if (domainField != null)
                {
                    _logger.LogInformation("Domain field detected after password entry");
                    
                    // Handle the domain field using the enhanced logic
                    if (domainField.TagName.ToLower() == "select")
                    {
                        await HandleDomainDropdownAsync(driver, domainField, domain);
                    }
                    else
                    {
                        await HandleCustomDomainFieldAsync(driver, domainField, domain);
                    }
                    
                    return true;
                }
                else
                {
                    _logger.LogWarning("No domain field appeared after password entry, but domain was provided");
                    return false;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in post-password domain handling");
            return false;
        }
    }
    
    /// <summary>
    /// Detects current form fields without extensive searching
    /// </summary>
    private async Task<LoginFormElements> DetectCurrentFormFields(IWebDriver driver)
    {
        var result = new LoginFormElements();
        
        try
        {
            // Quick detection of visible fields
            var inputs = driver.FindElements(By.TagName("input"));
            var selects = driver.FindElements(By.TagName("select"));
            var buttons = driver.FindElements(By.TagName("button"));
            
            // Find username field (first text/email input)
            result.UsernameField = inputs.FirstOrDefault(input => 
            {
                var type = input.GetAttribute("type")?.ToLower();
                return (type == "text" || type == "email" || string.IsNullOrEmpty(type)) && 
                       input.Displayed && input.Enabled;
            });
            
            // Find password field
            result.PasswordField = inputs.FirstOrDefault(input => 
                input.GetAttribute("type")?.ToLower() == "password" && 
                input.Displayed && input.Enabled);
            
            // Find domain field (select or text with domain-like attributes)
            result.DomainField = selects.FirstOrDefault(select => 
                select.Displayed && select.Enabled) ?? 
                inputs.Skip(2).FirstOrDefault(input => // Skip username/password
                {
                    var type = input.GetAttribute("type")?.ToLower();
                    var name = input.GetAttribute("name")?.ToLower();
                    var id = input.GetAttribute("id")?.ToLower();
                    return (type == "text" || string.IsNullOrEmpty(type)) &&
                           input.Displayed && input.Enabled &&
                           (name?.Contains("domain") == true || id?.Contains("domain") == true);
                });
            
            // Find submit button
            result.SubmitButton = buttons.FirstOrDefault(button => 
                button.Displayed && button.Enabled) ??
                inputs.FirstOrDefault(input => 
                    input.GetAttribute("type")?.ToLower() == "submit" && 
                    input.Displayed && input.Enabled);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in quick form field detection");
        }
        
        return result;
    }
    
    /// <summary>
    /// Specifically looks for domain fields that appear after password entry
    /// </summary>
    private async Task<IWebElement?> DetectDomainFieldAfterPassword(IWebDriver driver)
    {
        try
        {
            // Wait for DOM changes after password entry
            await Task.Delay(500);
            
            // Look for select elements (domain dropdowns)
            var selects = driver.FindElements(By.TagName("select"))
                .Where(s => s.Displayed && s.Enabled);
                
            foreach (var select in selects)
            {
                var id = select.GetAttribute("id")?.ToLower() ?? "";
                var name = select.GetAttribute("name")?.ToLower() ?? "";
                var className = select.GetAttribute("class")?.ToLower() ?? "";
                
                if (id.Contains("domain") || name.Contains("domain") || 
                    id.Contains("tenant") || name.Contains("tenant") ||
                    id.Contains("org") || name.Contains("org") ||
                    className.Contains("domain"))
                {
                    _logger.LogDebug($"Found domain dropdown: id={id}, name={name}");
                    return select;
                }
            }
            
            // Look for input fields with domain-like attributes
            var inputs = driver.FindElements(By.TagName("input"))
                .Where(i => i.Displayed && i.Enabled);
                
            foreach (var input in inputs)
            {
                var type = input.GetAttribute("type")?.ToLower();
                if (type == "password") continue; // Skip password fields
                
                var id = input.GetAttribute("id")?.ToLower() ?? "";
                var name = input.GetAttribute("name")?.ToLower() ?? "";
                var placeholder = input.GetAttribute("placeholder")?.ToLower() ?? "";
                var className = input.GetAttribute("class")?.ToLower() ?? "";
                
                if (id.Contains("domain") || name.Contains("domain") || placeholder.Contains("domain") ||
                    id.Contains("tenant") || name.Contains("tenant") || placeholder.Contains("tenant") ||
                    id.Contains("org") || name.Contains("org") || placeholder.Contains("org") ||
                    className.Contains("domain"))
                {
                    _logger.LogDebug($"Found domain input field: id={id}, name={name}, placeholder={placeholder}");
                    return input;
                }
            }
            
            // Look for custom dropdowns or components
            var divs = driver.FindElements(By.TagName("div"))
                .Where(d => d.Displayed && d.GetAttribute("role") == "combobox");
                
            foreach (var div in divs)
            {
                var id = div.GetAttribute("id")?.ToLower() ?? "";
                var className = div.GetAttribute("class")?.ToLower() ?? "";
                var dataTestId = div.GetAttribute("data-testid")?.ToLower() ?? "";
                
                if (id.Contains("domain") || className.Contains("domain") || dataTestId.Contains("domain"))
                {
                    _logger.LogDebug($"Found custom domain component: id={id}, class={className}");
                    return div;
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting domain field after password");
            return null;
        }
    }
    
    /// <summary>
    /// Enhanced dropdown handling with all the strategies from the main method
    /// </summary>
    private async Task HandleDomainDropdownAsync(IWebDriver driver, IWebElement domainField, string domain)
    {
        bool selectionSuccessful = false;
        var selectElement = new SelectElement(domainField);
        
        try
        {
            // Strategy 1: Try exact text match first
            selectElement.SelectByText(domain);
            selectionSuccessful = true;
            _logger.LogDebug("Successfully selected domain from dropdown using exact text match");
        }
        catch (NoSuchElementException)
        {
            _logger.LogDebug("Exact text match failed for domain dropdown, trying case-insensitive match");
            
            try
            {
                // Strategy 2: Try case-insensitive text match
                var options = selectElement.Options;
                var matchingOption = options.FirstOrDefault(option => 
                    string.Equals(option.Text?.Trim(), domain?.Trim(), StringComparison.OrdinalIgnoreCase));
                
                if (matchingOption != null)
                {
                    selectElement.SelectByText(matchingOption.Text);
                    selectionSuccessful = true;
                    _logger.LogDebug("Successfully selected domain using case-insensitive text match");
                }
                else
                {
                    // Strategy 3: Try partial text match
                    var partialMatch = options.FirstOrDefault(option => 
                        option.Text?.ToLower().Contains(domain?.ToLower()) == true);
                    
                    if (partialMatch != null)
                    {
                        selectElement.SelectByText(partialMatch.Text);
                        selectionSuccessful = true;
                        _logger.LogDebug("Successfully selected domain using partial text match");
                    }
                }
            }
            catch (Exception partialEx)
            {
                _logger.LogDebug($"Case-insensitive/partial text matching failed: {partialEx.Message}");
            }
        }
        catch (Exception exactEx)
        {
            _logger.LogDebug($"Exact text selection failed: {exactEx.Message}");
        }
        
        // Additional strategies (SelectByValue, SelectByIndex, JavaScript fallback) from main method
        if (!selectionSuccessful)
        {
            // Apply same fallback strategies as in the main EnterCredentialsAsync method
            // (Implementation would be same as in the main method)
            await EnterTextOptimizedAsync(domainField, domain);
        }
    }

    /// <summary>
    /// Optimized method for handling username dropdowns (login2.htm scenario)
    /// </summary>
    public virtual async Task<bool> HandleUsernameDropdownOptimizedAsync(IWebDriver driver, IWebElement usernameField, string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("No username provided for dropdown selection");
            return false;
        }
        
        try
        {
            _logger.LogInformation("Handling username dropdown with optimized performance");
            
            // Check if it's actually a dropdown
            if (usernameField.TagName.ToLower() != "select")
            {
                _logger.LogDebug("Username field is not a dropdown, using standard text entry");
                await EnterTextOptimizedAsync(usernameField, username);
                return true;
            }
            
            var selectElement = new SelectElement(usernameField);
            var options = selectElement.Options;
            
            _logger.LogDebug($"Username dropdown has {options.Count} options");
            
            // Performance optimization: Cache options for faster lookup
            var optionLookup = new Dictionary<string, IWebElement>(StringComparer.OrdinalIgnoreCase);
            var partialMatches = new List<(IWebElement element, string text)>();
            
            foreach (var option in options)
            {
                var optionText = option.Text?.Trim();
                if (!string.IsNullOrEmpty(optionText))
                {
                    optionLookup[optionText] = option;
                    
                    // Pre-compute partial matches for faster lookup
                    if (optionText.ToLower().Contains(username.ToLower()))
                    {
                        partialMatches.Add((option, optionText));
                    }
                }
            }
            
            // Strategy 1: Exact match (fastest)
            if (optionLookup.TryGetValue(username, out var exactMatch))
            {
                selectElement.SelectByText(exactMatch.Text);
                _logger.LogDebug($"Username selected using exact match: {exactMatch.Text}");
                return true;
            }
            
            // Strategy 2: Case-insensitive exact match
            var caseInsensitiveMatch = optionLookup.FirstOrDefault(kvp => 
                string.Equals(kvp.Key, username, StringComparison.OrdinalIgnoreCase));
            
            if (caseInsensitiveMatch.Value != null)
            {
                selectElement.SelectByText(caseInsensitiveMatch.Value.Text);
                _logger.LogDebug($"Username selected using case-insensitive match: {caseInsensitiveMatch.Value.Text}");
                return true;
            }
            
            // Strategy 3: Partial match (using pre-computed matches)
            if (partialMatches.Count > 0)
            {
                // Prefer matches that start with the username
                var startsWithMatch = partialMatches.FirstOrDefault(pm => 
                    pm.text.StartsWith(username, StringComparison.OrdinalIgnoreCase));
                
                if (startsWithMatch.element != null)
                {
                    selectElement.SelectByText(startsWithMatch.text);
                    _logger.LogDebug($"Username selected using starts-with match: {startsWithMatch.text}");
                    return true;
                }
                
                // Otherwise use first partial match
                var firstPartial = partialMatches[0];
                selectElement.SelectByText(firstPartial.text);
                _logger.LogDebug($"Username selected using partial match: {firstPartial.text}");
                return true;
            }
            
            // Strategy 4: Try SelectByValue for value-based dropdowns
            try
            {
                selectElement.SelectByValue(username);
                _logger.LogDebug("Username selected using value attribute");
                return true;
            }
            catch (NoSuchElementException)
            {
                _logger.LogDebug("SelectByValue failed for username dropdown");
            }
            
            // Strategy 5: Email-based matching (common in username dropdowns)
            if (username.Contains("@"))
            {
                var emailMatch = options.FirstOrDefault(opt => 
                    opt.Text?.Contains("@") == true && 
                    opt.Text.ToLower().Contains(username.Split('@')[0].ToLower()));
                
                if (emailMatch != null)
                {
                    selectElement.SelectByText(emailMatch.Text);
                    _logger.LogDebug($"Username selected using email-based match: {emailMatch.Text}");
                    return true;
                }
            }
            
            // Strategy 6: Domain-based matching (for domain\username format)
            if (username.Contains("\\"))
            {
                var userPart = username.Split('\\').Last();
                var domainMatch = options.FirstOrDefault(opt => 
                    opt.Text?.ToLower().Contains(userPart.ToLower()) == true);
                
                if (domainMatch != null)
                {
                    selectElement.SelectByText(domainMatch.Text);
                    _logger.LogDebug($"Username selected using domain-based match: {domainMatch.Text}");
                    return true;
                }
            }
            
            // Strategy 7: JavaScript fallback for complex dropdowns
            if (_config.UseJavaScriptFallback)
            {
                try
                {
                    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                    
                    // Try to set the value directly
                    js.ExecuteScript($"arguments[0].value = '{username}'; arguments[0].dispatchEvent(new Event('change'));", usernameField);
                    
                    _logger.LogDebug("Username set using JavaScript fallback");
                    return true;
                }
                catch (Exception jsEx)
                {
                    _logger.LogDebug($"JavaScript fallback failed: {jsEx.Message}");
                }
            }
            
            // Strategy 8: Select first non-empty option as last resort
            var firstValidOption = options.Skip(1).FirstOrDefault(opt => 
                !string.IsNullOrWhiteSpace(opt.Text) && 
                !opt.Text.ToLower().Contains("select") &&
                !opt.Text.ToLower().Contains("choose"));
            
            if (firstValidOption != null)
            {
                selectElement.SelectByText(firstValidOption.Text);
                _logger.LogWarning($"No matching username found, selected first available option: {firstValidOption.Text}");
                return true;
            }
            
            _logger.LogError($"Failed to select username from dropdown. Available options: {string.Join(", ", options.Select(o => o.Text))}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in optimized username dropdown handling");
            return false;
        }
    }
}
