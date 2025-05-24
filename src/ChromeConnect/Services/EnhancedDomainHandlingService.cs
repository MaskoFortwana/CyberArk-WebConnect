using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Models;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Enhanced service for comprehensive domain field handling across all scenarios
    /// Consolidates improvements for domain dropdowns, custom fields, and progressive detection
    /// </summary>
    public class EnhancedDomainHandlingService
    {
        private readonly ILogger<EnhancedDomainHandlingService> _logger;

        public EnhancedDomainHandlingService(ILogger<EnhancedDomainHandlingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Comprehensive domain field handling for all scenarios
        /// </summary>
        public async Task<bool> HandleDomainFieldAsync(IWebDriver driver, IWebElement domainField, string domain, DomainHandlingContext context = null)
        {
            if (domainField == null)
            {
                _logger.LogDebug("No domain field provided");
                return true; // Not an error if no domain field
            }

            if (string.IsNullOrEmpty(domain))
            {
                _logger.LogDebug("No domain value provided");
                return true; // Not an error if no domain needed
            }

            try
            {
                _logger.LogInformation($"Handling domain field: {domainField.TagName.ToLower()}");

                var fieldType = AnalyzeDomainFieldType(domainField);
                _logger.LogDebug($"Domain field type identified: {fieldType}");

                switch (fieldType)
                {
                    case DomainFieldType.StandardDropdown:
                        return await HandleStandardDropdownAsync(domainField, domain);
                    
                    case DomainFieldType.CustomDropdown:
                        return await HandleCustomDropdownAsync(driver, domainField, domain);
                    
                    case DomainFieldType.TextInput:
                        return await HandleTextInputAsync(domainField, domain);
                    
                    case DomainFieldType.ComboBox:
                        return await HandleComboBoxAsync(driver, domainField, domain);
                    
                    case DomainFieldType.AutoComplete:
                        return await HandleAutoCompleteAsync(driver, domainField, domain);
                    
                    default:
                        _logger.LogWarning($"Unknown domain field type, attempting generic handling");
                        return await HandleGenericDomainFieldAsync(driver, domainField, domain);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive domain field handling");
                return false;
            }
        }

        /// <summary>
        /// Analyzes domain field to determine its type and implementation
        /// </summary>
        private DomainFieldType AnalyzeDomainFieldType(IWebElement domainField)
        {
            try
            {
                var tagName = domainField.TagName.ToLower();
                var role = domainField.GetAttribute("role")?.ToLower();
                var className = domainField.GetAttribute("class")?.ToLower() ?? "";
                var type = domainField.GetAttribute("type")?.ToLower();

                // Standard HTML select dropdown
                if (tagName == "select")
                {
                    return DomainFieldType.StandardDropdown;
                }

                // Text input field
                if (tagName == "input" && (type == "text" || string.IsNullOrEmpty(type)))
                {
                    // Check for autocomplete indicators
                    if (className.Contains("autocomplete") || className.Contains("typeahead") ||
                        domainField.GetAttribute("autocomplete") != null ||
                        domainField.GetAttribute("list") != null)
                    {
                        return DomainFieldType.AutoComplete;
                    }
                    
                    return DomainFieldType.TextInput;
                }

                // Custom dropdown implementations
                if (role == "combobox" || role == "listbox" || role == "button")
                {
                    return DomainFieldType.CustomDropdown;
                }

                // Div-based custom dropdowns
                if (tagName == "div" && (className.Contains("dropdown") || className.Contains("select") ||
                    className.Contains("combobox") || className.Contains("picker")))
                {
                    return DomainFieldType.CustomDropdown;
                }

                // Combobox pattern (input + dropdown)
                if (tagName == "input" && (role == "combobox" || className.Contains("combobox")))
                {
                    return DomainFieldType.ComboBox;
                }

                return DomainFieldType.Unknown;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing domain field type");
                return DomainFieldType.Unknown;
            }
        }

        /// <summary>
        /// Handles standard HTML select dropdowns with comprehensive selection strategies
        /// </summary>
        private async Task<bool> HandleStandardDropdownAsync(IWebElement domainField, string domain)
        {
            try
            {
                var selectElement = new SelectElement(domainField);
                var options = selectElement.Options;
                
                _logger.LogDebug($"Standard dropdown has {options.Count} options");

                // Pre-compute option mappings for performance
                var optionMappings = BuildOptionMappings(options);

                // Strategy 1: Exact text match
                if (optionMappings.ExactMatches.TryGetValue(domain, out var exactOption))
                {
                    selectElement.SelectByText(exactOption.Text);
                    _logger.LogDebug($"Selected domain using exact match: {exactOption.Text}");
                    return true;
                }

                // Strategy 2: Case-insensitive exact match
                var caseInsensitiveMatch = optionMappings.CaseInsensitiveMatches
                    .FirstOrDefault(kvp => string.Equals(kvp.Key, domain, StringComparison.OrdinalIgnoreCase));
                
                if (caseInsensitiveMatch.Value != null)
                {
                    selectElement.SelectByText(caseInsensitiveMatch.Value.Text);
                    _logger.LogDebug($"Selected domain using case-insensitive match: {caseInsensitiveMatch.Value.Text}");
                    return true;
                }

                // Strategy 3: Partial text match
                var partialMatch = optionMappings.PartialMatches
                    .FirstOrDefault(opt => opt.Text?.ToLower().Contains(domain.ToLower()) == true);
                
                if (partialMatch != null)
                {
                    selectElement.SelectByText(partialMatch.Text);
                    _logger.LogDebug($"Selected domain using partial match: {partialMatch.Text}");
                    return true;
                }

                // Strategy 4: Value-based selection
                try
                {
                    selectElement.SelectByValue(domain);
                    _logger.LogDebug("Selected domain using value attribute");
                    return true;
                }
                catch (NoSuchElementException)
                {
                    _logger.LogDebug("SelectByValue failed");
                }

                // Strategy 5: Fuzzy matching for similar domains
                var fuzzyMatch = FindFuzzyMatch(optionMappings.AllOptions, domain);
                if (fuzzyMatch != null)
                {
                    selectElement.SelectByText(fuzzyMatch.Text);
                    _logger.LogDebug($"Selected domain using fuzzy match: {fuzzyMatch.Text}");
                    return true;
                }

                // Strategy 6: Select first valid option as fallback
                var firstValid = optionMappings.ValidOptions.FirstOrDefault();
                if (firstValid != null)
                {
                    selectElement.SelectByText(firstValid.Text);
                    _logger.LogInformation($"No matching domain found, selected first valid option: {firstValid.Text}");
                    return true;
                }

                _logger.LogWarning($"All selection strategies failed. Available options: {string.Join(", ", options.Select(o => o.Text))}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling standard dropdown");
                return false;
            }
        }

        /// <summary>
        /// Handles custom dropdown implementations (div-based, role-based)
        /// </summary>
        private async Task<bool> HandleCustomDropdownAsync(IWebDriver driver, IWebElement domainField, string domain)
        {
            try
            {
                _logger.LogDebug("Handling custom dropdown implementation");

                // Strategy 1: Try clicking to open dropdown
                await ClickToOpenDropdown(domainField);
                await Task.Delay(500); // Wait for dropdown to open

                // Strategy 2: Look for dropdown options
                var options = await FindDropdownOptions(driver, domainField);
                
                if (options.Any())
                {
                    var selectedOption = SelectBestOption(options, domain);
                    if (selectedOption != null)
                    {
                        selectedOption.Click();
                        _logger.LogDebug($"Selected custom dropdown option: {selectedOption.Text}");
                        return true;
                    }
                }

                // Strategy 3: Try typing the domain value
                domainField.Clear();
                domainField.SendKeys(domain);
                
                // Trigger events that might be required
                var jsExecutor = (IJavaScriptExecutor)driver;
                jsExecutor.ExecuteScript(@"
                    arguments[0].dispatchEvent(new Event('input', { bubbles: true }));
                    arguments[0].dispatchEvent(new Event('change', { bubbles: true }));
                    arguments[0].dispatchEvent(new Event('blur', { bubbles: true }));
                ", domainField);

                _logger.LogDebug("Entered domain value into custom dropdown");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling custom dropdown");
                return false;
            }
        }

        /// <summary>
        /// Handles text input domain fields
        /// </summary>
        private async Task<bool> HandleTextInputAsync(IWebElement domainField, string domain)
        {
            try
            {
                domainField.Clear();
                domainField.SendKeys(domain);
                _logger.LogDebug("Entered domain into text input field");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling text input");
                return false;
            }
        }

        /// <summary>
        /// Handles combobox implementations (input + dropdown)
        /// </summary>
        private async Task<bool> HandleComboBoxAsync(IWebDriver driver, IWebElement domainField, string domain)
        {
            try
            {
                // Type the domain to trigger autocomplete
                domainField.Clear();
                domainField.SendKeys(domain);
                await Task.Delay(500); // Wait for autocomplete

                // Look for autocomplete suggestions
                var suggestions = await FindAutocompleteSuggestions(driver);
                
                if (suggestions.Any())
                {
                    var bestMatch = SelectBestOption(suggestions, domain);
                    if (bestMatch != null)
                    {
                        bestMatch.Click();
                        _logger.LogDebug($"Selected combobox suggestion: {bestMatch.Text}");
                        return true;
                    }
                }

                // If no suggestions, just keep the typed value
                _logger.LogDebug("No combobox suggestions found, keeping typed value");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling combobox");
                return false;
            }
        }

        /// <summary>
        /// Handles autocomplete input fields
        /// </summary>
        private async Task<bool> HandleAutoCompleteAsync(IWebDriver driver, IWebElement domainField, string domain)
        {
            try
            {
                // Type domain to trigger autocomplete
                domainField.Clear();
                
                // Type character by character to trigger autocomplete
                foreach (char c in domain)
                {
                    domainField.SendKeys(c.ToString());
                    await Task.Delay(100);
                }

                await Task.Delay(500); // Wait for autocomplete suggestions

                // Look for autocomplete dropdown or datalist
                var suggestions = await FindAutocompleteSuggestions(driver);
                
                if (suggestions.Any())
                {
                    var bestMatch = SelectBestOption(suggestions, domain);
                    if (bestMatch != null)
                    {
                        bestMatch.Click();
                        _logger.LogDebug($"Selected autocomplete suggestion: {bestMatch.Text}");
                        return true;
                    }
                }

                _logger.LogDebug("Autocomplete completed with typed value");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling autocomplete");
                return false;
            }
        }

        /// <summary>
        /// Generic domain field handling for unknown types
        /// </summary>
        private async Task<bool> HandleGenericDomainFieldAsync(IWebDriver driver, IWebElement domainField, string domain)
        {
            try
            {
                // Try multiple approaches
                
                // Approach 1: Direct text entry
                try
                {
                    domainField.Clear();
                    domainField.SendKeys(domain);
                    _logger.LogDebug("Generic handling: entered text directly");
                    return true;
                }
                catch (Exception)
                {
                    _logger.LogDebug("Direct text entry failed");
                }

                // Approach 2: JavaScript value setting
                try
                {
                    var jsExecutor = (IJavaScriptExecutor)driver;
                    jsExecutor.ExecuteScript($"arguments[0].value = '{domain}';", domainField);
                    jsExecutor.ExecuteScript("arguments[0].dispatchEvent(new Event('change'));", domainField);
                    _logger.LogDebug("Generic handling: set value via JavaScript");
                    return true;
                }
                catch (Exception)
                {
                    _logger.LogDebug("JavaScript value setting failed");
                }

                // Approach 3: Click and type
                try
                {
                    domainField.Click();
                    await Task.Delay(200);
                    domainField.SendKeys(domain);
                    _logger.LogDebug("Generic handling: clicked and typed");
                    return true;
                }
                catch (Exception)
                {
                    _logger.LogDebug("Click and type failed");
                }

                _logger.LogWarning("All generic handling approaches failed");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in generic domain field handling");
                return false;
            }
        }

        /// <summary>
        /// Builds optimized option mappings for fast lookup
        /// </summary>
        private OptionMappings BuildOptionMappings(IList<IWebElement> options)
        {
            var mappings = new OptionMappings();

            foreach (var option in options)
            {
                var text = option.Text?.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                mappings.AllOptions.Add(option);
                mappings.ExactMatches[text] = option;
                mappings.CaseInsensitiveMatches[text.ToLower()] = option;

                // Only include non-placeholder options as valid
                if (!IsPlaceholderOption(text))
                {
                    mappings.ValidOptions.Add(option);
                    mappings.PartialMatches.Add(option);
                }
            }

            return mappings;
        }

        /// <summary>
        /// Determines if an option is a placeholder (like "Select domain...")
        /// </summary>
        private bool IsPlaceholderOption(string optionText)
        {
            var lowerText = optionText.ToLower();
            return lowerText.Contains("select") || 
                   lowerText.Contains("choose") || 
                   lowerText.Contains("pick") ||
                   lowerText.StartsWith("-") ||
                   string.IsNullOrWhiteSpace(optionText);
        }

        /// <summary>
        /// Finds fuzzy matches for domain names
        /// </summary>
        private IWebElement? FindFuzzyMatch(List<IWebElement> options, string domain)
        {
            var domainLower = domain.ToLower();
            
            // Look for options that contain parts of the domain
            foreach (var option in options)
            {
                var optionText = option.Text?.ToLower();
                if (string.IsNullOrEmpty(optionText)) continue;

                // Check if domain parts are in the option
                var domainParts = domainLower.Split('.', '-', '_', ' ');
                var matchCount = domainParts.Count(part => optionText.Contains(part));
                
                if (matchCount >= domainParts.Length / 2) // At least half the parts match
                {
                    return option;
                }
            }

            return null;
        }

        /// <summary>
        /// Clicks element to open dropdown
        /// </summary>
        private async Task ClickToOpenDropdown(IWebElement element)
        {
            try
            {
                element.Click();
            }
            catch (Exception)
            {
                // Try JavaScript click if regular click fails
                // This would need the driver instance passed in
            }
        }

        /// <summary>
        /// Finds dropdown options after opening custom dropdown
        /// </summary>
        private async Task<List<IWebElement>> FindDropdownOptions(IWebDriver driver, IWebElement dropdownElement)
        {
            var options = new List<IWebElement>();

            try
            {
                // Common selectors for dropdown options
                var selectors = new[]
                {
                    ".dropdown-item",
                    ".option",
                    ".menu-item",
                    "[role='option']",
                    ".select-option",
                    "li",
                    ".item"
                };

                foreach (var selector in selectors)
                {
                    try
                    {
                        var elements = driver.FindElements(By.CssSelector(selector))
                            .Where(e => e.Displayed && e.Enabled)
                            .ToList();
                        
                        if (elements.Any())
                        {
                            options.AddRange(elements);
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        // Continue to next selector
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error finding dropdown options: {ex.Message}");
            }

            return options;
        }

        /// <summary>
        /// Finds autocomplete suggestions
        /// </summary>
        private async Task<List<IWebElement>> FindAutocompleteSuggestions(IWebDriver driver)
        {
            var suggestions = new List<IWebElement>();

            try
            {
                var selectors = new[]
                {
                    ".autocomplete-suggestion",
                    ".suggestion",
                    ".typeahead-suggestion",
                    "[role='option']",
                    ".ui-menu-item",
                    "datalist option"
                };

                foreach (var selector in selectors)
                {
                    try
                    {
                        var elements = driver.FindElements(By.CssSelector(selector))
                            .Where(e => e.Displayed)
                            .ToList();
                        
                        if (elements.Any())
                        {
                            suggestions.AddRange(elements);
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        // Continue to next selector
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error finding autocomplete suggestions: {ex.Message}");
            }

            return suggestions;
        }

        /// <summary>
        /// Selects the best option from a list based on domain matching
        /// </summary>
        private IWebElement? SelectBestOption(List<IWebElement> options, string domain)
        {
            if (!options.Any()) return null;

            var domainLower = domain.ToLower();

            // Exact match
            var exactMatch = options.FirstOrDefault(opt => 
                string.Equals(opt.Text?.Trim(), domain, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null) return exactMatch;

            // Partial match
            var partialMatch = options.FirstOrDefault(opt => 
                opt.Text?.ToLower().Contains(domainLower) == true);
            if (partialMatch != null) return partialMatch;

            // First non-placeholder option
            var firstValid = options.FirstOrDefault(opt => 
                !IsPlaceholderOption(opt.Text ?? ""));
            
            return firstValid;
        }
    }

    /// <summary>
    /// Domain field type enumeration
    /// </summary>
    public enum DomainFieldType
    {
        StandardDropdown,
        CustomDropdown,
        TextInput,
        ComboBox,
        AutoComplete,
        Unknown
    }

    /// <summary>
    /// Context for domain handling operations
    /// </summary>
    public class DomainHandlingContext
    {
        public bool IsProgressiveForm { get; set; }
        public bool AppearedAfterPassword { get; set; }
        public TimeSpan WaitTime { get; set; } = TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// Optimized option mappings for fast lookup
    /// </summary>
    internal class OptionMappings
    {
        public Dictionary<string, IWebElement> ExactMatches { get; } = new();
        public Dictionary<string, IWebElement> CaseInsensitiveMatches { get; } = new();
        public List<IWebElement> PartialMatches { get; } = new();
        public List<IWebElement> ValidOptions { get; } = new();
        public List<IWebElement> AllOptions { get; } = new();
    }
} 