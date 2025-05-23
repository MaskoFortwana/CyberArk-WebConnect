using System;
using System.Runtime.Serialization;

namespace ChromeConnect.Exceptions
{
    /// <summary>
    /// Base exception class for all ChromeConnect specific exceptions.
    /// Provides common functionality and properties for all exception types.
    /// </summary>
    [Serializable]
    public class ChromeConnectException : Exception
    {
        /// <summary>
        /// Gets the timestamp when the exception occurred.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;        /// <summary>
        /// Gets or sets an error code associated with this exception.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets additional context information about the error.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromeConnectException"/> class.
        /// </summary>
        public ChromeConnectException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromeConnectException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ChromeConnectException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromeConnectException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ChromeConnectException(string message, Exception innerException) 
            : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromeConnectException"/> class with a specified error message,
        /// error code, and context information.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorCode">A code that identifies the error type.</param>
        /// <param name="context">Additional contextual information about the error.</param>
        public ChromeConnectException(string message, string errorCode, string? context = null) 
            : base(message)
        {
            ErrorCode = errorCode;
            Context = context;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromeConnectException"/> class with a specified error message,
        /// error code, context information, and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorCode">A code that identifies the error type.</param>
        /// <param name="context">Additional contextual information about the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ChromeConnectException(string message, string errorCode, string context, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Context = context;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromeConnectException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The object that holds the serialized object data.</param>
        /// <param name="context">The contextual information about the source or destination.</param>
        protected ChromeConnectException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            // Deserialize any additional fields from info
            ErrorCode = info.GetString(nameof(ErrorCode));
            Context = info.GetString(nameof(Context));
            Timestamp = info.GetDateTime(nameof(Timestamp));
        }        /// <summary>
        /// When overridden in a derived class, sets the <see cref="SerializationInfo"/> with information about the exception.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        [Obsolete("This method overrides an obsolete member in Exception. It may be removed in a future release.", DiagnosticId = "SYSLIB0051")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            // Serialize additional fields to info
            info.AddValue(nameof(ErrorCode), ErrorCode);
            info.AddValue(nameof(Context), Context);
            info.AddValue(nameof(Timestamp), Timestamp);

            base.GetObjectData(info, context);
        }

        /// <summary>
        /// Returns a string representation of the exception, including the error code and timestamp.
        /// </summary>
        /// <returns>A string representation of the exception.</returns>
        public override string ToString()
        {
            string errorCodeInfo = !string.IsNullOrEmpty(ErrorCode) ? $" [Error Code: {ErrorCode}]" : string.Empty;
            string contextInfo = !string.IsNullOrEmpty(Context) ? $" [Context: {Context}]" : string.Empty;
            
            return $"{GetType().Name}: {Message}{errorCodeInfo}{contextInfo} [Time: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}]{Environment.NewLine}{base.ToString()}";
        }
    }
} 