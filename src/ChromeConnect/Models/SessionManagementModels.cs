using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ChromeConnect.Models
{
    /// <summary>
    /// Represents different session storage types.
    /// </summary>
    public enum SessionStorageType
    {
        /// <summary>
        /// Browser cookies.
        /// </summary>
        Cookies,
        
        /// <summary>
        /// Browser localStorage.
        /// </summary>
        LocalStorage,
        
        /// <summary>
        /// Browser sessionStorage.
        /// </summary>
        SessionStorage,
        
        /// <summary>
        /// In-memory storage (not persistent).
        /// </summary>
        Memory,
        
        /// <summary>
        /// File-based storage.
        /// </summary>
        File,
        
        /// <summary>
        /// Database storage.
        /// </summary>
        Database
    }

    /// <summary>
    /// Represents the current state of a session.
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// Session state is unknown.
        /// </summary>
        Unknown,
        
        /// <summary>
        /// Session is active and valid.
        /// </summary>
        Active,
        
        /// <summary>
        /// Session has expired.
        /// </summary>
        Expired,
        
        /// <summary>
        /// Session is invalid.
        /// </summary>
        Invalid,
        
        /// <summary>
        /// Session is being refreshed.
        /// </summary>
        Refreshing,
        
        /// <summary>
        /// Session recovery is in progress.
        /// </summary>
        Recovering,
        
        /// <summary>
        /// Session was terminated.
        /// </summary>
        Terminated
    }

    /// <summary>
    /// Represents session validation results.
    /// </summary>
    public enum SessionValidationResult
    {
        /// <summary>
        /// Session is valid.
        /// </summary>
        Valid,
        
        /// <summary>
        /// Session has expired.
        /// </summary>
        Expired,
        
        /// <summary>
        /// Session is invalid.
        /// </summary>
        Invalid,
        
        /// <summary>
        /// Session validation failed due to error.
        /// </summary>
        ValidationFailed,
        
        /// <summary>
        /// Session not found.
        /// </summary>
        NotFound
    }

    /// <summary>
    /// Represents different session refresh strategies.
    /// </summary>
    public enum SessionRefreshStrategy
    {
        /// <summary>
        /// No automatic refresh.
        /// </summary>
        None,
        
        /// <summary>
        /// Refresh at fixed intervals.
        /// </summary>
        FixedInterval,
        
        /// <summary>
        /// Refresh when session nears expiry.
        /// </summary>
        OnExpiry,
        
        /// <summary>
        /// Refresh on user activity.
        /// </summary>
        OnActivity,
        
        /// <summary>
        /// Adaptive refresh based on usage patterns.
        /// </summary>
        Adaptive
    }

    /// <summary>
    /// Configuration for session management behaviors.
    /// </summary>
    public class SessionManagementConfiguration
    {
        /// <summary>
        /// Gets or sets the default session timeout in minutes.
        /// </summary>
        public int DefaultSessionTimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the session validation interval in minutes.
        /// </summary>
        public int ValidationIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets the preferred storage type for sessions.
        /// </summary>
        public SessionStorageType PreferredStorageType { get; set; } = SessionStorageType.Cookies;

        /// <summary>
        /// Gets or sets the fallback storage types in order of preference.
        /// </summary>
        public List<SessionStorageType> FallbackStorageTypes { get; set; } = new()
        {
            SessionStorageType.LocalStorage,
            SessionStorageType.SessionStorage,
            SessionStorageType.Memory
        };

        /// <summary>
        /// Gets or sets whether to enable automatic session refresh.
        /// </summary>
        public bool EnableAutoRefresh { get; set; } = true;

        /// <summary>
        /// Gets or sets the session refresh strategy.
        /// </summary>
        public SessionRefreshStrategy RefreshStrategy { get; set; } = SessionRefreshStrategy.OnExpiry;

        /// <summary>
        /// Gets or sets the number of minutes before expiry to trigger refresh.
        /// </summary>
        public int RefreshBeforeExpiryMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets whether to enable session encryption.
        /// </summary>
        public bool EnableEncryption { get; set; } = true;

        /// <summary>
        /// Gets or sets the encryption key for session data.
        /// </summary>
        public string? EncryptionKey { get; set; }

        /// <summary>
        /// Gets or sets whether to enable automatic recovery on session expiry.
        /// </summary>
        public bool EnableAutoRecovery { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of recovery attempts.
        /// </summary>
        public int MaxRecoveryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether to enable detailed logging.
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the session identifier cookie name.
        /// </summary>
        public string SessionCookieName { get; set; } = "chromeconnect_session";

        /// <summary>
        /// Gets or sets additional session validation URLs.
        /// </summary>
        public List<string> ValidationUrls { get; set; } = new();
    }

    /// <summary>
    /// Represents session data and metadata.
    /// </summary>
    public class SessionData
    {
        /// <summary>
        /// Gets or sets the unique session identifier.
        /// </summary>
        [Required]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the session creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the session last accessed timestamp.
        /// </summary>
        public DateTime LastAccessedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the session expiry timestamp.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the current session state.
        /// </summary>
        public SessionState State { get; set; } = SessionState.Unknown;

        /// <summary>
        /// Gets or sets the domain this session is valid for.
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Gets or sets the URL where this session was created.
        /// </summary>
        public string? OriginUrl { get; set; }

        /// <summary>
        /// Gets or sets the user identifier associated with this session.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Gets or sets additional session attributes.
        /// </summary>
        public Dictionary<string, object> Attributes { get; set; } = new();

        /// <summary>
        /// Gets or sets the authentication tokens associated with this session.
        /// </summary>
        public Dictionary<string, string> Tokens { get; set; } = new();

        /// <summary>
        /// Gets or sets session cookies.
        /// </summary>
        public List<SessionCookie> Cookies { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this session data is encrypted.
        /// </summary>
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// Gets or sets the storage type used for this session.
        /// </summary>
        public SessionStorageType StorageType { get; set; }
    }

    /// <summary>
    /// Represents a session cookie.
    /// </summary>
    public class SessionCookie
    {
        /// <summary>
        /// Gets or sets the cookie name.
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cookie value.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cookie domain.
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Gets or sets the cookie path.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets the cookie expiry date.
        /// </summary>
        public DateTime? Expiry { get; set; }

        /// <summary>
        /// Gets or sets whether the cookie is secure.
        /// </summary>
        public bool IsSecure { get; set; }

        /// <summary>
        /// Gets or sets whether the cookie is HTTP only.
        /// </summary>
        public bool IsHttpOnly { get; set; }

        /// <summary>
        /// Gets or sets the SameSite attribute.
        /// </summary>
        public string? SameSite { get; set; }
    }

    /// <summary>
    /// Represents a session validation request.
    /// </summary>
    public class SessionValidationRequest
    {
        /// <summary>
        /// Gets or sets the session ID to validate.
        /// </summary>
        [Required]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL to validate the session against.
        /// </summary>
        public string? ValidationUrl { get; set; }

        /// <summary>
        /// Gets or sets whether to perform deep validation.
        /// </summary>
        public bool DeepValidation { get; set; } = true;

        /// <summary>
        /// Gets or sets the timeout for validation in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Gets or sets additional validation parameters.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Represents the result of a session validation.
    /// </summary>
    public class SessionValidationResult_Model
    {
        /// <summary>
        /// Gets or sets the validation result.
        /// </summary>
        public SessionValidationResult Result { get; set; }

        /// <summary>
        /// Gets or sets whether the validation was successful.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets any error message from validation.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets additional validation details.
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();

        /// <summary>
        /// Gets or sets the validation timestamp.
        /// </summary>
        public DateTime ValidatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the session state after validation.
        /// </summary>
        public SessionState SessionState { get; set; }

        /// <summary>
        /// Gets or sets the estimated time until session expiry.
        /// </summary>
        public TimeSpan? TimeUntilExpiry { get; set; }
    }

    /// <summary>
    /// Represents a session recovery request.
    /// </summary>
    public class SessionRecoveryRequest
    {
        /// <summary>
        /// Gets or sets the expired session ID.
        /// </summary>
        [Required]
        public string ExpiredSessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recovery strategy to use.
        /// </summary>
        public SessionRecoveryStrategy Strategy { get; set; } = SessionRecoveryStrategy.ReAuthenticate;

        /// <summary>
        /// Gets or sets the original login credentials for re-authentication.
        /// </summary>
        public Dictionary<string, string> Credentials { get; set; } = new();

        /// <summary>
        /// Gets or sets the target URL after recovery.
        /// </summary>
        public string? TargetUrl { get; set; }

        /// <summary>
        /// Gets or sets whether to preserve the original session data.
        /// </summary>
        public bool PreserveSessionData { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum recovery timeout in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;
    }

    /// <summary>
    /// Represents different session recovery strategies.
    /// </summary>
    public enum SessionRecoveryStrategy
    {
        /// <summary>
        /// Attempt to refresh the existing session.
        /// </summary>
        Refresh,
        
        /// <summary>
        /// Re-authenticate with stored credentials.
        /// </summary>
        ReAuthenticate,
        
        /// <summary>
        /// Create a new session.
        /// </summary>
        CreateNew,
        
        /// <summary>
        /// Restore from backup session data.
        /// </summary>
        RestoreBackup
    }

    /// <summary>
    /// Represents the result of a session recovery operation.
    /// </summary>
    public class SessionRecoveryResult
    {
        /// <summary>
        /// Gets or sets whether the recovery was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the recovered session data.
        /// </summary>
        public SessionData? RecoveredSession { get; set; }

        /// <summary>
        /// Gets or sets any error message from recovery.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the recovery strategy that was used.
        /// </summary>
        public SessionRecoveryStrategy StrategyUsed { get; set; }

        /// <summary>
        /// Gets or sets the recovery duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the number of recovery attempts made.
        /// </summary>
        public int AttemptsCount { get; set; }

        /// <summary>
        /// Gets or sets additional recovery details.
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// Represents session operation results.
    /// </summary>
    public class SessionOperationResult
    {
        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets any error message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the operation result data.
        /// </summary>
        public object? Data { get; set; }

        /// <summary>
        /// Gets or sets the operation duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets additional operation metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for session state change events.
    /// </summary>
    public class SessionStateChangeEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the session data.
        /// </summary>
        public SessionData Session { get; set; } = new();

        /// <summary>
        /// Gets or sets the previous session state.
        /// </summary>
        public SessionState PreviousState { get; set; }

        /// <summary>
        /// Gets or sets the new session state.
        /// </summary>
        public SessionState NewState { get; set; }

        /// <summary>
        /// Gets or sets the change timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets additional context data.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for session validation events.
    /// </summary>
    public class SessionValidationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the validation request.
        /// </summary>
        public SessionValidationRequest Request { get; set; } = new();

        /// <summary>
        /// Gets or sets the validation result.
        /// </summary>
        public SessionValidationResult_Model Result { get; set; } = new();

        /// <summary>
        /// Gets or sets the session data.
        /// </summary>
        public SessionData? Session { get; set; }
    }

    /// <summary>
    /// Event arguments for session recovery events.
    /// </summary>
    public class SessionRecoveryEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the recovery request.
        /// </summary>
        public SessionRecoveryRequest Request { get; set; } = new();

        /// <summary>
        /// Gets or sets the recovery result.
        /// </summary>
        public SessionRecoveryResult Result { get; set; } = new();

        /// <summary>
        /// Gets or sets the original session data.
        /// </summary>
        public SessionData? OriginalSession { get; set; }
    }
} 