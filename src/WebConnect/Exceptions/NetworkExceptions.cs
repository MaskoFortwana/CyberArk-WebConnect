using System;
using System.Runtime.Serialization;

namespace WebConnect.Exceptions
{
    /// <summary>
    /// Base exception class for all network-related exceptions.
    /// </summary>
    [Serializable]
    public class NetworkException : WebConnectException
    {
        public NetworkException() : base() { }
        public NetworkException(string message) : base(message) { }
        public NetworkException(string message, Exception innerException) : base(message, innerException) { }
        public NetworkException(string message, string errorCode, string? context = null) : base(message, errorCode, context ?? string.Empty) { }
        public NetworkException(string message, string errorCode, string context, Exception innerException) : base(message, errorCode, context, innerException) { }
        protected NetworkException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Exception thrown when a network connection cannot be established.
    /// </summary>
    [Serializable]
    public class ConnectionFailedException : NetworkException
    {
        /// <summary>
        /// Gets the URL that was being connected to.
        /// </summary>
        public string TargetUrl { get; }

        public ConnectionFailedException() : base() 
        {
            TargetUrl = "Unknown URL";
        }
        
        public ConnectionFailedException(string message, string targetUrl) 
            : base(message, "CONNECTION_FAILED_001", $"Target URL: {targetUrl}")
        {
            TargetUrl = targetUrl;
        }
        
        public ConnectionFailedException(string message, string targetUrl, Exception innerException) 
            : base(message, "CONNECTION_FAILED_001", $"Target URL: {targetUrl}", innerException)
        {
            TargetUrl = targetUrl;
        }
        
        protected ConnectionFailedException(SerializationInfo info, StreamingContext context) 
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
    /// Exception thrown when a certificate validation error occurs.
    /// </summary>
    [Serializable]
    public class CertificateException : NetworkException
    {
        /// <summary>
        /// Gets the URL associated with the certificate.
        /// </summary>
        public string TargetUrl { get; }
        
        /// <summary>
        /// Gets the certificate subject.
        /// </summary>
        public string? CertificateSubject { get; }
        
        /// <summary>
        /// Gets the certificate validation errors.
        /// </summary>
        public string? ValidationErrors { get; }

        public CertificateException() : base() 
        {
            TargetUrl = "Unknown URL";
            CertificateSubject = null;
            ValidationErrors = null;
        }
        
        public CertificateException(string message, string targetUrl, string? certificateSubject = null, string? validationErrors = null) 
            : base(message, "CERTIFICATE_001", FormatContext(targetUrl, certificateSubject, validationErrors))
        {
            TargetUrl = targetUrl;
            CertificateSubject = certificateSubject;
            ValidationErrors = validationErrors;
        }
        
        public CertificateException(string message, string targetUrl, string certificateSubject, string validationErrors, Exception innerException) 
            : base(message, "CERTIFICATE_001", FormatContext(targetUrl, certificateSubject, validationErrors), innerException)
        {
            TargetUrl = targetUrl;
            CertificateSubject = certificateSubject;
            ValidationErrors = validationErrors;
        }
        
        protected CertificateException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            TargetUrl = info.GetString(nameof(TargetUrl)) ?? "Unknown URL";
            CertificateSubject = info.GetString(nameof(CertificateSubject));
            ValidationErrors = info.GetString(nameof(ValidationErrors));
        }
        
        [Obsolete("This method overrides an obsolete member in Exception. It may be removed in a future release.", DiagnosticId = "SYSLIB0051")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(TargetUrl), TargetUrl);
            info.AddValue(nameof(CertificateSubject), CertificateSubject);
            info.AddValue(nameof(ValidationErrors), ValidationErrors);
            base.GetObjectData(info, context);
        }
        
        private static string FormatContext(string targetUrl, string certificateSubject, string validationErrors)
        {
            string context = $"Target URL: {targetUrl}";
            
            if (!string.IsNullOrEmpty(certificateSubject))
                context += $", Certificate: {certificateSubject}";
                
            if (!string.IsNullOrEmpty(validationErrors))
                context += $", Validation Errors: {validationErrors}";
                
            return context;
        }
    }

    /// <summary>
    /// Exception thrown when a network request times out.
    /// </summary>
    [Serializable]
    public class RequestTimeoutException : NetworkException
    {
        /// <summary>
        /// Gets the URL that was requested.
        /// </summary>
        public string RequestUrl { get; }
        
        /// <summary>
        /// Gets the timeout duration in milliseconds.
        /// </summary>
        public int TimeoutMilliseconds { get; }

        public RequestTimeoutException() : base() 
        {
            RequestUrl = "Unknown URL";
        }
        
        public RequestTimeoutException(string message, string requestUrl, int timeoutMilliseconds) 
            : base(message, "REQUEST_TIMEOUT_001", $"Request URL: {requestUrl}, Timeout: {timeoutMilliseconds}ms")
        {
            RequestUrl = requestUrl;
            TimeoutMilliseconds = timeoutMilliseconds;
        }
        
        public RequestTimeoutException(string message, string requestUrl, int timeoutMilliseconds, Exception innerException) 
            : base(message, "REQUEST_TIMEOUT_001", $"Request URL: {requestUrl}, Timeout: {timeoutMilliseconds}ms", innerException)
        {
            RequestUrl = requestUrl;
            TimeoutMilliseconds = timeoutMilliseconds;
        }
        
        protected RequestTimeoutException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            RequestUrl = info.GetString(nameof(RequestUrl)) ?? "Unknown URL";
            TimeoutMilliseconds = info.GetInt32(nameof(TimeoutMilliseconds));
        }
        
        [Obsolete("This method overrides an obsolete member in Exception. It may be removed in a future release.", DiagnosticId = "SYSLIB0051")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(RequestUrl), RequestUrl);
            info.AddValue(nameof(TimeoutMilliseconds), TimeoutMilliseconds);
            base.GetObjectData(info, context);
        }
    }
} 
