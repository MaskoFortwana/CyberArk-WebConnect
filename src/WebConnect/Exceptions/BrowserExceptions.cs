using System;
using System.Runtime.Serialization;

namespace WebConnect.Exceptions
{
    /// <summary>
    /// Base exception class for all browser-related exceptions.
    /// </summary>
    [Serializable]
    public class BrowserException : WebConnectException
    {
        public BrowserException() : base() { }
        public BrowserException(string message) : base(message) { }
        public BrowserException(string message, Exception innerException) : base(message, innerException) { }
        public BrowserException(string message, string errorCode, string? context = null) : base(message, errorCode, context ?? string.Empty) { }
        public BrowserException(string message, string errorCode, string context, Exception innerException) : base(message, errorCode, context, innerException) { }
        protected BrowserException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Exception thrown when the browser cannot be initialized properly.
    /// </summary>
    [Serializable]
    public class BrowserInitializationException : BrowserException
    {
        public BrowserInitializationException() : base() { }
        
        public BrowserInitializationException(string message) 
            : base(message, "BROWSER_INIT_001") { }
        
        public BrowserInitializationException(string message, Exception innerException) 
            : base(message, "BROWSER_INIT_001", string.Empty, innerException) { }
        
        public BrowserInitializationException(string message, string context) 
            : base(message, "BROWSER_INIT_001", context) { }
        
        public BrowserInitializationException(string message, string context, Exception innerException) 
            : base(message, "BROWSER_INIT_001", context, innerException) { }
        
        protected BrowserInitializationException(SerializationInfo info, StreamingContext context) 
            : base(info, context) { }
    }
    
    /// <summary>
    /// Exception thrown when a Chrome driver executable is missing or cannot be found.
    /// </summary>
    [Serializable]
    public class ChromeDriverMissingException : BrowserException
    {
        public ChromeDriverMissingException() : base() { }
        
        public ChromeDriverMissingException(string message) 
            : base(message, "CHROME_DRIVER_001") { }
        
        public ChromeDriverMissingException(string message, Exception innerException) 
            : base(message, "CHROME_DRIVER_001", string.Empty, innerException) { }
        
        public ChromeDriverMissingException(string message, string driverPath) 
            : base(message, "CHROME_DRIVER_001", $"Driver Path: {driverPath}") { }
        
        public ChromeDriverMissingException(string message, string driverPath, Exception innerException) 
            : base(message, "CHROME_DRIVER_001", $"Driver Path: {driverPath}", innerException) { }
        
        protected ChromeDriverMissingException(SerializationInfo info, StreamingContext context) 
            : base(info, context) { }
    }
    
    /// <summary>
    /// Exception thrown when there is a browser navigation error.
    /// </summary>
    [Serializable]
    public class BrowserNavigationException : BrowserException
    {
        /// <summary>
        /// Gets the URL that the browser was trying to navigate to when the error occurred.
        /// </summary>
        public string TargetUrl { get; }
        
        public BrowserNavigationException() : base() 
        {
            TargetUrl = "Unknown URL";
        }
        
        public BrowserNavigationException(string message, string targetUrl) 
            : base(message, "BROWSER_NAV_001", $"Target URL: {targetUrl}")
        {
            TargetUrl = targetUrl;
        }
        
        public BrowserNavigationException(string message, string targetUrl, Exception innerException) 
            : base(message, "BROWSER_NAV_001", $"Target URL: {targetUrl}", innerException)
        {
            TargetUrl = targetUrl;
        }
        
        protected BrowserNavigationException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            TargetUrl = info.GetString(nameof(TargetUrl)) ?? "Unknown URL";
        }
          [Obsolete("This method overrides an obsolete member in Exception. It may be removed in a future release.", DiagnosticId = "SYSLIB0051")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(TargetUrl), TargetUrl);
            base.GetObjectData(info, context);
        }
    }
    
    /// <summary>
    /// Exception thrown when a timeout occurs during browser operations.
    /// </summary>
    [Serializable]
    public class BrowserTimeoutException : BrowserException
    {
        /// <summary>
        /// Gets the duration of the timeout in milliseconds.
        /// </summary>
        public int TimeoutMilliseconds { get; }
        
        /// <summary>
        /// Gets the name of the operation that timed out.
        /// </summary>
        public string Operation { get; }
        
        public BrowserTimeoutException() : base() 
        {
            Operation = "Unknown Operation";
            // TimeoutMilliseconds is int, defaults to 0 which is acceptable.
        }
        
        public BrowserTimeoutException(string message, string operation, int timeoutMilliseconds) 
            : base(message, "BROWSER_TIMEOUT_001", $"Operation: {operation}, Timeout: {timeoutMilliseconds}ms")
        {
            Operation = operation;
            TimeoutMilliseconds = timeoutMilliseconds;
        }
        
        public BrowserTimeoutException(string message, string operation, int timeoutMilliseconds, Exception innerException) 
            : base(message, "BROWSER_TIMEOUT_001", $"Operation: {operation}, Timeout: {timeoutMilliseconds}ms", innerException)
        {
            Operation = operation;
            TimeoutMilliseconds = timeoutMilliseconds;
        }
        
        protected BrowserTimeoutException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            Operation = info.GetString(nameof(Operation)) ?? "Unknown Operation";
            TimeoutMilliseconds = info.GetInt32(nameof(TimeoutMilliseconds));
        }
        
        [Obsolete("This method overrides an obsolete member in Exception. It may be removed in a future release.", DiagnosticId = "SYSLIB0051")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(Operation), Operation);
            info.AddValue(nameof(TimeoutMilliseconds), TimeoutMilliseconds);
            base.GetObjectData(info, context);
        }
    }
} 
