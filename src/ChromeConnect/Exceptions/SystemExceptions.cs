using System;
using System.Runtime.Serialization;

namespace ChromeConnect.Exceptions
{
    /// <summary>
    /// Base exception class for all system-related exceptions.
    /// </summary>
    [Serializable]
    public class AppSystemException : ChromeConnectException
    {
        public AppSystemException() : base() { }
        public AppSystemException(string message) : base(message) { }
        public AppSystemException(string message, Exception innerException) : base(message, innerException) { }
        public AppSystemException(string message, string errorCode, string context = null) : base(message, errorCode, context) { }
        public AppSystemException(string message, string errorCode, string context, Exception innerException) : base(message, errorCode, context, innerException) { }
        protected AppSystemException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Exception thrown when there is a configuration error.
    /// </summary>
    [Serializable]
    public class ConfigurationException : AppSystemException
    {
        /// <summary>
        /// Gets the name of the configuration parameter that caused the error.
        /// </summary>
        public string ParameterName { get; }

        public ConfigurationException() : base() { }
        
        public ConfigurationException(string message, string parameterName) 
            : base(message, "CONFIG_001", $"Parameter: {parameterName}")
        {
            ParameterName = parameterName;
        }
        
        public ConfigurationException(string message, string parameterName, Exception innerException) 
            : base(message, "CONFIG_001", $"Parameter: {parameterName}", innerException)
        {
            ParameterName = parameterName;
        }
        
        protected ConfigurationException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            ParameterName = info.GetString(nameof(ParameterName));
        }
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(ParameterName), ParameterName);
            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// Exception thrown when there is an error with file operations.
    /// </summary>
    [Serializable]
    public class FileOperationException : AppSystemException
    {
        /// <summary>
        /// Gets the path of the file that caused the error.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the type of file operation that failed.
        /// </summary>
        public string OperationType { get; }

        public FileOperationException() : base() { }
        
        public FileOperationException(string message, string filePath, string operationType) 
            : base(message, "FILE_OP_001", $"File: {filePath}, Operation: {operationType}")
        {
            FilePath = filePath;
            OperationType = operationType;
        }
        
        public FileOperationException(string message, string filePath, string operationType, Exception innerException) 
            : base(message, "FILE_OP_001", $"File: {filePath}, Operation: {operationType}", innerException)
        {
            FilePath = filePath;
            OperationType = operationType;
        }
        
        protected FileOperationException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            FilePath = info.GetString(nameof(FilePath));
            OperationType = info.GetString(nameof(OperationType));
        }
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(FilePath), FilePath);
            info.AddValue(nameof(OperationType), OperationType);
            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// Exception thrown when a required resource is not available.
    /// </summary>
    [Serializable]
    public class ResourceNotFoundException : AppSystemException
    {
        /// <summary>
        /// Gets the name of the resource that was not found.
        /// </summary>
        public string ResourceName { get; }

        /// <summary>
        /// Gets the path where the resource was expected to be found.
        /// </summary>
        public string ResourcePath { get; }

        public ResourceNotFoundException() : base() { }
        
        public ResourceNotFoundException(string message, string resourceName, string resourcePath = null) 
            : base(message, "RESOURCE_NOT_FOUND_001", FormatContext(resourceName, resourcePath))
        {
            ResourceName = resourceName;
            ResourcePath = resourcePath;
        }
        
        public ResourceNotFoundException(string message, string resourceName, string resourcePath, Exception innerException) 
            : base(message, "RESOURCE_NOT_FOUND_001", FormatContext(resourceName, resourcePath), innerException)
        {
            ResourceName = resourceName;
            ResourcePath = resourcePath;
        }
        
        protected ResourceNotFoundException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            ResourceName = info.GetString(nameof(ResourceName));
            ResourcePath = info.GetString(nameof(ResourcePath));
        }
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(ResourceName), ResourceName);
            info.AddValue(nameof(ResourcePath), ResourcePath);
            base.GetObjectData(info, context);
        }

        private static string FormatContext(string resourceName, string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return $"Resource: {resourceName}";
            
            return $"Resource: {resourceName}, Path: {resourcePath}";
        }
    }

    /// <summary>
    /// Exception thrown when an operation is canceled.
    /// </summary>
    [Serializable]
    public class AppOperationCanceledException : AppSystemException
    {
        /// <summary>
        /// Gets the name of the operation that was canceled.
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Gets the time when the operation was canceled.
        /// </summary>
        public DateTime CancelTime { get; }

        public AppOperationCanceledException() : base() 
        {
            CancelTime = DateTime.UtcNow;
        }
        
        public AppOperationCanceledException(string message, string operationName) 
            : base(message, "OPERATION_CANCELED_001", $"Operation: {operationName}, Time: {DateTime.UtcNow:O}")
        {
            OperationName = operationName;
            CancelTime = DateTime.UtcNow;
        }
        
        public AppOperationCanceledException(string message, string operationName, Exception innerException) 
            : base(message, "OPERATION_CANCELED_001", $"Operation: {operationName}, Time: {DateTime.UtcNow:O}", innerException)
        {
            OperationName = operationName;
            CancelTime = DateTime.UtcNow;
        }
        
        protected AppOperationCanceledException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            OperationName = info.GetString(nameof(OperationName));
            CancelTime = info.GetDateTime(nameof(CancelTime));
        }
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(OperationName), OperationName);
            info.AddValue(nameof(CancelTime), CancelTime);
            base.GetObjectData(info, context);
        }
    }
} 