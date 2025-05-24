using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Models;
using ChromeConnect.Core;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Service for handling dynamic form detection across various scenarios
    /// Consolidates all improvements for login field detection and domain handling
    /// </summary>
    public class DynamicFormDetectionService
    {
        private readonly ILogger<DynamicFormDetectionService> _logger;
        private readonly LoginDetector _loginDetector;
        private readonly CredentialManager _credentialManager;

        public DynamicFormDetectionService(
            ILogger<DynamicFormDetectionService> logger,
            LoginDetector loginDetector,
            CredentialManager credentialManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loginDetector = loginDetector ?? throw new ArgumentNullException(nameof(loginDetector));
            _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
        }

        /// <summary>
        /// Comprehensive form detection that handles all dynamic scenarios
        /// </summary>
        public async Task<LoginFormElements?> DetectDynamicFormAsync(IWebDriver driver, string username, string password, string domain)
        {
            _logger.LogInformation("Starting comprehensive dynamic form detection");

            try
            {
                // Strategy 1: Try standard detection first (fastest for static forms)
                var standardForm = await _loginDetector.DetectLoginFormAsync(driver);
                if (IsCompleteForm(standardForm))
                {
                    _logger.LogInformation("Complete form detected using standard detection");
                    return standardForm;
                }

                // Strategy 2: Try progressive detection for dynamic forms
                _logger.LogInformation("Standard detection incomplete, trying progressive detection");
                var progressiveForm = await _loginDetector.DetectProgressiveFormAsync(driver, username, password, domain);
                if (IsCompleteForm(progressiveForm))
                {
                    _logger.LogInformation("Complete form detected using progressive detection");
                    return progressiveForm;
                }

                // Strategy 3: Try mutation-based detection for highly dynamic forms
                _logger.LogInformation("Progressive detection incomplete, trying mutation-based detection");
                var mutationForm = await DetectWithMutationObserverAsync(driver, username, password, domain);
                if (IsCompleteForm(mutationForm))
                {
                    _logger.LogInformation("Complete form detected using mutation-based detection");
                    return mutationForm;
                }

                // Strategy 4: Hybrid approach - combine best elements from all attempts
                _logger.LogInformation("Individual strategies incomplete, trying hybrid approach");
                var hybridForm = CombineBestElements(standardForm, progressiveForm, mutationForm);
                if (IsValidForm(hybridForm))
                {
                    _logger.LogInformation("Valid form created using hybrid approach");
                    return hybridForm;
                }

                _logger.LogWarning("All dynamic form detection strategies failed");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in dynamic form detection");
                return null;
            }
        }

        /// <summary>
        /// Enhanced form detection using mutation observers for highly dynamic forms
        /// </summary>
        private async Task<LoginFormElements?> DetectWithMutationObserverAsync(IWebDriver driver, string username, string password, string domain)
        {
            try
            {
                _logger.LogDebug("Setting up mutation observer for dynamic form detection");

                var result = new LoginFormElements();
                var jsExecutor = (IJavaScriptExecutor)driver;

                // Set up mutation observer to track DOM changes
                var observerScript = @"
                    window.formMutationData = {
                        mutations: [],
                        observer: null
                    };
                    
                    window.formMutationData.observer = new MutationObserver(function(mutations) {
                        mutations.forEach(function(mutation) {
                            if (mutation.type === 'childList') {
                                mutation.addedNodes.forEach(function(node) {
                                    if (node.nodeType === 1) { // Element node
                                        var tagName = node.tagName ? node.tagName.toLowerCase() : '';
                                        if (tagName === 'input' || tagName === 'select' || tagName === 'button') {
                                            window.formMutationData.mutations.push({
                                                type: 'added',
                                                tagName: tagName,
                                                id: node.id || '',
                                                name: node.name || '',
                                                className: node.className || '',
                                                inputType: node.type || ''
                                            });
                                        }
                                    }
                                });
                            }
                        });
                    });
                    
                    window.formMutationData.observer.observe(document.body, {
                        childList: true,
                        subtree: true
                    });
                ";

                jsExecutor.ExecuteScript(observerScript);

                // Initial form detection
                var initialForm = await _loginDetector.DetectLoginFormAsync(driver);
                if (initialForm?.UsernameField != null)
                {
                    result.UsernameField = initialForm.UsernameField;
                }

                // Enter username and monitor for changes
                if (result.UsernameField != null && !string.IsNullOrEmpty(username))
                {
                    await EnterFieldWithMutationMonitoring(driver, result.UsernameField, username);
                    
                    // Check for new fields after username entry
                    var newFields = await GetMutationResults(driver);
                    await UpdateFormFromMutations(driver, result, newFields);
                }

                // Enter password and monitor for changes
                if (result.PasswordField != null && !string.IsNullOrEmpty(password))
                {
                    await EnterFieldWithMutationMonitoring(driver, result.PasswordField, password);
                    
                    // Check for new fields after password entry
                    var newFields = await GetMutationResults(driver);
                    await UpdateFormFromMutations(driver, result, newFields);
                }

                // Handle domain field if it appeared
                if (result.DomainField != null && !string.IsNullOrEmpty(domain))
                {
                    await HandleDynamicDomainField(driver, result.DomainField, domain);
                }

                // Final scan for submit button
                if (result.SubmitButton == null)
                {
                    var finalForm = await _loginDetector.DetectLoginFormAsync(driver);
                    if (finalForm?.SubmitButton != null)
                    {
                        result.SubmitButton = finalForm.SubmitButton;
                    }
                }

                // Clean up mutation observer
                jsExecutor.ExecuteScript("if (window.formMutationData && window.formMutationData.observer) { window.formMutationData.observer.disconnect(); }");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in mutation-based form detection");
                return null;
            }
        }

        /// <summary>
        /// Enters text into a field while monitoring for DOM mutations
        /// </summary>
        private async Task EnterFieldWithMutationMonitoring(IWebDriver driver, IWebElement field, string value)
        {
            try
            {
                var jsExecutor = (IJavaScriptExecutor)driver;
                
                // Clear previous mutations
                jsExecutor.ExecuteScript("if (window.formMutationData) { window.formMutationData.mutations = []; }");

                // Enter the value with events
                field.Clear();
                field.Click();
                
                // Type character by character to trigger progressive events
                foreach (char c in value)
                {
                    field.SendKeys(c.ToString());
                    await Task.Delay(50);
                }

                // Trigger events that might cause field appearance
                jsExecutor.ExecuteScript(@"
                    arguments[0].dispatchEvent(new Event('input', { bubbles: true }));
                    arguments[0].dispatchEvent(new Event('change', { bubbles: true }));
                    arguments[0].dispatchEvent(new Event('blur', { bubbles: true }));
                ", field);

                // Wait for potential DOM changes
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in field entry with mutation monitoring");
            }
        }

        /// <summary>
        /// Gets mutation results from the JavaScript observer
        /// </summary>
        private async Task<List<dynamic>> GetMutationResults(IWebDriver driver)
        {
            try
            {
                var jsExecutor = (IJavaScriptExecutor)driver;
                var mutations = jsExecutor.ExecuteScript("return window.formMutationData ? window.formMutationData.mutations : [];");
                
                if (mutations is System.Collections.ObjectModel.ReadOnlyCollection<object> mutationList)
                {
                    return mutationList.Cast<dynamic>().ToList();
                }
                
                return new List<dynamic>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting mutation results");
                return new List<dynamic>();
            }
        }

        /// <summary>
        /// Updates form elements based on detected mutations
        /// </summary>
        private async Task UpdateFormFromMutations(IWebDriver driver, LoginFormElements form, List<dynamic> mutations)
        {
            try
            {
                foreach (var mutation in mutations)
                {
                    var tagName = mutation.tagName?.ToString()?.ToLower();
                    var id = mutation.id?.ToString()?.ToLower() ?? "";
                    var name = mutation.name?.ToString()?.ToLower() ?? "";
                    var className = mutation.className?.ToString()?.ToLower() ?? "";
                    var inputType = mutation.inputType?.ToString()?.ToLower() ?? "";

                    // Try to find the actual element
                    IWebElement? element = null;
                    
                    if (!string.IsNullOrEmpty(id))
                    {
                        try { element = driver.FindElement(By.Id(id)); } catch { }
                    }
                    
                    if (element == null && !string.IsNullOrEmpty(name))
                    {
                        try { element = driver.FindElement(By.Name(name)); } catch { }
                    }

                    if (element != null && element.Displayed && element.Enabled)
                    {
                        // Determine field type and assign
                        if (tagName == "input" && inputType == "password" && form.PasswordField == null)
                        {
                            form.PasswordField = element;
                            _logger.LogDebug("Password field detected from mutation");
                        }
                        else if (tagName == "select" && form.DomainField == null && 
                                (id.Contains("domain") || name.Contains("domain") || className.Contains("domain")))
                        {
                            form.DomainField = element;
                            _logger.LogDebug("Domain field detected from mutation");
                        }
                        else if (tagName == "button" && form.SubmitButton == null)
                        {
                            form.SubmitButton = element;
                            _logger.LogDebug("Submit button detected from mutation");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating form from mutations");
            }
        }

        /// <summary>
        /// Handles dynamic domain fields with all the enhanced strategies
        /// </summary>
        private async Task HandleDynamicDomainField(IWebDriver driver, IWebElement domainField, string domain)
        {
            try
            {
                if (domainField.TagName.ToLower() == "select")
                {
                    // Use the enhanced dropdown handling from CredentialManager
                    var selectElement = new SelectElement(domainField);
                    
                    // Try multiple selection strategies
                    bool success = await TryDomainSelection(selectElement, domain);
                    
                    if (!success)
                    {
                        _logger.LogWarning("All domain selection strategies failed");
                    }
                }
                else
                {
                    // Handle custom domain fields
                    await _credentialManager.HandleCustomDomainFieldAsync(driver, domainField, domain);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling dynamic domain field");
            }
        }

        /// <summary>
        /// Tries multiple domain selection strategies
        /// </summary>
        private async Task<bool> TryDomainSelection(SelectElement selectElement, string domain)
        {
            try
            {
                // Strategy 1: Exact text match
                selectElement.SelectByText(domain);
                return true;
            }
            catch (NoSuchElementException)
            {
                try
                {
                    // Strategy 2: Case-insensitive match
                    var options = selectElement.Options;
                    var matchingOption = options.FirstOrDefault(option => 
                        string.Equals(option.Text?.Trim(), domain?.Trim(), StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingOption != null)
                    {
                        selectElement.SelectByText(matchingOption.Text);
                        return true;
                    }

                    // Strategy 3: Partial match
                    var partialMatch = options.FirstOrDefault(option => 
                        option.Text?.ToLower().Contains(domain?.ToLower()) == true);
                    
                    if (partialMatch != null)
                    {
                        selectElement.SelectByText(partialMatch.Text);
                        return true;
                    }

                    // Strategy 4: Select first valid option
                    var firstValid = options.Skip(1).FirstOrDefault(opt => 
                        !string.IsNullOrWhiteSpace(opt.Text) && 
                        !opt.Text.ToLower().Contains("select"));
                    
                    if (firstValid != null)
                    {
                        selectElement.SelectByText(firstValid.Text);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Domain selection strategies failed: {ex.Message}");
                }
            }
            
            return false;
        }

        /// <summary>
        /// Combines the best elements from multiple detection attempts
        /// </summary>
        private LoginFormElements? CombineBestElements(params LoginFormElements?[] forms)
        {
            var result = new LoginFormElements();
            bool hasAnyElement = false;

            foreach (var form in forms)
            {
                if (form == null) continue;

                if (result.UsernameField == null && form.UsernameField != null)
                {
                    result.UsernameField = form.UsernameField;
                    hasAnyElement = true;
                }

                if (result.PasswordField == null && form.PasswordField != null)
                {
                    result.PasswordField = form.PasswordField;
                    hasAnyElement = true;
                }

                if (result.DomainField == null && form.DomainField != null)
                {
                    result.DomainField = form.DomainField;
                    hasAnyElement = true;
                }

                if (result.SubmitButton == null && form.SubmitButton != null)
                {
                    result.SubmitButton = form.SubmitButton;
                    hasAnyElement = true;
                }
            }

            return hasAnyElement ? result : null;
        }

        /// <summary>
        /// Checks if a form is complete (has essential fields)
        /// </summary>
        private bool IsCompleteForm(LoginFormElements? form)
        {
            return form != null && 
                   form.UsernameField != null && 
                   form.PasswordField != null;
        }

        /// <summary>
        /// Checks if a form is valid (has at least username or password)
        /// </summary>
        private bool IsValidForm(LoginFormElements? form)
        {
            return form != null && 
                   (form.UsernameField != null || form.PasswordField != null);
        }
    }
} 