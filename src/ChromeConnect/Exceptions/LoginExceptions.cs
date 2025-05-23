using System;
using System.Runtime.Serialization;

namespace ChromeConnect.Exceptions
{
    /// <summary>
    /// Base exception class for all login-related exceptions.
    /// </summary>
    [Serializable]
    public class LoginException : ChromeConnectException
    {
        public LoginException() : base() { }
        public LoginException(string message) : base(message) { }
        public LoginException(string message, Exception innerException) : base(message, innerException) { }
        public LoginException(string message, string errorCode, string context = null) : base(message, errorCode, context) { }
        public LoginException(string message, string errorCode, string context, Exception innerException) : base(message, errorCode, context, innerException) { }
        protected LoginException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Exception thrown when login form elements cannot be found on the page.
    /// </summary>
    [Serializable]
    public class LoginFormNotFoundException : LoginException
    {
        /// <summary>
        /// Gets the URL of the page where the login form was expected.
        /// </summary>
        public string PageUrl { get; }

        public LoginFormNotFoundException() : base() { }
        
        public LoginFormNotFoundException(string message, string pageUrl) 
            : base(message, "LOGIN_FORM_001", $"Page URL: {pageUrl}")
        {
            PageUrl = pageUrl;
        }
        
        public LoginFormNotFoundException(string message, string pageUrl, Exception innerException) 
            : base(message, "LOGIN_FORM_001", $"Page URL: {pageUrl}", innerException)
        {
            PageUrl = pageUrl;
        }
        
        protected LoginFormNotFoundException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            PageUrl = info.GetString(nameof(PageUrl));
        }
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(PageUrl), PageUrl);
            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// Exception thrown when there is an error entering credentials into a login form.
    /// </summary>
    [Serializable]
    public class CredentialEntryException : LoginException
    {
        /// <summary>
        /// Gets the type of field that caused the error (e.g., "username", "password").
        /// </summary>
        public string FieldType { get; }

        public CredentialEntryException() : base() { }
        
        public CredentialEntryException(string message, string fieldType) 
            : base(message, "CREDENTIAL_ENTRY_001", $"Field Type: {fieldType}")
        {
            FieldType = fieldType;
        }
        
        public CredentialEntryException(string message, string fieldType, Exception innerException) 
            : base(message, "CREDENTIAL_ENTRY_001", $"Field Type: {fieldType}", innerException)
        {
            FieldType = fieldType;
        }
        
        protected CredentialEntryException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            FieldType = info.GetString(nameof(FieldType));
        }
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(FieldType), FieldType);
            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// Exception thrown when there is an error verifying the login result.
    /// </summary>
    [Serializable]
    public class LoginVerificationException : LoginException
    {
        /// <summary>
        /// Gets a value indicating whether any error messages were found on the page.
        /// </summary>
        public bool ErrorMessagesFound { get; }
        
        /// <summary>
        /// Gets any error messages found on the page.
        /// </summary>
        public string[] ErrorMessages { get; }

        public LoginVerificationException() : base() { }
        
        public LoginVerificationException(string message) 
            : base(message, "LOGIN_VERIFY_001")
        {
            ErrorMessagesFound = false;
            ErrorMessages = Array.Empty<string>();
        }
        
        public LoginVerificationException(string message, string[] errorMessages) 
            : base(message, "LOGIN_VERIFY_001", $"Error Messages: {string.Join(", ", errorMessages)}")
        {
            ErrorMessagesFound = errorMessages != null && errorMessages.Length > 0;
            ErrorMessages = errorMessages ?? Array.Empty<string>();
        }
        
        public LoginVerificationException(string message, string[] errorMessages, Exception innerException) 
            : base(message, "LOGIN_VERIFY_001", $"Error Messages: {string.Join(", ", errorMessages ?? Array.Empty<string>())}", innerException)
        {
            ErrorMessagesFound = errorMessages != null && errorMessages.Length > 0;
            ErrorMessages = errorMessages ?? Array.Empty<string>();
        }
        
        protected LoginVerificationException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            ErrorMessagesFound = info.GetBoolean(nameof(ErrorMessagesFound));
            ErrorMessages = (string[])info.GetValue(nameof(ErrorMessages), typeof(string[]));
        }
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(ErrorMessagesFound), ErrorMessagesFound);
            info.AddValue(nameof(ErrorMessages), ErrorMessages);
            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// Exception thrown when a login attempt fails due to incorrect credentials.
    /// </summary>
    [Serializable]
    public class InvalidCredentialsException : LoginException
    {
        /// <summary>
        /// Gets a list of error messages from the login page.
        /// </summary>
        public string[] ErrorMessages { get; }

        public InvalidCredentialsException() : base() { }
        
        public InvalidCredentialsException(string message) 
            : base(message, "INVALID_CREDS_001")
        {
            ErrorMessages = Array.Empty<string>();
        }
        
        public InvalidCredentialsException(string message, string[] errorMessages) 
            : base(message, "INVALID_CREDS_001", $"Error Messages: {string.Join(", ", errorMessages ?? Array.Empty<string>())}")
        {
            ErrorMessages = errorMessages ?? Array.Empty<string>();
        }
        
        protected InvalidCredentialsException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            ErrorMessages = (string[])info.GetValue(nameof(ErrorMessages), typeof(string[]));
        }
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(ErrorMessages), ErrorMessages);
            base.GetObjectData(info, context);
        }
    }
} 