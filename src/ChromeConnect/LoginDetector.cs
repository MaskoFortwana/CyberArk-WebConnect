using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Models;
using ChromeConnect.Services;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace ChromeConnect.Core;

public class LoginDetector
{
    private readonly ILogger<LoginDetector> _logger;
    private readonly DetectionMetricsService _metricsService;
    
    // Performance optimization: Cache compiled regex patterns
    private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();
    
    // Performance optimization: Element cache for current page
    private readonly Dictionary<string, List<IWebElement>> _elementCache = new();
    private string _cachedUrl = "";
    
    // Common variations and synonyms for fuzzy matching
    private static readonly Dictionary<string, string[]> UsernameVariations = new()
    {
        { "username", new[] { "user", "userid", "user_id", "user-id", "login", "loginid", "login_id", "email", "e-mail", "mail", "account", "uid" } },
        { "user", new[] { "username", "userid", "user_id", "user-id", "login", "account", "uid" } },
        { "email", new[] { "e-mail", "mail", "email-address", "email_address", "emailaddress", "login", "username" } },
        { "login", new[] { "username", "user", "userid", "loginid", "account", "signin" } }
    };

    private static readonly Dictionary<string, string[]> PasswordVariations = new()
    {
        { "password", new[] { "pass", "pwd", "passwd", "passphrase", "pass-word", "pass_word", "pin", "secret" } },
        { "pass", new[] { "password", "pwd", "passwd", "passphrase", "pin" } },
        { "pwd", new[] { "password", "pass", "passwd", "passphrase" } }
    };

    private static readonly Dictionary<string, string[]> DomainVariations = new()
    {
        { "domain", new[] { "tenant", "organization", "org", "company", "corp", "realm", "authority" } },
        { "tenant", new[] { "domain", "organization", "org", "company", "realm" } },
        { "organization", new[] { "org", "domain", "tenant", "company", "corp" } }
    };

    private static readonly Dictionary<string, string[]> SubmitVariations = new()
    {
        { "login", new[] { "log-in", "log_in", "signin", "sign-in", "sign_in", "submit", "enter", "go", "connect" } },
        { "submit", new[] { "login", "signin", "enter", "go", "send", "continue", "proceed" } },
        { "signin", new[] { "sign-in", "sign_in", "login", "log-in", "enter" } }
    };

    public LoginDetector(ILogger<LoginDetector> logger, DetectionMetricsService metricsService = null)
    {
        _logger = logger;
        _metricsService = metricsService; // Optional service for enhanced metrics
    }

    /// <summary>
    /// Performance-optimized login form detection with batched DOM queries and intelligent waiting
    /// </summary>
    public virtual async Task<LoginFormElements?> DetectLoginFormAsync(IWebDriver driver)
    {
        _logger.LogInformation("Starting optimized login form detection");
        
        try
        {
            var currentUrl = driver.Url;
            
            // Performance optimization: Check if we need to refresh element cache
            if (_cachedUrl != currentUrl)
            {
                _elementCache.Clear();
                _cachedUrl = currentUrl;
            }

            // **NEW: IMMEDIATE FAST-PATH DETECTION - Try to detect common login forms instantly**
            var fastPathResult = await TryFastPathDetectionAsync(driver);
            if (IsValidLoginForm(fastPathResult))
            {
                _logger.LogInformation("Login form detected using FAST-PATH detection in minimal time");
                LogDetectedElements(fastPathResult);
                return fastPathResult;
            }

            // Get method recommendation from historical data if metrics service is available
            DetectionMethod recommendedMethod = DetectionMethod.UrlSpecific;
            string recommendationReasoning = "Default fallback strategy";
            
            if (_metricsService != null)
            {
                var recommendation = _metricsService.GetRecommendedMethod(currentUrl);
                recommendedMethod = recommendation.Method;
                recommendationReasoning = recommendation.Reasoning;
                _logger.LogInformation($"Recommended detection method: {recommendedMethod} - {recommendationReasoning}");
            }

            // Get URL-specific configuration
            var config = LoginPageConfigurations.GetConfigurationForUrl(currentUrl);
            if (config != null)
            {
                _logger.LogInformation($"Using URL-specific configuration: {config.DisplayName}");
            }

            // **OPTIMIZED: Only wait for page ready if fast-path failed**
            await WaitForPageReadyAsync(driver, config);

            // Performance optimization: Batch DOM queries for all elements at once
            await PreloadElementCacheAsync(driver);

            // Log page information for debugging
            LogPageInformation(driver);

            // Detection result with confidence tracking
            LoginFormElements? detectedForm = null;
            DetectionMethod usedMethod = DetectionMethod.UrlSpecific;
            int confidence = 0;
            string attemptId = null;

            // Try detection methods in order based on recommendation
            var methodOrder = GetMethodOrder(recommendedMethod);
            
            foreach (var method in methodOrder)
            {
                try
                {
                    if (_metricsService != null)
                    {
                        attemptId = _metricsService.StartDetectionAttempt(currentUrl, method);
                    }

                    switch (method)
                    {
                        case DetectionMethod.UrlSpecific when config != null:
                            detectedForm = await DetectByConfigurationOptimizedAsync(driver, config);
                            confidence = CalculateConfigurationConfidence(detectedForm, config);
                            break;
                        case DetectionMethod.CommonAttributes:
                            detectedForm = await DetectByCommonAttributesOptimizedAsync(driver);
                            confidence = CalculateCommonAttributesConfidence(detectedForm);
                            break;
                        case DetectionMethod.XPath:
                            detectedForm = await DetectByXPathOptimizedAsync(driver);
                            confidence = CalculateXPathConfidence(detectedForm);
                            break;
                        case DetectionMethod.ShadowDOM:
                            detectedForm = await DetectWithShadowDOMOptimizedAsync(driver);
                            confidence = CalculateShadowDOMConfidence(detectedForm, driver);
                            break;
                    }

                    if (IsValidLoginForm(detectedForm))
                    {
                        usedMethod = method;
                        _logger.LogInformation($"Login form detected using {method} strategy with {confidence}% confidence");
                        LogDetectedElements(detectedForm);
                        
                        // Record successful detection
                        if (_metricsService != null && attemptId != null)
                        {
                            var selectorDetails = ExtractSelectorDetails(detectedForm, driver);
                            _metricsService.RecordSuccess(attemptId, detectedForm, confidence, selectorDetails);
                        }
                        
                        return detectedForm;
                    }
                    else if (_metricsService != null && attemptId != null)
                    {
                        // Record failed attempt
                        _metricsService.RecordFailure(attemptId, $"No valid form elements found with {method} method");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error during {method} detection strategy");
                    if (_metricsService != null && attemptId != null)
                    {
                        _metricsService.RecordFailure(attemptId, $"Exception in {method} method: {ex.Message}");
                    }
                }
            }

            // If all strategies fail, log detailed debug information
            _logger.LogWarning("Unable to detect login form using any strategy");
            await LogDetailedDOMInformation(driver);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting login form");
            return null;
        }
    }

    /// <summary>
    /// **NEW: Ultra-fast detection for common login forms - completes in under 500ms**
    /// This method tries the most common login form patterns immediately without waiting for full page load
    /// </summary>
    private async Task<LoginFormElements?> TryFastPathDetectionAsync(IWebDriver driver)
    {
        try
        {
            _logger.LogDebug("Attempting FAST-PATH login form detection");
            var startTime = DateTime.Now;

            var loginForm = new LoginFormElements();

            // **STRATEGY 1: Look for the most common login patterns immediately**
            // Password field is the most reliable indicator of a login form
            var passwordFields = driver.FindElements(By.CssSelector("input[type='password']"));
            if (passwordFields.Count == 0)
            {
                _logger.LogDebug("FAST-PATH: No password fields found immediately");
                return null;
            }

            // Take the first visible password field
            var passwordField = passwordFields.FirstOrDefault(p => IsElementVisible(p));
            if (passwordField == null)
            {
                _logger.LogDebug("FAST-PATH: No visible password fields found");
                return null;
            }

            loginForm.PasswordField = passwordField;
            _logger.LogDebug($"FAST-PATH: Found password field - ID: {GetAttributeLower(passwordField, "id")}, Name: {GetAttributeLower(passwordField, "name")}");

            // **STRATEGY 2: Find username field near the password field**
            // Look for text/email inputs AND select elements that could be username fields
            var allInputs = driver.FindElements(By.TagName("input"));
            var allSelects = driver.FindElements(By.TagName("select"));
            
            _logger.LogDebug($"FAST-PATH: Found {allInputs.Count} input elements and {allSelects.Count} select elements");
            
            // First try to find input-based username fields
            var usernameInputField = allInputs
                .Where(input => IsElementVisible(input))
                .Where(input => {
                    var inputType = input.GetAttribute("type")?.ToLower();
                    return inputType == "text" || inputType == "email" || string.IsNullOrEmpty(inputType);
                })
                .Where(input => !AreSameElement(input, passwordField)) // Exclude the password field
                .FirstOrDefault();
            
            if (usernameInputField != null)
            {
                _logger.LogDebug($"FAST-PATH: Found input username field - ID: {GetAttributeLower(usernameInputField, "id")}, Name: {GetAttributeLower(usernameInputField, "name")}");
            }
            
            // Then try to find select-based username fields
            var usernameSelectField = allSelects
                .Where(select => IsElementVisible(select))
                .Where(select => IsLikelyUsernameDropdown(select))
                .FirstOrDefault();
            
            if (usernameSelectField != null)
            {
                _logger.LogDebug($"FAST-PATH: Found select username field - ID: {GetAttributeLower(usernameSelectField, "id")}, Name: {GetAttributeLower(usernameSelectField, "name")}");
            }
            
            // Prefer input fields over select fields for fast path (more common), but accept either
            loginForm.UsernameField = usernameInputField ?? usernameSelectField;
            
            if (loginForm.UsernameField != null)
            {
                _logger.LogDebug($"FAST-PATH: Selected username field - Tag: {loginForm.UsernameField.TagName}, ID: {GetAttributeLower(loginForm.UsernameField, "id")}, Name: {GetAttributeLower(loginForm.UsernameField, "name")}");
            }
            else
            {
                _logger.LogDebug("FAST-PATH: No suitable username field found");
            }

            // **STRATEGY 2.5: Find domain field (optional but important for some login forms)**
            // Look for select elements that could be domain fields
            var domainSelectField = allSelects
                .Where(select => IsElementVisible(select))
                .Where(select => !AreSameElement(select, loginForm.UsernameField)) // Exclude already detected username field
                .Where(select => IsLikelyDomainDropdown(select))
                .FirstOrDefault();
            
            if (domainSelectField != null)
            {
                _logger.LogDebug($"FAST-PATH: Found domain field - ID: {GetAttributeLower(domainSelectField, "id")}, Name: {GetAttributeLower(domainSelectField, "name")}");
                loginForm.DomainField = domainSelectField;
            }
            else
            {
                _logger.LogDebug("FAST-PATH: No domain field found");
            }

            // **STRATEGY 3: Find submit button**
            // Look for submit buttons, regular buttons with login text, or submit inputs
            var submitButton = driver.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button"))
                .Where(btn => IsElementVisible(btn))
                .FirstOrDefault(btn => {
                    var text = btn.Text?.ToLower() ?? "";
                    var value = btn.GetAttribute("value")?.ToLower() ?? "";
                    return text.Contains("login") || text.Contains("sign") || text.Contains("submit") ||
                           value.Contains("login") || value.Contains("sign") || value.Contains("submit") ||
                           btn.GetAttribute("type")?.ToLower() == "submit";
                });

            loginForm.SubmitButton = submitButton;
            
            if (submitButton != null)
            {
                _logger.LogDebug($"FAST-PATH: Found submit button - ID: {GetAttributeLower(submitButton, "id")}, Text: {submitButton.Text}");
            }

            var detectionTime = DateTime.Now - startTime;
            _logger.LogDebug($"FAST-PATH detection completed in {detectionTime.TotalMilliseconds}ms");

            // Return result if we found the essential elements (password + username)
            // We need both password and username fields for a valid login form
            if (loginForm.PasswordField != null && loginForm.UsernameField != null)
            {
                _logger.LogInformation($"FAST-PATH detection successful in {detectionTime.TotalMilliseconds}ms - Found username, password{(loginForm.DomainField != null ? ", and domain" : "")} fields");
                return loginForm;
            }

            _logger.LogDebug("FAST-PATH: Essential elements not found (need both password and username), falling back to standard detection");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FAST-PATH detection failed, falling back to standard detection");
            return null;
        }
    }

    /// <summary>
    /// Performance optimization: Intelligent page ready detection instead of fixed delays
    /// </summary>
    private async Task WaitForPageReadyAsync(IWebDriver driver, LoginPageConfiguration config)
    {
        try
        {
            _logger.LogDebug("Starting optimized page ready detection");
            
            // Quick check if basic elements are already available
            var existingInputs = driver.FindElements(By.TagName("input"));
            if (existingInputs.Count > 0)
            {
                _logger.LogDebug($"Found {existingInputs.Count} input elements immediately, skipping extended wait");
                return;
            }

            // Reduced timeout for document ready state - most pages load quickly
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(3)); // Reduced from 10s
            
            try
            {
                wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
            }
            catch (WebDriverTimeoutException)
            {
                _logger.LogDebug("Document ready state timeout - continuing with detection");
            }

            // Quick check for minimum page elements - don't wait if they exist
            try
            {
                var quickWait = new WebDriverWait(driver, TimeSpan.FromSeconds(1)); // Very short wait
                quickWait.Until(d => 
                {
                    var inputs = d.FindElements(By.TagName("input"));
                    return inputs.Count > 0;
                });
                _logger.LogDebug("Input elements detected quickly");
            }
            catch (WebDriverTimeoutException)
            {
                _logger.LogDebug("No input elements found in quick check - proceeding anyway");
            }

            // Only add minimal wait if explicitly configured for complex AJAX pages
            if (config?.AdditionalWaitMs > 0)
            {
                var maxWait = Math.Min(config.AdditionalWaitMs, 2000); // Cap at 2 seconds max
                _logger.LogDebug($"Adding configured wait time: {maxWait}ms (capped from {config.AdditionalWaitMs}ms)");
                await Task.Delay(maxWait);
            }
            else
            {
                // Minimal adaptive wait - reduced from 300ms to 100ms
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in optimized page ready detection, using minimal fallback");
            await Task.Delay(200); // Reduced fallback delay from 1000ms to 200ms
        }
    }

    /// <summary>
    /// Performance optimization: Batch DOM queries and cache results
    /// </summary>
    private async Task PreloadElementCacheAsync(IWebDriver driver)
    {
        try
        {
            _logger.LogDebug("Preloading element cache for optimized detection");

            // Single batched query for all element types we might need
            var allInputs = driver.FindElements(By.TagName("input")).ToList();
            var allButtons = driver.FindElements(By.TagName("button")).ToList();
            var allSelects = driver.FindElements(By.TagName("select")).ToList();
            var allLinks = driver.FindElements(By.TagName("a")).ToList();
            
            // Cache elements by type
            _elementCache["inputs"] = allInputs;
            _elementCache["buttons"] = allButtons;
            _elementCache["selects"] = allSelects;
            _elementCache["links"] = allLinks;
            
            // Cache common element combinations
            _elementCache["formElements"] = allInputs.Concat(allButtons).Concat(allSelects).ToList();
            _elementCache["clickableElements"] = allButtons.Concat(allLinks).Concat(allInputs.Where(i => 
                i.GetAttribute("type")?.ToLower() == "submit" || 
                i.GetAttribute("type")?.ToLower() == "button")).ToList();

            _logger.LogDebug($"Cached {allInputs.Count} inputs, {allButtons.Count} buttons, " +
                           $"{allSelects.Count} selects, {allLinks.Count} links");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error preloading element cache");
        }
    }

    private void LogPageInformation(IWebDriver driver)
    {
        try
        {
            _logger.LogInformation($"Current URL: {driver.Url}");
            _logger.LogInformation($"Page title: {driver.Title}");
            
            // Count input elements
            var inputElements = driver.FindElements(By.TagName("input"));
            _logger.LogInformation($"Total input elements found: {inputElements.Count}");
            
            var passwordFields = driver.FindElements(By.CssSelector("input[type='password']"));
            _logger.LogInformation($"Password fields found: {passwordFields.Count}");
            
            var textFields = driver.FindElements(By.CssSelector("input[type='text']"));
            _logger.LogInformation($"Text fields found: {textFields.Count}");
            
            var buttons = driver.FindElements(By.TagName("button"));
            _logger.LogInformation($"Button elements found: {buttons.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not log page information");
        }
    }

    private void LogDetectedElements(LoginFormElements loginForm)
    {
        try
        {
            _logger.LogInformation("Successfully detected login form elements:");
            
            if (loginForm.UsernameField != null)
            {
                _logger.LogInformation($"Username field - Tag: {loginForm.UsernameField.TagName}, ID: {loginForm.UsernameField.GetAttribute("id")}, Name: {loginForm.UsernameField.GetAttribute("name")}");
            }
            
            if (loginForm.PasswordField != null)
            {
                _logger.LogInformation($"Password field - Tag: {loginForm.PasswordField.TagName}, ID: {loginForm.PasswordField.GetAttribute("id")}, Name: {loginForm.PasswordField.GetAttribute("name")}");
            }
            
            if (loginForm.DomainField != null)
            {
                _logger.LogInformation($"Domain field - Tag: {loginForm.DomainField.TagName}, ID: {loginForm.DomainField.GetAttribute("id")}, Name: {loginForm.DomainField.GetAttribute("name")}");
            }
            
            if (loginForm.SubmitButton != null)
            {
                _logger.LogInformation($"Submit button - Tag: {loginForm.SubmitButton.TagName}, ID: {loginForm.SubmitButton.GetAttribute("id")}, Name: {loginForm.SubmitButton.GetAttribute("name")}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not log detected elements information");
        }
    }

    private async Task LogDetailedDOMInformation(IWebDriver driver)
    {
        try
        {
            _logger.LogWarning("=== DETAILED DOM ANALYSIS FOR DEBUGGING ===");
            
            // Log all input elements with their attributes
            var allInputs = driver.FindElements(By.TagName("input"));
            _logger.LogWarning($"All input elements ({allInputs.Count}):");
            
            for (int i = 0; i < Math.Min(allInputs.Count, 10); i++) // Limit to first 10 to avoid spam
            {
                var input = allInputs[i];
                try
                {
                    var type = input.GetAttribute("type") ?? "not-set";
                    var id = input.GetAttribute("id") ?? "not-set";
                    var name = input.GetAttribute("name") ?? "not-set";
                    var placeholder = input.GetAttribute("placeholder") ?? "not-set";
                    var className = input.GetAttribute("class") ?? "not-set";
                    var displayed = input.Displayed;
                    
                    _logger.LogWarning($"Input {i+1}: Type={type}, ID={id}, Name={name}, Placeholder={placeholder}, Class={className}, Displayed={displayed}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error reading input {i+1}: {ex.Message}");
                }
            }
            
            // Log all button elements
            var allButtons = driver.FindElements(By.TagName("button"));
            _logger.LogWarning($"All button elements ({allButtons.Count}):");
            
            for (int i = 0; i < Math.Min(allButtons.Count, 5); i++) // Limit to first 5
            {
                var button = allButtons[i];
                try
                {
                    var id = button.GetAttribute("id") ?? "not-set";
                    var name = button.GetAttribute("name") ?? "not-set";
                    var type = button.GetAttribute("type") ?? "not-set";
                    var className = button.GetAttribute("class") ?? "not-set";
                    var text = button.Text?.Trim() ?? "not-set";
                    var displayed = button.Displayed;
                    
                    _logger.LogWarning($"Button {i+1}: Type={type}, ID={id}, Name={name}, Class={className}, Text={text}, Displayed={displayed}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error reading button {i+1}: {ex.Message}");
                }
            }
            
            // Log forms
            var forms = driver.FindElements(By.TagName("form"));
            _logger.LogWarning($"Forms found: {forms.Count}");
            
            _logger.LogWarning("=== END DOM ANALYSIS ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during detailed DOM logging");
        }
    }

    private LoginFormElements DetectByCommonAttributes(IWebDriver driver)
    {
        _logger.LogDebug("Trying to detect login form using enhanced common attributes detection with scoring");
        
        var loginForm = new LoginFormElements();

        try
        {
            // Enhanced detection with scoring system for better element selection and Shadow DOM support
            loginForm.UsernameField = FindBestElementByScoreWithShadowDOM(driver, ElementType.Username);
            loginForm.PasswordField = FindBestElementByScoreWithShadowDOM(driver, ElementType.Password);
            
            // Pass already detected fields to prevent overlap
            var alreadyDetectedFields = new List<IWebElement?> { loginForm.UsernameField, loginForm.PasswordField };
            loginForm.DomainField = FindBestElementByScoreWithShadowDOM(driver, ElementType.Domain, alreadyDetectedFields);
            
            loginForm.SubmitButton = FindBestElementByScoreWithShadowDOM(driver, ElementType.SubmitButton);

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in enhanced common attributes detection strategy");
            return new LoginFormElements();
        }
    }

    private IWebElement? FindBestElementByScore(IWebDriver driver, ElementType elementType, List<IWebElement?>? excludeElements = null)
    {
        var candidates = new Dictionary<IWebElement, int>();
        
        try
        {
            // Get all relevant elements for scoring
            var allInputs = driver.FindElements(By.TagName("input")).ToList();
            var allButtons = driver.FindElements(By.TagName("button")).ToList();
            var allSelects = driver.FindElements(By.TagName("select")).ToList();
            var allLinks = driver.FindElements(By.TagName("a")).ToList();

            switch (elementType)
            {
                case ElementType.Username:
                    ScoreUsernameElements(allInputs.Concat(allSelects).ToList(), candidates);
                    break;
                case ElementType.Password:
                    ScorePasswordElements(allInputs, candidates);
                    break;
                case ElementType.Domain:
                    ScoreDomainElements(allInputs.Concat(allSelects).ToList(), candidates, excludeElements);
                    break;
                case ElementType.SubmitButton:
                    ScoreSubmitElements(allButtons.Concat(allInputs).Concat(allLinks).ToList(), candidates);
                    break;
            }

            // Return the highest scoring visible element that's not in the exclusion list
            var bestCandidate = candidates
                .Where(kvp => IsElementVisible(kvp.Key))
                .Where(kvp => excludeElements?.Contains(kvp.Key) != true) // Exclude already detected elements
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault();

            if (bestCandidate.Key != null && bestCandidate.Value > 0)
            {
                _logger.LogDebug($"Found {elementType} element with score {bestCandidate.Value}: " +
                               $"Tag={bestCandidate.Key.TagName}, ID={bestCandidate.Key.GetAttribute("id")}, " +
                               $"Name={bestCandidate.Key.GetAttribute("name")}");
                return bestCandidate.Key;
            }

            _logger.LogDebug($"No suitable {elementType} element found with scoring system");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in {elementType} detection using scoring system");
            return null;
        }
    }

    private void ScoreUsernameElements(List<IWebElement> elements, Dictionary<IWebElement, int> candidates, List<IWebElement?>? excludeElements = null)
    {
        var usernameTerms = GetVariationsForElementType(ElementType.Username).SelectMany(kv => kv.Value.Concat(new[] { kv.Key })).Distinct().ToArray();

        foreach (var element in elements)
        {
            try // Outer try for the element processing
            {
                if (!IsElementVisible(element)) continue;

                bool isExcluded = false;
                if (excludeElements != null)
                {
                    foreach (var excludedElement in excludeElements)
                    {
                        if (AreSameElement(element, excludedElement))
                        {
                            isExcluded = true;
                            break;
                        }
                    }
                }
                if (isExcluded)
                {
                    _logger.LogDebug($"Skipping element (ID={GetAttributeLower(element, "id")}, Name={GetAttributeLower(element, "name")}) for Username as it's in excludeElements.");
                    continue;
                }

                int score = 0;
                try // Inner try for the actual scoring attempt
                {
                    int idScore = 0;
                    int nameScore = 0;
                    int placeholderScore = 0;
                    int ariaScore = 0;
                    int dataTestScore = 0;
                    int classScore = 0;

                    var tagName = element.TagName?.ToLower();
                    var type = GetAttributeLower(element, "type");
                    var id = GetAttributeLower(element, "id");
                    var name = GetAttributeLower(element, "name");
                    var placeholder = GetAttributeLower(element, "placeholder");
                    var ariaLabel = GetAttributeLower(element, "aria-label");
                    var className = GetAttributeLower(element, "class");
                    var dataTestId = GetAttributeLower(element, "data-testid");

                    // Define target terms for username fields
                    var currentUsernameTerms = new[] { "username", "user", "login", "email", "account", "userid" }; // Renamed to avoid conflict if usernameTerms is class member

                    // Enhanced fuzzy matching for each attribute
                    idScore = ScoreAttributeWithFuzzyMatching(id, currentUsernameTerms, ElementType.Username);
                    nameScore = ScoreAttributeWithFuzzyMatching(name, currentUsernameTerms, ElementType.Username);
                    placeholderScore = ScoreAttributeWithFuzzyMatching(placeholder, currentUsernameTerms, ElementType.Username);
                    ariaScore = ScoreAttributeWithFuzzyMatching(ariaLabel, currentUsernameTerms, ElementType.Username);
                    dataTestScore = ScoreAttributeWithFuzzyMatching(dataTestId, currentUsernameTerms, ElementType.Username);
                    classScore = ScoreAttributeWithFuzzyMatching(className, currentUsernameTerms, ElementType.Username);

                    // Apply weighted scoring based on attribute reliability
                    score += (int)(idScore * 0.9);      // ID is most reliable
                    score += (int)(nameScore * 0.85);   // Name is very reliable
                    score += (int)(placeholderScore * 0.6); // Placeholder is moderately reliable
                    score += (int)(ariaScore * 0.7);    // ARIA labels are fairly reliable
                    score += (int)(dataTestScore * 0.8); // Data test attributes are reliable
                    score += (int)(classScore * 0.4);   // Class names are least reliable

                    // Element type validation with fuzzy matching
                    if (tagName == "select")
                    {
                        // Special scoring for select elements (dropdowns)
                        score += 25; // Base score for select elements
                        
                        // Analyze dropdown options for username-like content
                        try
                        {
                            var options = element.FindElements(By.TagName("option"));
                            if (options.Count > 0 && options.Count <= 20) // Reasonable option count
                            {
                                score += 10; // Bonus for reasonable option count
                                
                                // Check if options contain user-like values
                                var optionValues = options.Select(opt => opt.GetAttribute("value")?.ToLower() ?? "").ToList();
                                var optionTexts = options.Select(opt => opt.Text?.ToLower() ?? "").ToList();
                                
                                foreach (var value in optionValues.Concat(optionTexts))
                                {
                                    if (!string.IsNullOrEmpty(value) && 
                                        (value.Contains("user") || value.Contains("admin") || 
                                         (value.Length >= 3 && value.Length <= 20 && !value.Contains("select"))))
                                    {
                                        score += 5; // Bonus for user-like option values
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug($"Error analyzing select options: {ex.Message}");
                        }
                    }
                    else if (tagName == "input")
                    {
                        // Input type validation
                        if (type == "email") score += 45; // Email type is highly indicative
                        else if (type == "text") score += 10; // Text type is acceptable
                        else if (type == "password") score -= 50; // Strong penalty for password fields
                    }
                    else
                    {
                        // Other element types are unlikely to be username fields
                        score -= 20;
                    }

                    // Additional contextual scoring
                    
                    // Multi-word fuzzy matching for complex placeholders/labels
                    if (!string.IsNullOrEmpty(placeholder))
                    {
                        var placeholderWords = placeholder.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in placeholderWords)
                        {
                            int wordScore = ScoreAttributeWithFuzzyMatching(word, currentUsernameTerms, ElementType.Username);
                            if (wordScore > 50) score += (int)(wordScore * 0.3); // Bonus for fuzzy word matches
                        }
                    }

                    if (!string.IsNullOrEmpty(ariaLabel))
                    {
                        var ariaWords = ariaLabel.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in ariaWords)
                        {
                            int wordScore = ScoreAttributeWithFuzzyMatching(word, currentUsernameTerms, ElementType.Username);
                            if (wordScore > 50) score += (int)(wordScore * 0.35); // Bonus for fuzzy word matches
                        }
                    }

                    // Form position bonus (username is typically first input)
                    var formInputs = GetFormInputs(element);
                    if (formInputs.Count > 0 && AreSameElement(formInputs[0], element)) score += 15; // Used AreSameElement

                    // Visibility bonus
                    if (IsElementVisible(element)) score += 5;

                    // Advanced pattern matching for common username field patterns
                    var allAttributes = $"{id} {name} {placeholder} {ariaLabel} {className} {dataTestId}".ToLower();
                    
                    // Email pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(email|e-?mail|mail)\\b")) score += 25;
                    
                    // Login pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(login|log-?in|signin|sign-?in)\\b")) score += 20;
                    
                    // User pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(user|username|userid|user-?id)\\b")) score += 20;
                    
                    // Account pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(account|acct)\\b")) score += 15;

                    // Penalty for common non-username patterns
                    if (Regex.IsMatch(allAttributes, @"\\b(password|pass|pwd|confirm|repeat|verify)\\b")) score -= 30;
                    if (Regex.IsMatch(allAttributes, @"\\b(search|query|filter)\\b")) score -= 20;

                    // Only consider elements with positive scores
                    if (score > 0)
                    {
                        candidates[element] = score;
                        _logger.LogDebug($"Username candidate scored {score}: ID={id}, Name={name}, TagName={tagName}, Type={type}");
                    }
                }
                catch (Exception ex_inner) // Inner catch for scoring logic
                {
                    _logger.LogDebug($"Error scoring username element: {ex_inner.Message}");
                }
                // Inner try-catch block ends here
            } // Outer try for element processing ends here.
            catch (Exception ex_outer) // Catch for the outer try (processing the element)
            {
                _logger.LogDebug(ex_outer, $"Error processing an element in ScoreUsernameElements loop for element: ID={GetAttributeLower(element, "id")}");
            }
        } // End of foreach loop
    }

    private void ScorePasswordElements(List<IWebElement> inputs, Dictionary<IWebElement, int> candidates, List<IWebElement?>? excludeElements = null)
    {
        var passwordTerms = GetVariationsForElementType(ElementType.Password).SelectMany(kv => kv.Value.Concat(new[] { kv.Key })).Distinct().ToArray();

        foreach (var input in inputs)
        {
            try // Outer try for the 'input' element processing
            {
                if (!IsElementVisible(input)) continue;

                bool isExcluded = false;
                if (excludeElements != null)
                {
                    foreach (var excludedElement in excludeElements)
                    {
                        if (AreSameElement(input, excludedElement))
                        {
                            isExcluded = true;
                            break;
                        }
                    }
                }
                if (isExcluded)
                {
                    _logger.LogDebug($"Skipping element (ID={GetAttributeLower(input, "id")}, Name={GetAttributeLower(input, "name")}) for Password as it's in excludeElements.");
                    continue;
                }
                
                int score = 0;
                try // Inner try for the actual scoring attempt
                {
                    int idScore = 0;
                    int nameScore = 0;
                    int placeholderScore = 0;
                    int ariaScore = 0;
                    int dataTestScore = 0;
                    int classScore = 0;

                    var type = GetAttributeLower(input, "type");
                    var id = GetAttributeLower(input, "id");
                    var name = GetAttributeLower(input, "name");
                    var placeholder = GetAttributeLower(input, "placeholder");
                    var ariaLabel = GetAttributeLower(input, "aria-label");
                    var className = GetAttributeLower(input, "class");
                    var dataTestId = GetAttributeLower(input, "data-testid");

                    // Define target terms for password fields
                    var currentPasswordTerms = new[] { "password", "pass", "pwd", "passwd", "passphrase", "pin", "secret" }; // Renamed

                    // Enhanced fuzzy matching for each attribute
                    idScore = ScoreAttributeWithFuzzyMatching(id, currentPasswordTerms, ElementType.Password);
                    nameScore = ScoreAttributeWithFuzzyMatching(name, currentPasswordTerms, ElementType.Password);
                    placeholderScore = ScoreAttributeWithFuzzyMatching(placeholder, currentPasswordTerms, ElementType.Password);
                    ariaScore = ScoreAttributeWithFuzzyMatching(ariaLabel, currentPasswordTerms, ElementType.Password);
                    dataTestScore = ScoreAttributeWithFuzzyMatching(dataTestId, currentPasswordTerms, ElementType.Password);
                    classScore = ScoreAttributeWithFuzzyMatching(className, currentPasswordTerms, ElementType.Password);

                    // Apply weighted scoring based on attribute reliability
                    score += (int)(idScore * 0.9);      // ID is most reliable
                    score += (int)(nameScore * 0.85);   // Name is very reliable
                    score += (int)(placeholderScore * 0.6); // Placeholder is moderately reliable
                    score += (int)(ariaScore * 0.7);    // ARIA labels are fairly reliable
                    score += (int)(dataTestScore * 0.8); // Data test attributes are reliable
                    score += (int)(classScore * 0.4);   // Class names are least reliable

                    // Input type validation - password type is highly indicative
                    if (type == "password") score += 50; // Password type is the strongest indicator
                    else if (type == "text") score += 5; // Text type is possible but less likely
                    else if (type != "text" && type != "password") score -= 30; // Other types are unlikely

                    // Multi-word fuzzy matching for complex placeholders/labels
                    if (!string.IsNullOrEmpty(placeholder))
                    {
                        var placeholderWords = placeholder.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in placeholderWords)
                        {
                            int wordScore = ScoreAttributeWithFuzzyMatching(word, currentPasswordTerms, ElementType.Password);
                            if (wordScore > 50) score += (int)(wordScore * 0.3); // Bonus for fuzzy word matches
                        }
                    }

                    if (!string.IsNullOrEmpty(ariaLabel))
                    {
                        var ariaWords = ariaLabel.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in ariaWords)
                        {
                            int wordScore = ScoreAttributeWithFuzzyMatching(word, currentPasswordTerms, ElementType.Password);
                            if (wordScore > 50) score += (int)(wordScore * 0.35); // Bonus for fuzzy word matches
                        }
                    }

                    // Advanced pattern matching for password field patterns
                    var allAttributes = $"{id} {name} {placeholder} {ariaLabel} {className} {dataTestId}".ToLower();
                    
                    // Password pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(password|pass-?word)\\b")) score += 30;
                    
                    // Common password abbreviations
                    if (Regex.IsMatch(allAttributes, @"\\b(pass|pwd|passwd)\\b")) score += 25;
                    
                    // Security-related terms
                    if (Regex.IsMatch(allAttributes, @"\\b(secret|pin|passphrase|auth)\\b")) score += 20;
                    
                    // Confirmation password patterns (should have lower score than main password)
                    if (Regex.IsMatch(allAttributes, @"\\b(confirm|verify|repeat|again|2nd|second)\\b")) 
                    {
                        score += 15; // Still a password field, but likely confirmation
                        _logger.LogDebug($"Detected potential confirmation password field: {id}/{name}");
                    }

                    // Penalty for non-password patterns
                    if (Regex.IsMatch(allAttributes, @"\\b(username|user|email|login|account)\\b")) score -= 40;
                    if (Regex.IsMatch(allAttributes, @"\\b(search|query|filter|domain|tenant)\\b")) score -= 25;

                    // Form position consideration (password is typically second input)
                    var formInputs = GetFormInputs(input);
                    if (formInputs.Count >= 2 && AreSameElement(formInputs[1], input)) score += 10; // Used AreSameElement

                    // Visibility bonus
                    if (IsElementVisible(input)) score += 5;

                    // Penalty for elements that don't have password-like attributes at all
                    if (type != "password" && idScore == 0 && nameScore == 0 && placeholderScore == 0 && ariaScore == 0)
                    {
                        score -= 20; // Likely not a password field
                    }

                    // Only consider elements with positive scores
                    if (score > 0)
                    {
                        candidates[input] = score;
                        _logger.LogDebug($"Password candidate scored {score}: ID={id}, Name={name}, Type={type}");
                    }
                }
                catch (Exception ex_inner) // Inner catch for scoring logic
                {
                    _logger.LogDebug($"Error scoring password element: {ex_inner.Message}");
                }
                // Inner try-catch block ends here
            } // Outer try for 'input' processing ends here.
            catch (Exception ex_outer) // Catch for the outer try (processing the element 'input')
            {
                _logger.LogDebug(ex_outer, $"Error processing an element in ScorePasswordElements loop for input: ID={GetAttributeLower(input, "id")}");
            }
        } // End of foreach loop
    }

    private void ScoreDomainElements(List<IWebElement> elements, Dictionary<IWebElement, int> candidates, List<IWebElement?>? excludeElements = null)
    {
        var domainTerms = GetVariationsForElementType(ElementType.Domain).SelectMany(kv => kv.Value.Concat(new[] { kv.Key })).Distinct().ToArray();

        foreach (var element in elements)
        {
            try // Outer try for the 'element' processing
            {
                if (!IsElementVisible(element)) continue;
            
                bool isExcluded = false;
                if (excludeElements != null)
                {
                    foreach (var excludedElement in excludeElements)
                    {
                        if (AreSameElement(element, excludedElement))
                        {
                            isExcluded = true;
                            break;
                        }
                    }
                }
                if (isExcluded)
                {
                    _logger.LogDebug($"Skipping element (ID={GetAttributeLower(element, "id")}, Name={GetAttributeLower(element, "name")}) for Domain as it's in excludeElements.");
                    continue;
                }

                int score = 0;
                try // Inner try for the actual scoring attempt
                {
                    int idScore = 0;
                    int nameScore = 0;
                    int placeholderScore = 0;
                    int ariaScore = 0;
                    int dataTestScore = 0;
                    int classScore = 0;

                    var tagName = element.TagName.ToLower();
                    var type = GetAttributeLower(element, "type");
                    var id = GetAttributeLower(element, "id");
                    var name = GetAttributeLower(element, "name");
                    var placeholder = GetAttributeLower(element, "placeholder");
                    var ariaLabel = GetAttributeLower(element, "aria-label");
                    var className = GetAttributeLower(element, "class");
                    var dataTestId = GetAttributeLower(element, "data-testid");

                    // Define target terms for domain fields
                    var currentDomainTerms = new[] { "domain", "tenant", "organization", "org", "company", "realm", "authority" }; // Renamed

                    // Enhanced fuzzy matching for each attribute
                    idScore = ScoreAttributeWithFuzzyMatching(id, currentDomainTerms, ElementType.Domain);
                    nameScore = ScoreAttributeWithFuzzyMatching(name, currentDomainTerms, ElementType.Domain);
                    placeholderScore = ScoreAttributeWithFuzzyMatching(placeholder, currentDomainTerms, ElementType.Domain);
                    ariaScore = ScoreAttributeWithFuzzyMatching(ariaLabel, currentDomainTerms, ElementType.Domain);
                    dataTestScore = ScoreAttributeWithFuzzyMatching(dataTestId, currentDomainTerms, ElementType.Domain);
                    classScore = ScoreAttributeWithFuzzyMatching(className, currentDomainTerms, ElementType.Domain);

                    // Apply weighted scoring based on attribute reliability
                    score += (int)(idScore * 0.9);      // ID is most reliable
                    score += (int)(nameScore * 0.85);   // Name is very reliable
                    score += (int)(placeholderScore * 0.6); // Placeholder is moderately reliable
                    score += (int)(ariaScore * 0.7);    // ARIA labels are fairly reliable
                    score += (int)(dataTestScore * 0.8); // Data test attributes are reliable
                    score += (int)(classScore * 0.4);   // Class names are least reliable

                    // Element type bonuses
                    if (tagName == "select") 
                    {
                        score += 15; // Domain fields are often dropdowns
                        
                        // Additional bonus for required domain dropdowns (common pattern)
                        var required = element.GetAttribute("required");
                        if (!string.IsNullOrEmpty(required)) score += 10;
                    }
                    if (tagName == "input" && (type == "text" || type == "")) score += 5;

                    // Multi-word fuzzy matching for complex placeholders/labels
                    if (!string.IsNullOrEmpty(placeholder))
                    {
                        var placeholderWords = placeholder.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in placeholderWords)
                        {
                            int wordScore = ScoreAttributeWithFuzzyMatching(word, currentDomainTerms, ElementType.Domain);
                            if (wordScore > 50) score += (int)(wordScore * 0.3); // Bonus for fuzzy word matches
                        }
                    }

                    if (!string.IsNullOrEmpty(ariaLabel))
                    {
                        var ariaWords = ariaLabel.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in ariaWords)
                        {
                            int wordScore = ScoreAttributeWithFuzzyMatching(word, currentDomainTerms, ElementType.Domain);
                            if (wordScore > 50) score += (int)(wordScore * 0.35); // Bonus for fuzzy word matches
                        }
                    }

                    // Advanced pattern matching for domain field patterns
                    var allAttributes = $"{id} {name} {placeholder} {ariaLabel} {className} {dataTestId}".ToLower();
                    
                    // Exact match bonus for "domain" (highest priority for our target case)
                    if (id == "domain" || name == "domain") score += 50;
                    
                    // Domain pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(domain|tenant)\\b")) score += 30;
                    
                    // Organization pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(organization|organisation|org)\\b")) score += 25;
                    
                    // Company pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(company|corp|corporation)\\b")) score += 20;
                    
                    // Enterprise/realm patterns
                    if (Regex.IsMatch(allAttributes, @"\\b(realm|authority|enterprise|workspace)\\b")) score += 20;

                    // STRONG penalty for non-domain patterns - prevent username/password field reuse
                    if (Regex.IsMatch(allAttributes, @"\\b(username|user|password|pass|email|login)\\b")) score -= 100; // Increased penalty
                    if (Regex.IsMatch(allAttributes, @"\\b(search|query|filter|submit)\\b")) score -= 50; // Increased penalty

                    // Additional validation: Require at least one domain-specific term to have a positive score
                    bool hasDomainSpecificTerm = Regex.IsMatch(allAttributes, @"\\b(domain|tenant|organization|organisation|org|company|corp|corporation|realm|authority|enterprise|workspace)\\b");
                    if (!hasDomainSpecificTerm)
                    {
                        // If no domain-specific terms found, apply heavy penalty to prevent false positives
                        score -= 80;
                        _logger.LogDebug($"No domain-specific terms found in element: ID={id}, Name={name} - applying heavy penalty");
                    }

                    // Form position bonus (domain is often third field)
                    var formInputs = GetFormInputs(element);
                    if (formInputs.Count >= 3 && AreSameElement(formInputs[2], element)) score += 10; // Used AreSameElement

                    // Visibility bonus
                    if (IsElementVisible(element)) score += 5;

                    // Enhanced handling for select elements with domain-like options
                    if (tagName == "select")
                    {
                        try
                        {
                            var options = element.FindElements(By.TagName("option"));
                            if (options.Count > 1) // Must have multiple options
                            {
                                var optionValues = options.Select(o => o.GetAttribute("value")?.ToLower() ?? "").ToList();
                                var optionTexts = options.Select(o => o.Text?.ToLower() ?? "").ToList();
                                var allOptionContent = optionValues.Concat(optionTexts).ToList();
                                
                                // Enhanced domain option detection
                                bool hasDomainOptions = false;
                                int domainOptionBonus = 0;
                                
                                foreach (var content in allOptionContent)
                                {
                                    if (string.IsNullOrEmpty(content)) continue;
                                    
                                    // High-value domain patterns
                                    if (content.Contains(".local")) { hasDomainOptions = true; domainOptionBonus += 40; }
                                    else if (content.Contains(".com") || content.Contains(".org") || content.Contains(".net")) { hasDomainOptions = true; domainOptionBonus += 35; }
                                    else if (Regex.IsMatch(content, @"\\b(domain|tenant|org|company|corp)\\b")) { hasDomainOptions = true; domainOptionBonus += 30; }
                                    // Medium-value patterns (like "masko.local", "picovina")
                                    else if (content.Length > 3 && content.Length < 50 && 
                                            !content.Contains("select") && !content.Contains("choose") && 
                                            !content.Contains("--") && !content.Contains("option"))
                                    {
                                        hasDomainOptions = true; 
                                        domainOptionBonus += 20;
                                    }
                                }
                                
                                if (hasDomainOptions) 
                                {
                                    score += Math.Min(domainOptionBonus, 60); // Cap the bonus to prevent over-scoring
                                    _logger.LogDebug($"Domain select element scored {domainOptionBonus} bonus points for domain-like options");
                                }
                                
                                // Additional bonus for reasonable option count (typical domain dropdowns)
                                if (options.Count >= 2 && options.Count <= 20)
                                {
                                    score += 10;
                                }
                            }
                        }
                        catch (Exception ex_select)
                        {
                            _logger.LogDebug($"Error analyzing select options for domain field: {ex_select.Message}");
                        }
                    }

                    // Only consider elements with positive scores AND domain-specific attributes
                    if (score > 0 && hasDomainSpecificTerm)
                    {
                        candidates[element] = score;
                        _logger.LogDebug($"Domain candidate scored {score}: ID={id}, Name={name}, TagName={tagName}");
                    }
                    else if (score <= 0)
                    {
                        _logger.LogDebug($"Domain candidate rejected (score {score}): ID={id}, Name={name}, TagName={tagName}");
                    }
                }
                catch (Exception ex_inner) // Inner catch for scoring logic
                {
                    _logger.LogDebug($"Error scoring domain element: {ex_inner.Message}");
                }
                // Inner try-catch block ends here
            } // Outer try for 'element' processing ends here.
            catch (Exception ex_outer) // Catch for the outer try (processing the element 'element')
            {
                _logger.LogDebug(ex_outer, $"Error processing an element in ScoreDomainElements loop for element: ID={GetAttributeLower(element, "id")}");
            }
        } // End of foreach loop
    }

    private void ScoreSubmitElements(List<IWebElement> elements, Dictionary<IWebElement, int> candidates, List<IWebElement?>? excludeElements = null)
    {
        var submitTerms = GetVariationsForElementType(ElementType.SubmitButton).SelectMany(kv => kv.Value.Concat(new[] { kv.Key })).Distinct().ToArray();

        foreach (var element in elements)
        {
            try // Outer try for the 'element' processing
            {
                if (!IsElementVisible(element)) continue;

                bool isExcluded = false;
                if (excludeElements != null)
                {
                    foreach (var excludedElement in excludeElements)
                    {
                        if (AreSameElement(element, excludedElement))
                        {
                            isExcluded = true;
                            break;
                        }
                    }
                }
                if (isExcluded)
                {
                    _logger.LogDebug($"Skipping element (ID={GetAttributeLower(element, "id")}, Name={GetAttributeLower(element, "name")}) for SubmitButton as it's in excludeElements.");
                    continue;
                }

                int score = 0;
                try // Inner try for the actual scoring attempt
                {
                    int idScore = 0;
                    int nameScore = 0;
                    int valueScore = 0;
                    int textScore = 0;
                    int ariaScore = 0;
                    int dataTestScore = 0;
                    int classScore = 0;

                    var tagName = element.TagName.ToLower();
                    var type = GetAttributeLower(element, "type");
                    var id = GetAttributeLower(element, "id");
                    var name = GetAttributeLower(element, "name");
                    var value = GetAttributeLower(element, "value");
                    var text = element.Text?.ToLower() ?? "";
                    var ariaLabel = GetAttributeLower(element, "aria-label");
                    var className = GetAttributeLower(element, "class");
                    var dataTestId = GetAttributeLower(element, "data-testid");

                    // Define target terms for submit buttons
                    var currentSubmitTerms = new[] { "login", "submit", "signin", "sign-in", "enter", "go", "connect", "continue" }; // Renamed

                    // Enhanced fuzzy matching for each attribute
                    idScore = ScoreAttributeWithFuzzyMatching(id, currentSubmitTerms, ElementType.SubmitButton);
                    nameScore = ScoreAttributeWithFuzzyMatching(name, currentSubmitTerms, ElementType.SubmitButton);
                    valueScore = ScoreAttributeWithFuzzyMatching(value, currentSubmitTerms, ElementType.SubmitButton);
                    textScore = ScoreAttributeWithFuzzyMatching(text, currentSubmitTerms, ElementType.SubmitButton);
                    ariaScore = ScoreAttributeWithFuzzyMatching(ariaLabel, currentSubmitTerms, ElementType.SubmitButton);
                    dataTestScore = ScoreAttributeWithFuzzyMatching(dataTestId, currentSubmitTerms, ElementType.SubmitButton);
                    classScore = ScoreAttributeWithFuzzyMatching(className, currentSubmitTerms, ElementType.SubmitButton);

                    // Apply weighted scoring based on attribute reliability for submit buttons
                    score += (int)(idScore * 0.8);      // ID is reliable
                    score += (int)(nameScore * 0.8);    // Name is reliable
                    score += (int)(valueScore * 0.9);   // Value is very reliable for buttons
                    score += (int)(textScore * 0.95);   // Visible text is most reliable
                    score += (int)(ariaScore * 0.7);    // ARIA labels are fairly reliable
                    score += (int)(dataTestScore * 0.8); // Data test attributes are reliable
                    score += (int)(classScore * 0.4);   // Class names are least reliable

                    // Element type validation
                    if (type == "submit") score += 50; // Submit type is the strongest indicator
                    else if (tagName == "button") score += 20; // Button elements are likely
                    else if (tagName == "input" && type == "button") score += 15; // Input buttons are possible
                    else if (tagName == "a") score += 5; // Links can be submit mechanisms

                    // Multi-word fuzzy matching for complex text/labels
                    if (!string.IsNullOrEmpty(text))
                    {
                        var textWords = text.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in textWords)
                        {
                            int wordScore = ScoreAttributeWithFuzzyMatching(word, currentSubmitTerms, ElementType.SubmitButton);
                            if (wordScore > 50) score += (int)(wordScore * 0.4); // Bonus for fuzzy word matches in text
                        }
                    }

                    if (!string.IsNullOrEmpty(value))
                    {
                        var valueWords = value.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in valueWords)
                        {
                            int wordScore = ScoreAttributeWithFuzzyMatching(word, currentSubmitTerms, ElementType.SubmitButton);
                            if (wordScore > 50) score += (int)(wordScore * 0.35); // Bonus for fuzzy word matches in value
                        }
                    }

                    // Advanced pattern matching for submit button patterns
                    var allAttributes = $"{id} {name} {value} {text} {ariaLabel} {className} {dataTestId}".ToLower();
                    
                    // Login pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(login|log-?in|signin|sign-?in)\\b")) score += 30;
                    
                    // Submit pattern detection
                    if (Regex.IsMatch(allAttributes, @"\\b(submit|send)\\b")) score += 25;
                    
                    // Action patterns
                    if (Regex.IsMatch(allAttributes, @"\\b(enter|go|continue|proceed|connect)\\b")) score += 20;
                    
                    // Authentication patterns
                    if (Regex.IsMatch(allAttributes, @"\\b(authenticate|auth|access)\\b")) score += 15;

                    // Penalty for non-submit patterns
                    if (Regex.IsMatch(allAttributes, @"\\b(cancel|reset|clear|back|previous)\\b")) score -= 30;
                    if (Regex.IsMatch(allAttributes, @"\\b(register|signup|sign-?up|create|forgot)\\b")) score -= 25;
                    if (Regex.IsMatch(allAttributes, @"\\b(search|filter|sort|edit|delete)\\b")) score -= 20;

                    // Form position bonus (submit is often last element)
                    var formButtons = GetFormButtons(element);
                    if (formButtons.Count > 0 && AreSameElement(formButtons.Last(), element)) score += 15; // Used AreSameElement

                    // Primary button detection (common CSS patterns)
                    if (Regex.IsMatch(className, @"\\b(primary|btn-primary|main|default)\\b")) score += 10;

                    // Visibility requirement - submit buttons must be visible
                    if (IsElementVisible(element)) 
                    {
                        score += 10; // Higher bonus for visibility
                    }
                    else 
                    {
                        score -= 40; // Heavy penalty for hidden submit buttons
                    }

                    // Special handling for links that might be submit mechanisms
                    if (tagName == "a")
                    {
                        var href = element.GetAttribute("href")?.ToLower() ?? "";
                        if (href.Contains("javascript") || href == "#" || string.IsNullOrEmpty(href))
                        {
                            score += 10; // Likely a JavaScript-based submit link
                        }
                        else
                        {
                            score -= 10; // Regular navigation links are less likely to be submit buttons
                        }
                    }

                    // Role attribute consideration
                    var role = GetAttributeLower(element, "role");
                    if (role == "button") score += 10;

                    // Only consider elements with positive scores
                    if (score > 0)
                    {
                        candidates[element] = score;
                        _logger.LogDebug($"Submit candidate scored {score}: ID={id}, Name={name}, TagName={tagName}, Type={type}, Text='{text}'");
                    }
                }
                catch (Exception ex_inner) // Inner catch for scoring logic
                {
                    _logger.LogDebug($"Error scoring submit element: {ex_inner.Message}");
                }
                // Inner try-catch block ends here
            } // Outer try for 'element' processing ends here.
            catch (Exception ex_outer) // Catch for the outer try (processing the element 'element')
            {
                _logger.LogDebug(ex_outer, $"Error processing an element in ScoreSubmitElements loop for element: ID={GetAttributeLower(element, "id")}");
            }
        } // End of foreach loop
    }

    private string GetAttributeLower(IWebElement element, string attributeName)
    {
        try
        {
            return element?.GetAttribute(attributeName)?.ToLower() ?? string.Empty;
        }
        catch (Exception ex) // Catch StaleElementReferenceException or others
        {
            _logger.LogDebug(ex, $"Error getting attribute '{attributeName}' for element. Element might be stale.");
            return string.Empty;
        }
    }

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
    /// Fast heuristic to determine if a select element is likely a username dropdown
    /// Used in fast path detection for performance
    /// </summary>
    private bool IsLikelyUsernameDropdown(IWebElement selectElement)
    {
        try
        {
            var id = GetAttributeLower(selectElement, "id");
            var name = GetAttributeLower(selectElement, "name");
            var className = GetAttributeLower(selectElement, "class");
            
            // Check for username-like attributes
            var usernameTerms = new[] { "username", "user", "login", "account", "userid" };
            var allAttributes = $"{id} {name} {className}";
            
            foreach (var term in usernameTerms)
            {
                if (allAttributes.Contains(term))
                {
                    _logger.LogDebug($"FAST-PATH: Found likely username dropdown with {term} in attributes: ID={id}, Name={name}");
                    return true;
                }
            }
            
            // Quick check of option values for username-like content
            try
            {
                var options = selectElement.FindElements(By.TagName("option"));
                if (options.Count > 1 && options.Count <= 20) // Reasonable range for username dropdowns
                {
                    var optionTexts = options.Take(5).Select(o => o.Text?.ToLower() ?? "").ToList();
                    
                    // Look for user-like option values
                    bool hasUserLikeOptions = optionTexts.Any(text => 
                        text.Contains("user") || text.Contains("admin") || 
                        text.Length > 3 && text.Length < 20 && !text.Contains("select"));
                    
                    if (hasUserLikeOptions)
                    {
                        _logger.LogDebug($"FAST-PATH: Found likely username dropdown with user-like options");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"FAST-PATH: Error checking dropdown options: {ex.Message}");
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"FAST-PATH: Error analyzing select element: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Fast heuristic to determine if a select element is likely a domain dropdown
    /// Used in fast path detection for performance
    /// </summary>
    private bool IsLikelyDomainDropdown(IWebElement selectElement)
    {
        try
        {
            var id = GetAttributeLower(selectElement, "id");
            var name = GetAttributeLower(selectElement, "name");
            var className = GetAttributeLower(selectElement, "class");
            
            // Check for domain-like attributes
            var domainTerms = new[] { "domain", "tenant", "organization", "org", "company", "realm", "authority" };
            var allAttributes = $"{id} {name} {className}";
            
            foreach (var term in domainTerms)
            {
                if (allAttributes.Contains(term))
                {
                    _logger.LogDebug($"FAST-PATH: Found likely domain dropdown with {term} in attributes: ID={id}, Name={name}");
                    return true;
                }
            }
            
            // Quick check of option values for domain-like content
            try
            {
                var options = selectElement.FindElements(By.TagName("option"));
                if (options.Count > 1 && options.Count <= 50) // Reasonable range for domain dropdowns (can be more than usernames)
                {
                    var optionValues = options.Take(10).Select(o => o.GetAttribute("value")?.ToLower() ?? "").ToList();
                    var optionTexts = options.Take(10).Select(o => o.Text?.ToLower() ?? "").ToList();
                    var allOptionContent = optionValues.Concat(optionTexts).ToList();
                    
                    // Look for domain-like option values
                    bool hasDomainLikeOptions = allOptionContent.Any(content => 
                        !string.IsNullOrEmpty(content) && (
                            content.Contains(".local") || 
                            content.Contains(".com") || 
                            content.Contains(".org") || 
                            content.Contains(".net") ||
                            content.Contains("domain") ||
                            content.Contains("tenant") ||
                            // Check for patterns like "masko.local" or "picovina"
                            (content.Length > 3 && content.Length < 50 && 
                             !content.Contains("select") && 
                             !content.Contains("choose") &&
                             !content.Contains("--"))
                        ));
                    
                    if (hasDomainLikeOptions)
                    {
                        _logger.LogDebug($"FAST-PATH: Found likely domain dropdown with domain-like options");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"FAST-PATH: Error checking domain dropdown options: {ex.Message}");
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"FAST-PATH: Error analyzing domain select element: {ex.Message}");
            return false;
        }
    }

    private List<IWebElement> GetFormInputs(IWebElement element)
    {
        try
        {
            // Try to find the parent form and get all inputs
            var form = element.FindElement(By.XPath("./ancestor::form[1]"));
            return form.FindElements(By.TagName("input")).ToList();
        }
        catch
        {
            // If no form parent, just return empty list
            return new List<IWebElement>();
        }
    }

    private List<IWebElement> GetFormButtons(IWebElement element)
    {
        try
        {
            // Try to find the parent form and get all buttons and submit inputs
            var form = element.FindElement(By.XPath("./ancestor::form[1]"));
            var buttons = form.FindElements(By.TagName("button")).ToList();
            var submitInputs = form.FindElements(By.CssSelector("input[type='submit'], input[type='button']")).ToList();
            return buttons.Concat(submitInputs).ToList();
        }
        catch
        {
            // If no form parent, just return empty list
            return new List<IWebElement>();
        }
    }

    public enum ElementType
    {
        Username,
        Password,
        Domain,
        SubmitButton
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

    private LoginFormElements DetectByConfiguration(IWebDriver driver, LoginPageConfiguration config)
    {
        _logger.LogDebug($"Trying to detect login form using configuration: {config.DisplayName}");
        
        var loginForm = new LoginFormElements();

        try
        {
            // Try to find username field using config selectors
            foreach (var selector in config.UsernameSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.UsernameField = elements[0];
                        _logger.LogDebug($"Username field found with selector: {selector}");
                        break;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            // Try to find password field using config selectors
            foreach (var selector in config.PasswordSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.PasswordField = elements[0];
                        _logger.LogDebug($"Password field found with selector: {selector}");
                        break;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            // Try to find domain field using config selectors (optional)
            foreach (var selector in config.DomainSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.DomainField = elements[0];
                        _logger.LogDebug($"Domain field found with selector: {selector}");
                        break;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            // Try to find submit button using config selectors
            foreach (var selector in config.SubmitButtonSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0 && elements[0].Displayed)
                    {
                        loginForm.SubmitButton = elements[0];
                        _logger.LogDebug($"Submit button found with selector: {selector}");
                        break;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in configuration-based detection for: {config.DisplayName}");
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

    #region Fuzzy Matching Algorithms

    /// <summary>
    /// Calculates the Levenshtein distance between two strings for fuzzy matching
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return string.IsNullOrEmpty(s2) ? 0 : s2.Length;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        // Initialize first row and column
        for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;

        // Calculate distances
        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    /// <summary>
    /// Calculates similarity percentage between two strings using Levenshtein distance
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

        int maxLength = Math.Max(s1.Length, s2.Length);
        int distance = LevenshteinDistance(s1.ToLower(), s2.ToLower());
        return 1.0 - (double)distance / maxLength;
    }

    /// <summary>
    /// Simplified Soundex algorithm for phonetic matching
    /// </summary>
    private static string Soundex(string word)
    {
        if (string.IsNullOrEmpty(word)) return "0000";
        
        word = word.ToUpper();
        var soundexChars = new char[4];
        soundexChars[0] = word[0];

        var mappings = new Dictionary<char, char>
        {
            {'B', '1'}, {'F', '1'}, {'P', '1'}, {'V', '1'},
            {'C', '2'}, {'G', '2'}, {'J', '2'}, {'K', '2'}, {'Q', '2'}, {'S', '2'}, {'X', '2'}, {'Z', '2'},
            {'D', '3'}, {'T', '3'},
            {'L', '4'},
            {'M', '5'}, {'N', '5'},
            {'R', '6'}
        };

        int index = 1;
        char previousCode = '\0';

        for (int i = 1; i < word.Length && index < 4; i++)
        {
            if (mappings.TryGetValue(word[i], out char code))
            {
                if (code != previousCode)
                {
                    soundexChars[index++] = code;
                    previousCode = code;
                }
            }
            else
            {
                previousCode = '\0';
            }
        }

        // Fill remaining positions with zeros
        for (int i = index; i < 4; i++)
        {
            soundexChars[i] = '0';
        }

        return new string(soundexChars);
    }

    /// <summary>
    /// Enhanced fuzzy matching that combines multiple techniques
    /// </summary>
    private int CalculateFuzzyScore(string target, string candidate, ElementType elementType)
    {
        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(candidate))
            return 0;

        target = target.ToLower().Trim();
        candidate = candidate.ToLower().Trim();
        
        int score = 0;

        // Exact match gets highest score
        if (target == candidate) return 100;

        // High similarity based on Levenshtein distance
        double similarity = CalculateSimilarity(target, candidate);
        if (similarity >= 0.8) score += (int)(similarity * 50); // Up to 50 points for high similarity

        // Phonetic matching using Soundex
        if (Soundex(target) == Soundex(candidate))
        {
            score += 25; // Bonus for phonetic similarity
        }

        // Check for variations and synonyms
        var variations = GetVariationsForElementType(elementType);
        if (variations.ContainsKey(target))
        {
            foreach (var variation in variations[target])
            {
                if (candidate == variation) score += 40;
                else if (candidate.Contains(variation)) score += 20;
                else if (CalculateSimilarity(candidate, variation) >= 0.7) score += 15;
            }
        }

        // Partial matching with different strategies
        if (candidate.Contains(target)) score += 30;
        if (target.Contains(candidate)) score += 25;
        
        // Word boundary matching (more reliable than simple contains)
        if (Regex.IsMatch(candidate, $@"\b{Regex.Escape(target)}\b")) score += 35;

        // Common prefix/suffix matching
        if (candidate.StartsWith(target) || candidate.EndsWith(target)) score += 20;
        if (target.StartsWith(candidate) || target.EndsWith(candidate)) score += 15;

        // Acronym matching (first letters)
        if (IsAcronymMatch(target, candidate)) score += 10;

        return Math.Min(score, 95); // Cap at 95 to ensure exact matches still rank higher
    }

    /// <summary>
    /// Checks if one string could be an acronym of another
    /// </summary>
    private bool IsAcronymMatch(string full, string acronym)
    {
        if (string.IsNullOrEmpty(full) || string.IsNullOrEmpty(acronym)) return false;
        
        var words = full.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length != acronym.Length) return false;

        return words.Select(w => w[0]).SequenceEqual(acronym.ToLower());
    }

    /// <summary>
    /// Gets variation dictionary for the specified element type
    /// </summary>
    private Dictionary<string, string[]> GetVariationsForElementType(ElementType elementType)
    {
        return elementType switch
        {
            ElementType.Username => UsernameVariations,
            ElementType.Password => PasswordVariations,
            ElementType.Domain => DomainVariations,
            ElementType.SubmitButton => SubmitVariations,
            _ => new Dictionary<string, string[]>()
        };
    }

    /// <summary>
    /// Enhanced attribute scoring that uses fuzzy matching
    /// </summary>
    private int ScoreAttributeWithFuzzyMatching(string attributeValue, string[] targetTerms, ElementType elementType)
    {
        if (string.IsNullOrEmpty(attributeValue)) return 0;

        int maxScore = 0;
        
        foreach (var term in targetTerms)
        {
            int fuzzyScore = CalculateFuzzyScore(term, attributeValue, elementType);
            maxScore = Math.Max(maxScore, fuzzyScore);
        }

        return maxScore;
    }

    #endregion

    #region Shadow DOM Support

    /// <summary>
    /// Enhanced Shadow DOM detection and traversal capabilities
    /// </summary>
    
    /// <summary>
    /// Finds elements within Shadow DOM trees using multiple detection strategies
    /// </summary>
    private IWebElement? FindBestElementByScoreWithShadowDOM(IWebDriver driver, ElementType elementType, List<IWebElement?>? excludeElements = null)
    {
        var allCandidates = new Dictionary<IWebElement, int>();
        
        try
        {
            // First, try regular DOM elements
            var regularElements = FindBestElementByScore(driver, elementType, excludeElements);
            if (regularElements != null)
            {
                return regularElements;
            }

            // If no regular DOM elements found, try Shadow DOM
            var shadowElements = FindElementsInShadowDOM(driver, elementType);
            
            foreach (var element in shadowElements)
            {
                // Skip elements that are in the exclusion list
                if (excludeElements?.Contains(element) == true) continue;
                
                int score = ScoreElementForType(element, elementType);
                
                // Add shadow DOM bonus - these are harder to find so give slight preference
                if (score > 0)
                {
                    score += 500; // Shadow DOM elements get bonus for being found
                    allCandidates[element] = score;
                }
            }

            // Return the highest scoring shadow DOM element
            var bestShadowCandidate = allCandidates
                .Where(kvp => IsElementVisible(kvp.Key))
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault();

            if (bestShadowCandidate.Key != null)
            {
                _logger.LogInformation($"Found {elementType} element in Shadow DOM with score {bestShadowCandidate.Value}");
                return bestShadowCandidate.Key;
            }

            _logger.LogDebug($"No suitable {elementType} element found in Shadow DOM");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in Shadow DOM-aware {elementType} detection");
            return null;
        }
    }

    /// <summary>
    /// Finds all elements of a specific type within Shadow DOM trees
    /// </summary>
    private List<IWebElement> FindElementsInShadowDOM(IWebDriver driver, ElementType elementType)
    {
        var shadowElements = new List<IWebElement>();
        
        try
        {
            // Find all potential shadow hosts in the page
            var shadowHosts = FindPotentialShadowHosts(driver);
            
            foreach (var shadowHost in shadowHosts)
            {
                try
                {
                    var shadowRoot = GetShadowRoot(shadowHost);
                    if (shadowRoot != null)
                    {
                        _logger.LogDebug($"Searching Shadow DOM attached to element: {shadowHost.TagName}#{shadowHost.GetAttribute("id")}");
                        
                        // Search for elements within this shadow root
                        var elements = FindElementsInShadowRoot(shadowRoot, elementType);
                        shadowElements.AddRange(elements);
                        
                        // Recursively search for nested shadow roots
                        var nestedElements = FindNestedShadowElements(shadowRoot, elementType);
                        shadowElements.AddRange(nestedElements);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Could not access shadow root for element {shadowHost.TagName}: {ex.Message}");
                    // Continue with next potential shadow host
                }
            }
            
            _logger.LogDebug($"Found {shadowElements.Count} potential {elementType} elements in Shadow DOM");
            return shadowElements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error searching Shadow DOM for {elementType} elements");
            return shadowElements;
        }
    }

    /// <summary>
    /// Finds potential shadow host elements in the page
    /// </summary>
    private List<IWebElement> FindPotentialShadowHosts(IWebDriver driver)
    {
        var potentialHosts = new List<IWebElement>();
        
        try
        {
            // Common shadow host selectors
            string[] shadowHostSelectors = {
                // Custom elements (most common shadow hosts)
                "*[is]",
                // Elements with shadow-related data attributes
                "*[data-shadow]",
                "*[data-shadow-root]",
                // Common component frameworks that use shadow DOM
                "*[data-component]",
                "*[data-widget]",
                // Elements with shadow-related IDs or classes
                "*[id*='shadow']",
                "*[class*='shadow']",
                // Common component library patterns
                "app-*", "ui-*", "component-*", "widget-*",
                // Material UI and other component libraries
                "mui-*", "md-*", "mdc-*", "paper-*",
                // Framework-specific components
                "ion-*", "stencil-*", "lwc-*",
                // React and Vue.js components with Shadow DOM
                "*[data-reactroot]", "*[data-v-*]",
                // Angular components
                "*[ng-*]", "*[data-ng-*]",
                // Lit and Polymer components
                "lit-*", "polymer-*",
                // Common custom element patterns (elements with hyphens in tag names)
                // Note: We use JavaScript to find actual custom elements instead of the broad selector
            };

            foreach (var selector in shadowHostSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    potentialHosts.AddRange(elements);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error with shadow host selector '{selector}': {ex.Message}");
                }
            }

            // Also check for elements that might have shadow roots using JavaScript
            try
            {
                var js = (IJavaScriptExecutor)driver;
                var shadowHostsScript = @"
                    var hosts = [];
                    var walker = document.createTreeWalker(
                        document.body,
                        NodeFilter.SHOW_ELEMENT,
                        {
                            acceptNode: function(node) {
                                return node.shadowRoot ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
                            }
                        }
                    );
                    var node;
                    while (node = walker.nextNode()) {
                        hosts.push(node);
                    }
                    
                    // Also find custom elements (elements with hyphens in tag names)
                    var customElements = Array.from(document.querySelectorAll('*')).filter(function(el) {
                        return el.tagName.includes('-') && 
                               !['SCRIPT', 'STYLE', 'META', 'LINK', 'TITLE'].includes(el.tagName);
                    });
                    
                    // Check if custom elements have shadow roots
                    customElements.forEach(function(el) {
                        if (el.shadowRoot && !hosts.includes(el)) {
                            hosts.push(el);
                        }
                    });
                    
                    return hosts;
                ";
                
                var jsHosts = js.ExecuteScript(shadowHostsScript);
                if (jsHosts is IReadOnlyCollection<object> hostCollection)
                {
                    foreach (var host in hostCollection)
                    {
                        if (host is IWebElement element)
                        {
                            potentialHosts.Add(element);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"JavaScript shadow host detection failed: {ex.Message}");
            }

            // Remove duplicates and filter out invalid elements
            var uniqueHosts = potentialHosts
                .Distinct()
                .Where(element => 
                {
                    try
                    {
                        return element.Displayed && !string.IsNullOrEmpty(element.TagName);
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();

            _logger.LogDebug($"Found {uniqueHosts.Count} potential shadow host elements");
            return uniqueHosts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding potential shadow hosts");
            return new List<IWebElement>();
        }
    }

    /// <summary>
    /// Gets the shadow root from an element using multiple strategies
    /// </summary>
    private ISearchContext? GetShadowRoot(IWebElement element)
    {
        try
        {
            // Strategy 1: Native getShadowRoot() method (Selenium 4.0+)
            try
            {
                var shadowRoot = element.GetShadowRoot();
                _logger.LogDebug("Successfully obtained shadow root using native getShadowRoot() method");
                return shadowRoot;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Native getShadowRoot() failed: {ex.Message}");
            }

            // Strategy 2: JavaScript fallback
            try
            {
                var js = (IJavaScriptExecutor)((IWrapsDriver)element).WrappedDriver;
                var shadowRoot = js.ExecuteScript("return arguments[0].shadowRoot", element);
                
                if (shadowRoot is ISearchContext searchContext)
                {
                    _logger.LogDebug("Successfully obtained shadow root using JavaScript method");
                    return searchContext;
                }
                else if (shadowRoot != null)
                {
                    // Handle the case where JavaScript returns a dictionary (Chromium 96+)
                    _logger.LogDebug("Shadow root returned as object, attempting to convert");
                    // In some cases, we might need to handle this differently
                    // For now, we'll try to cast it as ISearchContext
                    return shadowRoot as ISearchContext;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"JavaScript getShadowRoot() failed: {ex.Message}");
            }

            // Strategy 3: Check if element has shadow root using JavaScript
            try
            {
                var js = (IJavaScriptExecutor)((IWrapsDriver)element).WrappedDriver;
                var hasShadowRoot = js.ExecuteScript("return arguments[0].shadowRoot !== null", element);
                
                if (hasShadowRoot is bool hasRoot && hasRoot)
                {
                    _logger.LogDebug("Element has shadow root but could not access it directly");
                    // Element has a shadow root but we can't access it (closed shadow root)
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Shadow root existence check failed: {ex.Message}");
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Failed to get shadow root from element: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Finds elements within a specific shadow root
    /// </summary>
    private List<IWebElement> FindElementsInShadowRoot(ISearchContext shadowRoot, ElementType elementType)
    {
        var elements = new List<IWebElement>();
        
        try
        {
            string[] selectors = GetSelectorsForElementType(elementType);
            
            foreach (var selector in selectors)
            {
                try
                {
                    var foundElements = shadowRoot.FindElements(By.CssSelector(selector));
                    elements.AddRange(foundElements);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error with shadow root selector '{selector}': {ex.Message}");
                }
            }

            _logger.LogDebug($"Found {elements.Count} {elementType} elements in shadow root");
            return elements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error finding elements in shadow root for {elementType}");
            return elements;
        }
    }

    /// <summary>
    /// Recursively finds elements in nested shadow roots
    /// </summary>
    private List<IWebElement> FindNestedShadowElements(ISearchContext shadowRoot, ElementType elementType)
    {
        var nestedElements = new List<IWebElement>();
        
        try
        {
            // Find potential shadow hosts within this shadow root
            var nestedHosts = shadowRoot.FindElements(By.CssSelector("*"));
            
            foreach (var nestedHost in nestedHosts)
            {
                try
                {
                    var nestedShadowRoot = GetShadowRoot(nestedHost);
                    if (nestedShadowRoot != null)
                    {
                        _logger.LogDebug($"Found nested shadow root in {nestedHost.TagName}");
                        
                        // Search this nested shadow root
                        var elements = FindElementsInShadowRoot(nestedShadowRoot, elementType);
                        nestedElements.AddRange(elements);
                        
                        // Recursively search deeper
                        var deeperElements = FindNestedShadowElements(nestedShadowRoot, elementType);
                        nestedElements.AddRange(deeperElements);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error accessing nested shadow root: {ex.Message}");
                }
            }

            _logger.LogDebug($"Found {nestedElements.Count} {elementType} elements in nested shadow roots");
            return nestedElements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error finding nested shadow elements for {elementType}");
            return nestedElements;
        }
    }

    /// <summary>
    /// Gets appropriate CSS selectors for each element type for shadow DOM searching
    /// </summary>
    private string[] GetSelectorsForElementType(ElementType elementType)
    {
        return elementType switch
        {
            ElementType.Username => new[] {
                "input[type='text']",
                "input[type='email']", 
                "input:not([type])",
                "input[type='']",
                "*[role='textbox']",
                "*[contenteditable='true']"
            },
            ElementType.Password => new[] {
                "input[type='password']",
                "*[role='password']"
            },
            ElementType.Domain => new[] {
                "select",
                "input[type='text']",
                "*[role='combobox']",
                "*[role='listbox']"
            },
            ElementType.SubmitButton => new[] {
                "button",
                "input[type='submit']",
                "input[type='button']",
                "*[role='button']",
                "a[href='#']",
                "a[href='javascript:void(0)']"
            },
            _ => new string[0]
        };
    }

    /// <summary>
    /// Scores an element for a specific type with shadow DOM context awareness
    /// </summary>
    private int ScoreElementForType(IWebElement element, ElementType elementType)
    {
        try
        {
            // Use the existing scoring methods but adapt for shadow DOM context
            var candidates = new Dictionary<IWebElement, int>();
            
            switch (elementType)
            {
                case ElementType.Username:
                    ScoreUsernameElements(new List<IWebElement> { element }, candidates);
                    break;
                case ElementType.Password:
                    ScorePasswordElements(new List<IWebElement> { element }, candidates);
                    break;
                case ElementType.Domain:
                    ScoreDomainElements(new List<IWebElement> { element }, candidates);
                    break;
                case ElementType.SubmitButton:
                    ScoreSubmitElements(new List<IWebElement> { element }, candidates);
                    break;
            }

            return candidates.GetValueOrDefault(element, 0);
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Error scoring shadow DOM element: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Checks if an element is within a shadow DOM
    /// </summary>
    private bool IsElementInShadowDOM(IWebElement element)
    {
        try
        {
            var js = (IJavaScriptExecutor)((IWrapsDriver)element).WrappedDriver;
            var isInShadow = js.ExecuteScript(@"
                var element = arguments[0];
                var root = element.getRootNode();
                return root !== document && root.nodeType === Node.DOCUMENT_FRAGMENT_NODE;
            ", element);
            
            return isInShadow is bool inShadow && inShadow;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Error checking if element is in shadow DOM: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enhanced detection method that includes Shadow DOM support
    /// </summary>
    private LoginFormElements DetectWithShadowDOMSupport(IWebDriver driver)
    {
        _logger.LogDebug("Trying to detect login form with Shadow DOM support");
        
        var loginForm = new LoginFormElements();

        try
        {
            // Enhanced detection with Shadow DOM awareness
            loginForm.UsernameField = FindBestElementByScoreWithShadowDOM(driver, ElementType.Username);
            loginForm.PasswordField = FindBestElementByScoreWithShadowDOM(driver, ElementType.Password);
            
            // Pass already detected fields to prevent overlap
            var alreadyDetectedFields = new List<IWebElement?> { loginForm.UsernameField, loginForm.PasswordField };
            loginForm.DomainField = FindBestElementByScoreWithShadowDOM(driver, ElementType.Domain, alreadyDetectedFields);
            
            loginForm.SubmitButton = FindBestElementByScoreWithShadowDOM(driver, ElementType.SubmitButton);

            if (loginForm.UsernameField != null || loginForm.PasswordField != null)
            {
                _logger.LogInformation("Successfully detected login form elements using Shadow DOM-aware detection");
                
                // Log which elements were found in Shadow DOM
                if (loginForm.UsernameField != null && IsElementInShadowDOM(loginForm.UsernameField))
                    _logger.LogInformation("Username field found in Shadow DOM");
                if (loginForm.PasswordField != null && IsElementInShadowDOM(loginForm.PasswordField))
                    _logger.LogInformation("Password field found in Shadow DOM");
                if (loginForm.DomainField != null && IsElementInShadowDOM(loginForm.DomainField))
                    _logger.LogInformation("Domain field found in Shadow DOM");
                if (loginForm.SubmitButton != null && IsElementInShadowDOM(loginForm.SubmitButton))
                    _logger.LogInformation("Submit button found in Shadow DOM");
            }

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Shadow DOM-aware detection strategy");
            return new LoginFormElements();
        }
    }

    #endregion

    #region Enhanced Scoring and Confidence Calculation

    /// <summary>
    /// Determines the order of detection methods based on the recommended method and historical performance.
    /// </summary>
    /// <param name="recommendedMethod">The method recommended by historical analysis</param>
    /// <returns>Ordered list of detection methods to try</returns>
    private List<DetectionMethod> GetMethodOrder(DetectionMethod recommendedMethod)
    {
        var methods = new List<DetectionMethod>();
        
        // Start with recommended method
        methods.Add(recommendedMethod);
        
        // Add other methods as fallbacks
        foreach (DetectionMethod method in Enum.GetValues<DetectionMethod>())
        {
            if (method != recommendedMethod)
            {
                methods.Add(method);
            }
        }
        
        return methods;
    }

    /// <summary>
    /// Calculates confidence score for URL-specific configuration detection.
    /// </summary>
    /// <param name="elements">Detected form elements</param>
    /// <param name="config">Configuration used</param>
    /// <returns>Confidence score (0-100)</returns>
    private int CalculateConfigurationConfidence(LoginFormElements? elements, LoginPageConfiguration config)
    {
        if (!IsValidLoginForm(elements)) return 0;
        
        int confidence = 85; // High base confidence for URL-specific detection
        
        // Boost confidence based on elements found
        if (elements.UsernameField != null) confidence += 5;
        if (elements.PasswordField != null) confidence += 5;
        if (elements.SubmitButton != null) confidence += 3;
        if (elements.DomainField != null) confidence += 2;
        
        return Math.Min(confidence, 100);
    }

    /// <summary>
    /// Calculates confidence score for common attributes detection.
    /// </summary>
    /// <param name="elements">Detected form elements</param>
    /// <param name="driver">WebDriver instance for additional analysis</param>
    /// <returns>Confidence score (0-100)</returns>
    private int CalculateCommonAttributesConfidence(LoginFormElements? elements, IWebDriver driver)
    {
        if (!IsValidLoginForm(elements)) return 0;
        
        int confidence = 0;
        int maxPossibleScore = 0;
        
        // Analyze username field confidence
        if (elements.UsernameField != null)
        {
            var usernameScore = AnalyzeElementConfidence(elements.UsernameField, ElementType.Username);
            confidence += usernameScore;
            maxPossibleScore += 100;
        }
        
        // Analyze password field confidence
        if (elements.PasswordField != null)
        {
            var passwordScore = AnalyzeElementConfidence(elements.PasswordField, ElementType.Password);
            confidence += passwordScore;
            maxPossibleScore += 100;
        }
        
        // Analyze submit button confidence
        if (elements.SubmitButton != null)
        {
            var submitScore = AnalyzeElementConfidence(elements.SubmitButton, ElementType.SubmitButton);
            confidence += submitScore;
            maxPossibleScore += 100;
        }
        
        // Calculate percentage confidence
        if (maxPossibleScore > 0)
        {
            confidence = (int)Math.Round((double)confidence / maxPossibleScore * 100);
            
            // Apply bonuses for form completeness
            if (elements.UsernameField != null && elements.PasswordField != null && elements.SubmitButton != null)
            {
                confidence = Math.Min(confidence + 10, 100); // Complete form bonus
            }
        }
        
        return confidence;
    }

    /// <summary>
    /// Calculates confidence score for XPath detection.
    /// </summary>
    /// <param name="elements">Detected form elements</param>
    /// <returns>Confidence score (0-100)</returns>
    private int CalculateXPathConfidence(LoginFormElements? elements)
    {
        if (!IsValidLoginForm(elements)) return 0;
        
        int confidence = 60; // Moderate base confidence for XPath detection
        
        // Analyze element quality
        if (elements.UsernameField != null && HasStrongIdentifiers(elements.UsernameField))
            confidence += 10;
        if (elements.PasswordField != null && HasStrongIdentifiers(elements.PasswordField))
            confidence += 10;
        if (elements.SubmitButton != null && HasStrongIdentifiers(elements.SubmitButton))
            confidence += 8;
        
        return Math.Min(confidence, 95); // Cap XPath confidence below configuration
    }

    /// <summary>
    /// Calculates confidence score for Shadow DOM detection.
    /// </summary>
    /// <param name="elements">Detected form elements</param>
    /// <param name="driver">WebDriver instance for analysis</param>
    /// <returns>Confidence score (0-100)</returns>
    private int CalculateShadowDOMConfidence(LoginFormElements? elements, IWebDriver driver)
    {
        if (!IsValidLoginForm(elements)) return 0;
        
        int confidence = 70; // Good base confidence for Shadow DOM detection
        
        // Check if elements are actually in Shadow DOM
        bool hasActualShadowElements = false;
        if (elements.UsernameField != null && IsElementInShadowDOM(elements.UsernameField))
        {
            confidence += 10;
            hasActualShadowElements = true;
        }
        if (elements.PasswordField != null && IsElementInShadowDOM(elements.PasswordField))
        {
            confidence += 10;
            hasActualShadowElements = true;
        }
        if (elements.SubmitButton != null && IsElementInShadowDOM(elements.SubmitButton))
        {
            confidence += 5;
            hasActualShadowElements = true;
        }
        
        // If no elements are actually in Shadow DOM, reduce confidence
        if (!hasActualShadowElements)
        {
            confidence = Math.Max(confidence - 20, 40);
        }
        
        return Math.Min(confidence, 90);
    }

    /// <summary>
    /// Analyzes the confidence of a specific element based on its attributes and context.
    /// </summary>
    /// <param name="element">Element to analyze</param>
    /// <param name="elementType">Expected type of the element</param>
    /// <returns>Confidence score for this element (0-100)</returns>
    private int AnalyzeElementConfidence(IWebElement element, ElementType elementType)
    {
        try
        {
            var id = GetAttributeLower(element, "id");
            var name = GetAttributeLower(element, "name");
            var type = GetAttributeLower(element, "type");
            var placeholder = GetAttributeLower(element, "placeholder");
            var ariaLabel = GetAttributeLower(element, "aria-label");
            var className = GetAttributeLower(element, "class");
            
            int confidence = 0;
            
            // Get target terms for this element type
            string[] targetTerms = elementType switch
            {
                ElementType.Username => new[] { "username", "user", "login", "email", "account" },
                ElementType.Password => new[] { "password", "pass", "pwd", "secret" },
                ElementType.Domain => new[] { "domain", "tenant", "org", "company" },
                ElementType.SubmitButton => new[] { "login", "submit", "signin", "enter" },
                _ => Array.Empty<string>()
            };
            
            // Score attributes with fuzzy matching
            foreach (var term in targetTerms)
            {
                if (!string.IsNullOrEmpty(id) && CalculateFuzzyScore(term, id, elementType) > 80)
                    confidence += 25;
                if (!string.IsNullOrEmpty(name) && CalculateFuzzyScore(term, name, elementType) > 80)
                    confidence += 20;
                if (!string.IsNullOrEmpty(placeholder) && CalculateFuzzyScore(term, placeholder, elementType) > 70)
                    confidence += 15;
                if (!string.IsNullOrEmpty(ariaLabel) && CalculateFuzzyScore(term, ariaLabel, elementType) > 70)
                    confidence += 15;
            }
            
            // Type-specific bonuses
            switch (elementType)
            {
                case ElementType.Username when type == "email":
                    confidence += 20;
                    break;
                case ElementType.Password when type == "password":
                    confidence += 30;
                    break;
                case ElementType.SubmitButton when type == "submit":
                    confidence += 25;
                    break;
            }
            
            // Visibility and form context bonuses
            if (IsElementVisible(element))
                confidence += 10;
            
            return Math.Min(confidence, 100);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Checks if an element has strong identifying attributes.
    /// </summary>
    /// <param name="element">Element to check</param>
    /// <returns>True if element has strong identifiers</returns>
    private bool HasStrongIdentifiers(IWebElement element)
    {
        try
        {
            var id = element.GetAttribute("id");
            var name = element.GetAttribute("name");
            var dataTestId = element.GetAttribute("data-testid");
            
            return !string.IsNullOrEmpty(id) || 
                   !string.IsNullOrEmpty(name) || 
                   !string.IsNullOrEmpty(dataTestId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts selector details for metrics tracking.
    /// </summary>
    /// <param name="elements">Detected form elements</param>
    /// <param name="driver">WebDriver instance</param>
    /// <returns>Dictionary of selector details for tracking</returns>
    private Dictionary<string, SelectorDetails> ExtractSelectorDetails(LoginFormElements elements, IWebDriver driver)
    {
        var details = new Dictionary<string, SelectorDetails>();
        
        try
        {
            if (elements.UsernameField != null)
            {
                details["username"] = CreateSelectorDetails(elements.UsernameField, "username");
            }
            
            if (elements.PasswordField != null)
            {
                details["password"] = CreateSelectorDetails(elements.PasswordField, "password");
            }
            
            if (elements.SubmitButton != null)
            {
                details["submit"] = CreateSelectorDetails(elements.SubmitButton, "submit");
            }
            
            if (elements.DomainField != null)
            {
                details["domain"] = CreateSelectorDetails(elements.DomainField, "domain");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Error extracting selector details: {ex.Message}");
        }
        
        return details;
    }

    /// <summary>
    /// Creates selector details for a specific element.
    /// </summary>
    /// <param name="element">Element to analyze</param>
    /// <param name="elementType">Type of element</param>
    /// <returns>Selector details for metrics tracking</returns>
    private SelectorDetails CreateSelectorDetails(IWebElement element, string elementType)
    {
        try
        {
            var id = element.GetAttribute("id");
            var name = element.GetAttribute("name");
            var className = element.GetAttribute("class");
            var tagName = element.TagName;
            
            // Build most likely selector
            string selector = "";
            string attributeUsed = "";
            
            if (!string.IsNullOrEmpty(id))
            {
                selector = $"#{id}";
                attributeUsed = "id";
            }
            else if (!string.IsNullOrEmpty(name))
            {
                selector = $"{tagName}[name='{name}']";
                attributeUsed = "name";
            }
            else if (!string.IsNullOrEmpty(className))
            {
                var firstClass = className.Split(' ')[0];
                selector = $"{tagName}.{firstClass}";
                attributeUsed = "class";
            }
            else
            {
                selector = tagName;
                attributeUsed = "tagName";
            }
            
            return new SelectorDetails
            {
                ElementType = elementType,
                Selector = selector,
                AttributeUsed = attributeUsed,
                Score = AnalyzeElementConfidence(element, Enum.Parse<ElementType>(elementType, true)),
                WasSuccessful = true
            };
        }
        catch
        {
            return new SelectorDetails
            {
                ElementType = elementType,
                Selector = "unknown",
                AttributeUsed = "none",
                Score = 0,
                WasSuccessful = false
            };
        }
    }

    #endregion

    /// <summary>
    /// Performance-optimized configuration-based detection using cached elements
    /// </summary>
    private async Task<LoginFormElements> DetectByConfigurationOptimizedAsync(IWebDriver driver, LoginPageConfiguration config)
    {
        _logger.LogDebug($"Starting optimized configuration-based detection: {config.DisplayName}");
        
        var loginForm = new LoginFormElements();

        try
        {
            // Performance optimization: Use cached elements for faster selector matching
            var inputElements = GetCachedElements("inputs");
            var buttonElements = GetCachedElements("buttons");
            var selectElements = GetCachedElements("selects");
            var linkElements = GetCachedElements("links");

            // Try to find username field using config selectors with early termination
            loginForm.UsernameField = await FindElementBySelectorsOptimizedAsync(inputElements, config.UsernameSelectors, "username");

            // Try to find password field using config selectors with early termination
            loginForm.PasswordField = await FindElementBySelectorsOptimizedAsync(inputElements, config.PasswordSelectors, "password");

            // Try to find domain field using config selectors (optional)
            var domainElements = inputElements.Concat(selectElements).ToList();
            loginForm.DomainField = await FindElementBySelectorsOptimizedAsync(domainElements, config.DomainSelectors, "domain");

            // Try to find submit button using config selectors
            var submitElements = buttonElements.Concat(linkElements).Concat(inputElements.Where(i => 
                i.GetAttribute("type")?.ToLower() == "submit" || 
                i.GetAttribute("type")?.ToLower() == "button")).ToList();
            loginForm.SubmitButton = await FindElementBySelectorsOptimizedAsync(submitElements, config.SubmitButtonSelectors, "submit");

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in optimized configuration-based detection for: {config.DisplayName}");
            return new LoginFormElements();
        }
    }

    /// <summary>
    /// Performance-optimized element finding using cached elements and early termination
    /// </summary>
    private async Task<IWebElement?> FindElementBySelectorsOptimizedAsync(List<IWebElement> elements, List<string> selectors, string elementType)
    {
        if (!selectors.Any() || !elements.Any())
            return null;

        try
        {
            // Performance optimization: Batch selector matching against cached elements
            foreach (var element in elements)
            {
                try
                {
                    if (!IsElementVisible(element))
                        continue;

                    foreach (var selector in selectors)
                    {
                        if (ElementMatchesSelector(element, selector))
                        {
                            _logger.LogDebug($"{elementType} field found with optimized selector: {selector}");
                            return element;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error checking element against selectors: {ex.Message}");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Error in optimized element selection for {elementType}");
            return null;
        }
    }

    /// <summary>
    /// Performance-optimized common attributes detection using cached elements
    /// </summary>
    private async Task<LoginFormElements> DetectByCommonAttributesOptimizedAsync(IWebDriver driver)
    {
        _logger.LogDebug("Starting optimized common attributes detection");
        
        var loginForm = new LoginFormElements();

        try
        {
            // Performance optimization: Use cached elements and optimized scoring
            loginForm.UsernameField = await FindBestElementByScoreOptimizedAsync(ElementType.Username);
            loginForm.PasswordField = await FindBestElementByScoreOptimizedAsync(ElementType.Password);
            
            // For domain field, we need to manually exclude already detected fields since the optimized method doesn't support exclusion
            loginForm.DomainField = await FindBestElementByScoreOptimizedAsyncWithExclusion(ElementType.Domain, new List<IWebElement?> { loginForm.UsernameField, loginForm.PasswordField });
            
            loginForm.SubmitButton = await FindBestElementByScoreOptimizedAsync(ElementType.SubmitButton);

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in optimized common attributes detection");
            return new LoginFormElements();
        }
    }

    /// <summary>
    /// Performance-optimized XPath detection using cached elements and smart XPath strategies
    /// </summary>
    private async Task<LoginFormElements> DetectByXPathOptimizedAsync(IWebDriver driver)
    {
        _logger.LogDebug("Starting optimized XPath-based detection");
        
        var loginForm = new LoginFormElements();

        try
        {
            // Performance optimization: Smart XPath queries with early termination
            var usernameXPaths = new[]
            {
                "//input[@type='text' and (contains(@name,'user') or contains(@id,'user') or contains(@placeholder,'user'))]",
                "//input[@type='email']",
                "//input[contains(@class,'username') or contains(@class,'user')]",
                "//input[@type='text'][1]" // Fallback: first text input
            };

            var passwordXPaths = new[]
            {
                "//input[@type='password']",
                "//input[contains(@name,'pass') or contains(@id,'pass')]",
                "//input[contains(@class,'password')]"
            };

            var submitXPaths = new[]
            {
                "//button[@type='submit' or contains(text(),'Login') or contains(text(),'Sign in')]",
                "//input[@type='submit']",
                "//button[contains(@class,'login') or contains(@class,'submit')]",
                "//a[contains(@class,'login') or contains(text(),'Login')]"
            };

            // Use optimized XPath execution with early termination
            loginForm.UsernameField = await FindElementByXPathsOptimizedAsync(driver, usernameXPaths, "username");
            loginForm.PasswordField = await FindElementByXPathsOptimizedAsync(driver, passwordXPaths, "password");
            loginForm.SubmitButton = await FindElementByXPathsOptimizedAsync(driver, submitXPaths, "submit");

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in optimized XPath detection");
            return new LoginFormElements();
        }
    }

    /// <summary>
    /// Performance-optimized Shadow DOM detection using cached elements and intelligent traversal
    /// </summary>
    private async Task<LoginFormElements> DetectWithShadowDOMOptimizedAsync(IWebDriver driver)
    {
        _logger.LogDebug("Starting optimized Shadow DOM detection");
        
        var loginForm = new LoginFormElements();

        try
        {
            // Performance optimization: Use cached elements for Shadow DOM hosts
            var potentialHosts = GetCachedElements("formElements").Where(IsLikelyShadowHost).ToList();

            // Performance optimization: Process shadow hosts in parallel for better throughput
            var shadowElements = new List<IWebElement>();
            
            foreach (var host in potentialHosts.Take(10)) // Limit to prevent excessive processing
            {
                try
                {
                    var shadowRoot = GetShadowRoot(host);
                    if (shadowRoot != null)
                    {
                        var elements = await FindElementsInShadowRootOptimizedAsync(shadowRoot);
                        shadowElements.AddRange(elements);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error accessing shadow root: {ex.Message}");
                }
            }

            // Score shadow DOM elements using the optimized scoring system
            if (shadowElements.Any())
            {
                var candidates = new Dictionary<IWebElement, int>();
                
                foreach (var element in shadowElements)
                {
                    var score = ScoreElementForType(element, ElementType.Username);
                    if (score > 0) candidates[element] = score + 500; // Shadow DOM bonus
                }

                var bestUsername = candidates.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
                if (bestUsername != null)
                {
                    loginForm.UsernameField = bestUsername;
                }

                // Find password field in shadow DOM
                candidates.Clear();
                foreach (var element in shadowElements)
                {
                    var score = ScoreElementForType(element, ElementType.Password);
                    if (score > 0) candidates[element] = score + 500;
                }

                var bestPassword = candidates.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
                if (bestPassword != null)
                {
                    loginForm.PasswordField = bestPassword;
                }
            }

            return loginForm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in optimized Shadow DOM detection");
            return new LoginFormElements();
        }
    }

    #region Performance Optimization Helper Methods

    /// <summary>
    /// Get cached elements by type with fallback to empty list
    /// </summary>
    private List<IWebElement> GetCachedElements(string elementType)
    {
        return _elementCache.TryGetValue(elementType, out var elements) ? elements : new List<IWebElement>();
    }

    /// <summary>
    /// Check if an element matches a given CSS selector using cached attributes
    /// </summary>
    private bool ElementMatchesSelector(IWebElement element, string selector)
    {
        try
        {
            // Performance optimization: Use cached attribute extraction
            var tagName = element.TagName?.ToLower();
            var id = element.GetAttribute("id");
            var className = element.GetAttribute("class");
            var name = element.GetAttribute("name");
            var type = element.GetAttribute("type");

            // Simple selector parsing for common patterns
            if (selector.StartsWith("#") && !string.IsNullOrEmpty(id))
            {
                return id == selector.Substring(1);
            }

            if (selector.StartsWith(".") && !string.IsNullOrEmpty(className))
            {
                var classes = className.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return classes.Contains(selector.Substring(1));
            }

            if (selector.Contains("[") && selector.Contains("]"))
            {
                // Attribute selector parsing - simplified for common cases
                if (selector.Contains("type=") && !string.IsNullOrEmpty(type))
                {
                    var expectedType = ExtractAttributeValue(selector, "type");
                    return type.Equals(expectedType, StringComparison.OrdinalIgnoreCase);
                }

                if (selector.Contains("name=") && !string.IsNullOrEmpty(name))
                {
                    var expectedName = ExtractAttributeValue(selector, "name");
                    return name.Equals(expectedName, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Tag name matching
            if (!selector.Contains("[") && !selector.Contains("#") && !selector.Contains("."))
            {
                return tagName == selector.ToLower();
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extract attribute value from a CSS selector
    /// </summary>
    private string ExtractAttributeValue(string selector, string attribute)
    {
        try
        {
            var pattern = $@"{attribute}=['""]?([^'""\]]+)['""]?";
            var match = GetCachedRegex(pattern).Match(selector);
            return match.Success ? match.Groups[1].Value : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Get or create cached regex pattern for performance optimization
    /// </summary>
    private Regex GetCachedRegex(string pattern)
    {
        return _regexCache.GetOrAdd(pattern, p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled));
    }

    /// <summary>
    /// Performance-optimized element scoring using cached elements
    /// </summary>
    private async Task<IWebElement?> FindBestElementByScoreOptimizedAsync(ElementType elementType)
    {
        var candidates = new Dictionary<IWebElement, int>();
        
        try
        {
            // Get relevant cached elements based on element type
            var elements = GetRelevantElementsForType(elementType);
            
            // Performance optimization: Parallel scoring for better throughput
            var scoringTasks = elements.Select(element => Task.Run(() => 
            {
                try
                {
                    var score = ScoreElementForType(element, elementType);
                    if (score > 0 && IsElementVisible(element))
                    {
                        lock (candidates)
                        {
                            candidates[element] = score;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error scoring element: {ex.Message}");
                }
            })).ToArray();

            await Task.WhenAll(scoringTasks);

            // Return the highest scoring element
            var bestCandidate = candidates
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault();

            if (bestCandidate.Key != null)
            {
                _logger.LogDebug($"Found {elementType} element with optimized score {bestCandidate.Value}");
                return bestCandidate.Key;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in optimized element scoring for {elementType}");
            return null;
        }
    }

    /// <summary>
    /// Get relevant cached elements for a specific element type
    /// </summary>
    private List<IWebElement> GetRelevantElementsForType(ElementType elementType)
    {
        return elementType switch
        {
            ElementType.Username => GetCachedElements("inputs"),
            ElementType.Password => GetCachedElements("inputs"),
            ElementType.Domain => GetCachedElements("inputs").Concat(GetCachedElements("selects")).ToList(),
            ElementType.SubmitButton => GetCachedElements("clickableElements"),
            _ => GetCachedElements("formElements")
        };
    }

    /// <summary>
    /// Performance-optimized XPath element finding with early termination
    /// </summary>
    private async Task<IWebElement?> FindElementByXPathsOptimizedAsync(IWebDriver driver, string[] xpaths, string elementType)
    {
        foreach (var xpath in xpaths)
        {
            try
            {
                var elements = driver.FindElements(By.XPath(xpath));
                var validElement = elements.FirstOrDefault(IsElementVisible);
                
                if (validElement != null)
                {
                    _logger.LogDebug($"{elementType} field found with optimized XPath: {xpath}");
                    return validElement;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error with XPath '{xpath}': {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Check if an element is likely to be a Shadow DOM host
    /// </summary>
    private bool IsLikelyShadowHost(IWebElement element)
    {
        try
        {
            var tagName = element.TagName?.ToLower();
            
            // Custom elements (with hyphens) are common shadow hosts
            if (!string.IsNullOrEmpty(tagName) && tagName.Contains('-'))
                return true;

            // Elements with shadow-related attributes
            var shadowAttributes = new[] { "data-shadow", "data-shadow-root", "data-component", "data-widget" };
            if (shadowAttributes.Any(attr => !string.IsNullOrEmpty(element.GetAttribute(attr))))
                return true;

            // Framework-specific patterns
            var frameworkPrefixes = new[] { "app-", "ui-", "component-", "widget-", "mui-", "md-", "ion-", "lit-" };
            if (!string.IsNullOrEmpty(tagName) && frameworkPrefixes.Any(prefix => tagName.StartsWith(prefix)))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Performance-optimized Shadow DOM element finding
    /// </summary>
    private async Task<List<IWebElement>> FindElementsInShadowRootOptimizedAsync(ISearchContext shadowRoot)
    {
        var elements = new List<IWebElement>();
        
        try
        {
            // Performance optimization: Batch queries for all relevant element types
            var inputSelectors = new[] { "input[type='text']", "input[type='email']", "input[type='password']" };
            var buttonSelectors = new[] { "button", "input[type='submit']", "input[type='button']" };
            var allSelectors = inputSelectors.Concat(buttonSelectors);

            foreach (var selector in allSelectors)
            {
                try
                {
                    var foundElements = shadowRoot.FindElements(By.CssSelector(selector));
                    elements.AddRange(foundElements.Where(IsElementVisible));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error with shadow root selector '{selector}': {ex.Message}");
                }
            }

            return elements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding elements in shadow root");
            return elements;
        }
    }

    /// <summary>
    /// Performance-optimized confidence calculation for common attributes detection
    /// </summary>
    private int CalculateCommonAttributesConfidence(LoginFormElements? elements)
    {
        if (!IsValidLoginForm(elements)) return 0;
        
        int confidence = 70; // Base confidence for common attributes
        
        // Performance optimization: Quick confidence calculation
        if (elements.UsernameField != null)
        {
            confidence += HasStrongIdentifiers(elements.UsernameField) ? 10 : 5;
        }
        
        if (elements.PasswordField != null)
        {
            confidence += HasStrongIdentifiers(elements.PasswordField) ? 10 : 5;
        }
        
        if (elements.SubmitButton != null)
        {
            confidence += 5;
        }
        
        return Math.Min(confidence, 95);
    }

    #endregion

    /// <summary>
    /// Performance-optimized element scoring using cached elements with exclusion support
    /// </summary>
    private async Task<IWebElement?> FindBestElementByScoreOptimizedAsyncWithExclusion(ElementType elementType, List<IWebElement?>? excludeElements = null)
    {
        var candidates = new Dictionary<IWebElement, int>();
        
        try
        {
            // Get relevant cached elements based on element type
            var elements = GetRelevantElementsForType(elementType);
            
            // Performance optimization: Parallel scoring for better throughput
            var scoringTasks = elements.Select(element => Task.Run(() => 
            {
                try
                {
                    // Skip elements that are in the exclusion list
                    if (excludeElements?.Contains(element) == true) return;
                    
                    var score = ScoreElementForType(element, elementType);
                    if (score > 0 && IsElementVisible(element))
                    {
                        lock (candidates)
                        {
                            candidates[element] = score;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error scoring element: {ex.Message}");
                }
            })).ToArray();

            await Task.WhenAll(scoringTasks);

            // Return the highest scoring element that's not in the exclusion list
            var bestCandidate = candidates
                .Where(kvp => excludeElements?.Contains(kvp.Key) != true) // Double-check exclusion
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault();

            if (bestCandidate.Key != null)
            {
                _logger.LogDebug($"Found {elementType} element with optimized score {bestCandidate.Value} (with exclusion)");
                return bestCandidate.Key;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in optimized element scoring with exclusion for {elementType}");
            return null;
        }
    }

    /// <summary>
    /// Checks if two WebElements are the same element by comparing their properties.
    /// </summary>
    private bool AreSameElement(IWebElement? element1, IWebElement? element2)
    {
        if (element1 == null || element2 == null) return false;
        if (ReferenceEquals(element1, element2)) return true;

        try
        {
            string id1 = GetAttributeLower(element1, "id");
            string id2 = GetAttributeLower(element2, "id");
            string name1 = GetAttributeLower(element1, "name");
            string name2 = GetAttributeLower(element2, "name");
            string tag1 = element1.TagName?.ToLower() ?? string.Empty;
            string tag2 = element2.TagName?.ToLower() ?? string.Empty;

            if (!string.IsNullOrEmpty(id1) && id1 == id2 && tag1 == tag2) return true;
            if (!string.IsNullOrEmpty(name1) && name1 == name2 && tag1 == tag2)
            {
                // If IDs are present but different, they are not the same, even if names/tags match.
                if (!string.IsNullOrEmpty(id1) && !string.IsNullOrEmpty(id2) && id1 != id2) return false;
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception during AreSameElement comparison.");
            return false;
        }
    }
}
