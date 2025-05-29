using System;

namespace WebConnect.Models
{
    /// <summary>
    /// Represents login credentials for authentication.
    /// </summary>
    public class LoginCredentials
    {
        /// <summary>
        /// Gets or sets the username for authentication.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password for authentication.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the domain or tenant identifier for authentication.
        /// </summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the LoginCredentials class.
        /// </summary>
        public LoginCredentials()
        {
        }

        /// <summary>
        /// Initializes a new instance of the LoginCredentials class with specified values.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="domain">The domain (optional).</param>
        public LoginCredentials(string username, string password, string domain = "")
        {
            Username = username;
            Password = password;
            Domain = domain;
        }
    }
} 
