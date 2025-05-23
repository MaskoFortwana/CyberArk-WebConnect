using System;
using System.Collections.Generic;
using System.Linq;

namespace ChromeConnect.Models
{
    /// <summary>
    /// Configuration for a specific login page, containing URL-specific selectors and behaviors.
    /// </summary>
    public class LoginPageConfiguration
    {
        /// <summary>
        /// Gets or sets the URL pattern this configuration applies to.
        /// </summary>
        public string UrlPattern { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the priority of this configuration (higher numbers have priority).
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Gets or sets the display name for this configuration.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CSS selectors for the username field.
        /// </summary>
        public List<string> UsernameSelectors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the CSS selectors for the password field.
        /// </summary>
        public List<string> PasswordSelectors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the CSS selectors for the domain field (optional).
        /// </summary>
        public List<string> DomainSelectors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the CSS selectors for the submit button.
        /// </summary>
        public List<string> SubmitButtonSelectors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets additional wait time in milliseconds for this page.
        /// </summary>
        public int AdditionalWaitMs { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether this page requires JavaScript interaction.
        /// </summary>
        public bool RequiresJavaScript { get; set; } = false;

        /// <summary>
        /// Gets or sets selectors that indicate successful login.
        /// </summary>
        public List<string> SuccessIndicators { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets selectors that indicate failed login.
        /// </summary>
        public List<string> FailureIndicators { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets special handling notes for this page.
        /// </summary>
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Manages URL-specific login page configurations.
    /// </summary>
    public static class LoginPageConfigurations
    {
        private static readonly List<LoginPageConfiguration> _configurations = new();

        static LoginPageConfigurations()
        {
            InitializeConfigurations();
        }

        /// <summary>
        /// Gets the configuration for a specific URL.
        /// </summary>
        /// <param name="url">The URL to get configuration for.</param>
        /// <returns>The configuration if found, null otherwise.</returns>
        public static LoginPageConfiguration? GetConfigurationForUrl(string url)
        {
            foreach (var config in _configurations.OrderByDescending(c => c.Priority))
            {
                if (url.Contains(config.UrlPattern, StringComparison.OrdinalIgnoreCase))
                {
                    return config;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets all available configurations.
        /// </summary>
        /// <returns>List of all configurations.</returns>
        public static List<LoginPageConfiguration> GetAllConfigurations()
        {
            return new List<LoginPageConfiguration>(_configurations);
        }

        private static void InitializeConfigurations()
        {
            // Default configuration for generic login pages - Enhanced with more robust selectors
            _configurations.Add(new LoginPageConfiguration
            {
                UrlPattern = "",
                Priority = 0,
                DisplayName = "Generic Login Page",
                UsernameSelectors = new List<string>
                {
                    // Direct username field selectors (highest priority)
                    "input[name='username']",
                    "input[id='username']",
                    "input[name='user']",
                    "input[id='user']",
                    "input[name='login']",
                    "input[id='login']",
                    
                    // Email field selectors
                    "input[type='email']",
                    "input[name='email']",
                    "input[id='email']",
                    
                    // Case-insensitive attribute matching
                    "input[type='text'][id*='user' i]",
                    "input[type='text'][name*='user' i]",
                    "input[type='text'][placeholder*='user' i]",
                    "input[type='text'][id*='login' i]",
                    "input[type='text'][name*='login' i]",
                    "input[type='text'][placeholder*='login' i]",
                    "input[id*='email' i]",
                    "input[name*='email' i]",
                    "input[placeholder*='email' i]",
                    
                    // ARIA label based selectors
                    "input[aria-label*='user' i]",
                    "input[aria-label*='login' i]",
                    "input[aria-label*='email' i]",
                    "input[aria-labelledby*='user' i]",
                    "input[aria-labelledby*='login' i]",
                    "input[aria-labelledby*='email' i]",
                    
                    // Data attribute selectors
                    "input[data-testid*='user' i]",
                    "input[data-testid*='login' i]",
                    "input[data-testid*='email' i]",
                    "input[data-cy*='user' i]",
                    "input[data-cy*='login' i]",
                    "input[data-cy*='email' i]",
                    
                    // Class-based selectors
                    "input[class*='user' i]",
                    "input[class*='login' i]",
                    "input[class*='email' i]",
                    
                    // Form structure based fallbacks
                    "form input[type='text']:first-of-type",
                    "form input[type='email']:first-of-type",
                    ".login-form input[type='text']:first-of-type",
                    "#login-form input[type='text']:first-of-type"
                },
                PasswordSelectors = new List<string>
                {
                    // Direct password field selectors
                    "input[type='password']",
                    "input[name='password']",
                    "input[id='password']",
                    "input[name='pass']",
                    "input[id='pass']",
                    "input[name='pwd']",
                    "input[id='pwd']",
                    
                    // Case-insensitive attribute matching
                    "input[name*='password' i]",
                    "input[id*='password' i]",
                    "input[placeholder*='password' i]",
                    "input[name*='pass' i]",
                    "input[id*='pass' i]",
                    "input[placeholder*='pass' i]",
                    
                    // ARIA label based selectors
                    "input[aria-label*='password' i]",
                    "input[aria-label*='pass' i]",
                    "input[aria-labelledby*='password' i]",
                    "input[aria-labelledby*='pass' i]",
                    
                    // Data attribute selectors
                    "input[data-testid*='password' i]",
                    "input[data-testid*='pass' i]",
                    "input[data-cy*='password' i]",
                    "input[data-cy*='pass' i]",
                    
                    // Class-based selectors
                    "input[class*='password' i]",
                    "input[class*='pass' i]"
                },
                DomainSelectors = new List<string>
                {
                    // Direct domain field selectors
                    "input[name='domain']",
                    "input[id='domain']",
                    "select[name='domain']",
                    "select[id='domain']",
                    "input[name='tenant']",
                    "input[id='tenant']",
                    "select[name='tenant']",
                    "select[id='tenant']",
                    "input[name='organization']",
                    "input[id='organization']",
                    "select[name='organization']",
                    "select[id='organization']",
                    
                    // Case-insensitive attribute matching
                    "input[name*='domain' i]",
                    "input[id*='domain' i]",
                    "input[placeholder*='domain' i]",
                    "select[name*='domain' i]",
                    "select[id*='domain' i]",
                    "input[name*='tenant' i]",
                    "input[id*='tenant' i]",
                    "input[placeholder*='tenant' i]",
                    "select[name*='tenant' i]",
                    "select[id*='tenant' i]",
                    "input[name*='org' i]",
                    "input[id*='org' i]",
                    "input[placeholder*='org' i]",
                    "select[name*='org' i]",
                    "select[id*='org' i]",
                    
                    // ARIA label based selectors
                    "input[aria-label*='domain' i]",
                    "input[aria-label*='tenant' i]",
                    "input[aria-label*='organization' i]",
                    "select[aria-label*='domain' i]",
                    "select[aria-label*='tenant' i]",
                    "select[aria-label*='organization' i]",
                    
                    // Data attribute selectors
                    "input[data-testid*='domain' i]",
                    "input[data-testid*='tenant' i]",
                    "input[data-testid*='org' i]",
                    "select[data-testid*='domain' i]",
                    "select[data-testid*='tenant' i]",
                    "select[data-testid*='org' i]"
                },
                SubmitButtonSelectors = new List<string>
                {
                    // Direct submit button selectors
                    "button[type='submit']",
                    "input[type='submit']",
                    "button[name='submit']",
                    "button[id='submit']",
                    
                    // Login-specific button selectors
                    "button[name='login']",
                    "button[id='login']",
                    "input[name='login']",
                    "input[id='login']",
                    
                    // Case-insensitive attribute matching
                    "button[id*='login' i]",
                    "button[id*='submit' i]",
                    "button[name*='login' i]",
                    "button[name*='submit' i]",
                    "button[class*='login' i]",
                    "button[class*='submit' i]",
                    "input[id*='login' i]",
                    "input[id*='submit' i]",
                    "input[name*='login' i]",
                    "input[name*='submit' i]",
                    "input[class*='login' i]",
                    "input[class*='submit' i]",
                    
                    // Value-based selectors
                    "input[value*='Login' i]",
                    "input[value*='Submit' i]",
                    "input[value*='Sign In' i]",
                    "input[value*='Log In' i]",
                    
                    // ARIA label based selectors
                    "button[aria-label*='login' i]",
                    "button[aria-label*='submit' i]",
                    "button[aria-label*='sign in' i]",
                    "input[aria-label*='login' i]",
                    "input[aria-label*='submit' i]",
                    "input[aria-label*='sign in' i]",
                    
                    // Data attribute selectors
                    "button[data-testid*='login' i]",
                    "button[data-testid*='submit' i]",
                    "input[data-testid*='login' i]",
                    "input[data-testid*='submit' i]",
                    "button[data-cy*='login' i]",
                    "button[data-cy*='submit' i]",
                    "input[data-cy*='login' i]",
                    "input[data-cy*='submit' i]",
                    
                    // Link-based selectors for non-standard implementations
                    "a[href*='login' i]",
                    "a[id*='login' i]",
                    "a[class*='login' i]",
                    "a[data-testid*='login' i]",
                    
                    // Form-based fallback selectors
                    "form button:last-of-type",
                    ".login-form button:last-of-type",
                    "#login-form button:last-of-type",
                    "form input[type='submit']:last-of-type",
                    ".login-form input[type='submit']:last-of-type",
                    "#login-form input[type='submit']:last-of-type"
                }
            });

            // Enhanced configuration for 10.22.11.2:10001 login pages with test-server-specific selectors
            _configurations.Add(new LoginPageConfiguration
            {
                UrlPattern = "10.22.11.2:10001",
                Priority = 10,
                DisplayName = "Test Server Login Pages",
                UsernameSelectors = new List<string>
                {
                    // High-priority test server specific selectors
                    "input[name='username']",
                    "input[id='username']",
                    "input[name='user']",
                    "input[id='user']",
                    "input[name='login']",
                    "input[id='login']",
                    
                    // Form-position based selectors for test pages
                    "form input[type='text']:first-of-type",
                    "form input[type='text']:nth-of-type(1)",
                    
                    // Generic text field selectors with higher priority for test environment
                    "input[type='text']",
                    "input[type='email']",
                    
                    // Fallback selectors for dynamic or obfuscated test pages
                    "input:not([type='password']):not([type='hidden']):not([type='submit']):not([type='button'])",
                    
                    // Container-based selectors for test pages
                    ".login input[type='text']",
                    "#login input[type='text']",
                    ".form input[type='text']",
                    "#form input[type='text']",
                    
                    // Attribute-wildcard selectors for test variations
                    "input[name*='user']",
                    "input[id*='user']",
                    "input[placeholder*='user']",
                    "input[name*='email']",
                    "input[id*='email']",
                    "input[placeholder*='email']"
                },
                PasswordSelectors = new List<string>
                {
                    // High-priority test server specific selectors
                    "input[type='password']",
                    "input[name='password']",
                    "input[id='password']",
                    "input[name='pass']",
                    "input[id='pass']",
                    "input[name='pwd']",
                    "input[id='pwd']",
                    
                    // Container-based selectors for test pages
                    ".login input[type='password']",
                    "#login input[type='password']",
                    ".form input[type='password']",
                    "#form input[type='password']",
                    
                    // Attribute-wildcard selectors for test variations
                    "input[name*='pass']",
                    "input[id*='pass']",
                    "input[placeholder*='pass']"
                },
                DomainSelectors = new List<string>
                {
                    // High-priority test server specific selectors
                    "input[name='domain']",
                    "input[id='domain']",
                    "select[name='domain']",
                    "select[id='domain']",
                    "input[name='tenant']",
                    "input[id='tenant']",
                    "select[name='tenant']",
                    "select[id='tenant']",
                    
                    // Form-position based selectors (domain is often third field)
                    "form input[type='text']:nth-of-type(3)",
                    "form input[type='text']:last-of-type",
                    "form select:first-of-type",
                    
                    // Container-based selectors
                    ".login input[name*='domain']",
                    "#login input[name*='domain']",
                    ".login select[name*='domain']",
                    "#login select[name*='domain']",
                    
                    // Attribute-wildcard selectors
                    "input[name*='domain']",
                    "input[id*='domain']",
                    "input[placeholder*='domain']",
                    "select[name*='domain']",
                    "select[id*='domain']"
                },
                SubmitButtonSelectors = new List<string>
                {
                    // High-priority test server specific selectors
                    "button[type='submit']",
                    "input[type='submit']",
                    "button[name='submit']",
                    "button[id='submit']",
                    "button[name='login']",
                    "button[id='login']",
                    "input[name='login']",
                    "input[id='login']",
                    
                    // Value-based selectors for test pages
                    "input[value='Login']",
                    "input[value='Submit']",
                    "input[value='Sign In']",
                    "input[value='Log In']",
                    
                    // Form-position based selectors
                    "form button:last-of-type",
                    "form input[type='submit']:last-of-type",
                    "form button:only-of-type",
                    "form input[type='submit']:only-of-type",
                    
                    // Container-based selectors
                    ".login button",
                    "#login button",
                    ".form button",
                    "#form button",
                    ".login input[type='submit']",
                    "#login input[type='submit']",
                    ".form input[type='submit']",
                    "#form input[type='submit']",
                    
                    // Attribute-wildcard selectors
                    "button[name*='login']",
                    "button[id*='login']",
                    "button[class*='login']",
                    "input[name*='login']",
                    "input[id*='login']",
                    "input[class*='login']",
                    
                    // Generic button fallbacks for test environment
                    "button",
                    "input[type='submit']",
                    "input[type='button']"
                },
                AdditionalWaitMs = 2000,
                SuccessIndicators = new List<string>
                {
                    // Test-server specific success indicators
                    "//div[contains(text(), 'Welcome')]",
                    "//div[contains(text(), 'Success')]",
                    "//div[contains(text(), 'Logged in')]",
                    "//div[contains(text(), 'Dashboard')]",
                    "//p[contains(text(), 'Welcome')]",
                    "//p[contains(text(), 'Success')]",
                    "//p[contains(text(), 'Logged in')]",
                    "//span[contains(text(), 'Welcome')]",
                    "//span[contains(text(), 'Success')]",
                    "//span[contains(text(), 'Logged in')]",
                    
                    // Generic success patterns
                    ".success",
                    "#success",
                    ".welcome",
                    "#welcome",
                    ".dashboard",
                    "#dashboard",
                    ".logged-in",
                    "#logged-in",
                    
                    // Links that appear after login
                    "a[href*='logout']",
                    "a[href*='signout']",
                    "a[href*='dashboard']",
                    "a[href*='profile']",
                    "a[href*='account']",
                    
                    // Buttons that appear after login
                    "button[name*='logout']",
                    "button[id*='logout']",
                    "button[class*='logout']",
                    "input[name*='logout']",
                    "input[id*='logout']",
                    "input[class*='logout']"
                },
                FailureIndicators = new List<string>
                {
                    // Test-server specific error indicators
                    "//div[contains(text(), 'Error')]",
                    "//div[contains(text(), 'Failed')]",
                    "//div[contains(text(), 'Invalid')]",
                    "//div[contains(text(), 'Incorrect')]",
                    "//div[contains(text(), 'Wrong')]",
                    "//p[contains(text(), 'Error')]",
                    "//p[contains(text(), 'Failed')]",
                    "//p[contains(text(), 'Invalid')]",
                    "//p[contains(text(), 'Incorrect')]",
                    "//p[contains(text(), 'Wrong')]",
                    "//span[contains(text(), 'Error')]",
                    "//span[contains(text(), 'Failed')]",
                    "//span[contains(text(), 'Invalid')]",
                    "//span[contains(text(), 'Incorrect')]",
                    "//span[contains(text(), 'Wrong')]",
                    
                    // CSS class-based error indicators
                    ".error",
                    "#error",
                    ".alert",
                    "#alert",
                    ".danger",
                    "#danger",
                    ".warning",
                    "#warning",
                    ".failed",
                    "#failed",
                    ".invalid",
                    "#invalid",
                    ".incorrect",
                    "#incorrect",
                    
                    // Form validation indicators
                    ".field-error",
                    ".form-error",
                    ".validation-error",
                    ".input-error",
                    "input.error",
                    "input.invalid",
                    
                    // Bootstrap/common framework error classes
                    ".alert-danger",
                    ".alert-error",
                    ".has-error",
                    ".is-invalid",
                    ".text-danger",
                    ".text-error"
                },
                Notes = "Test server pages with enhanced selectors for robust detection across varying form structures. Includes comprehensive success/failure indicators."
            });

            // TODO: Add specific configurations for each individual URL (login2.htm, login3.htm, etc.)
            // These can be added based on DOM analysis results from enhanced logging
        }
    }
} 