using System;
using OpenQA.Selenium;

namespace WebConnect.Models
{
    /// <summary>
    /// Represents the elements of a login form detected on a webpage.
    /// </summary>
    public class LoginFormElements
    {
        /// <summary>
        /// Gets or sets the username input field element.
        /// </summary>
        public IWebElement? UsernameField { get; set; }

        /// <summary>
        /// Gets or sets the password input field element.
        /// </summary>
        public IWebElement? PasswordField { get; set; }

        /// <summary>
        /// Gets or sets the domain/tenant input field element, if present.
        /// </summary>
        public IWebElement? DomainField { get; set; }

        /// <summary>
        /// Gets or sets the submit button element.
        /// </summary>
        public IWebElement? SubmitButton { get; set; }
    }
} 
