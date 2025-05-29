using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebConnect.Models
{
    /// <summary>
    /// Represents different types of dynamic content wait strategies.
    /// </summary>
    public enum WaitStrategy
    {
        /// <summary>
        /// Wait for DOM to be ready.
        /// </summary>
        DomReady,
        
        /// <summary>
        /// Wait for all network requests to complete.
        /// </summary>
        NetworkIdle,
        
        /// <summary>
        /// Wait for specific element to appear.
        /// </summary>
        ElementPresence,
        
        /// <summary>
        /// Wait for specific element to be clickable.
        /// </summary>
        ElementClickable,
        
        /// <summary>
        /// Wait for AJAX requests to complete.
        /// </summary>
        AjaxComplete,
        
        /// <summary>
        /// Wait for JavaScript function to return true.
        /// </summary>
        CustomCondition,
        
        /// <summary>
        /// Wait for page performance metrics to stabilize.
        /// </summary>
        PerformanceStable,
        
        /// <summary>
        /// Wait for Angular to finish loading.
        /// </summary>
        AngularReady,
        
        /// <summary>
        /// Wait for React to finish rendering.
        /// </summary>
        ReactReady
    }

    /// <summary>
    /// Represents the type of JavaScript event to simulate.
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// Mouse click event.
        /// </summary>
        Click,
        
        /// <summary>
        /// Mouse double-click event.
        /// </summary>
        DoubleClick,
        
        /// <summary>
        /// Mouse hover event.
        /// </summary>
        Hover,
        
        /// <summary>
        /// Key press event.
        /// </summary>
        KeyPress,
        
        /// <summary>
        /// Input change event.
        /// </summary>
        Input,
        
        /// <summary>
        /// Focus event.
        /// </summary>
        Focus,
        
        /// <summary>
        /// Blur event.
        /// </summary>
        Blur,
        
        /// <summary>
        /// Form submit event.
        /// </summary>
        Submit,
        
        /// <summary>
        /// Custom JavaScript event.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Represents the execution result of a JavaScript operation.
    /// </summary>
    public enum JavaScriptExecutionResult
    {
        /// <summary>
        /// Execution was successful.
        /// </summary>
        Success,
        
        /// <summary>
        /// Execution failed with an error.
        /// </summary>
        Error,
        
        /// <summary>
        /// Execution timed out.
        /// </summary>
        Timeout,
        
        /// <summary>
        /// Element not found for interaction.
        /// </summary>
        ElementNotFound,
        
        /// <summary>
        /// JavaScript is disabled or not available.
        /// </summary>
        JavaScriptDisabled
    }

    /// <summary>
    /// Configuration for JavaScript interaction behaviors.
    /// </summary>
    public class JavaScriptInteractionConfiguration
    {
        /// <summary>
        /// Gets or sets the default timeout for JavaScript execution in seconds.
        /// </summary>
        public int DefaultExecutionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the default timeout for dynamic content waiting in seconds.
        /// </summary>
        public int DefaultWaitTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Gets or sets the polling interval for wait conditions in milliseconds.
        /// </summary>
        public int PollingIntervalMs { get; set; } = 500;

        /// <summary>
        /// Gets or sets whether to enable performance monitoring.
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to capture JavaScript errors automatically.
        /// </summary>
        public bool CaptureJavaScriptErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed operations.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay between retry attempts in milliseconds.
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether to enable detailed logging for debugging.
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the network idle timeout in milliseconds.
        /// </summary>
        public int NetworkIdleTimeoutMs { get; set; } = 2000;

        /// <summary>
        /// Gets or sets whether to enable Shadow DOM support.
        /// </summary>
        public bool EnableShadowDomSupport { get; set; } = true;
    }

    /// <summary>
    /// Represents a wait condition for dynamic content.
    /// </summary>
    public class WaitCondition
    {
        /// <summary>
        /// Gets or sets the unique identifier for this wait condition.
        /// </summary>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the wait strategy to use.
        /// </summary>
        public WaitStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets the element selector (for element-based waits).
        /// </summary>
        public string? ElementSelector { get; set; }

        /// <summary>
        /// Gets or sets the custom JavaScript condition (for custom waits).
        /// </summary>
        public string? CustomCondition { get; set; }

        /// <summary>
        /// Gets or sets the timeout for this specific condition in seconds.
        /// </summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets whether this is a critical condition that must pass.
        /// </summary>
        public bool IsCritical { get; set; } = true;

        /// <summary>
        /// Gets or sets additional parameters for the wait condition.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Gets or sets the description of what this condition waits for.
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Represents a JavaScript execution request.
    /// </summary>
    public class JavaScriptExecutionRequest
    {
        /// <summary>
        /// Gets or sets the JavaScript code to execute.
        /// </summary>
        [Required]
        public string Script { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the arguments to pass to the script.
        /// </summary>
        public object[]? Arguments { get; set; }

        /// <summary>
        /// Gets or sets the timeout for execution in seconds.
        /// </summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets whether to return the result.
        /// </summary>
        public bool ReturnResult { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this is an asynchronous script.
        /// </summary>
        public bool IsAsync { get; set; } = false;

        /// <summary>
        /// Gets or sets the description of what this script does.
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Represents the result of a JavaScript execution.
    /// </summary>
    public class JavaScriptOperationResult
    {
        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the execution result.
        /// </summary>
        public JavaScriptExecutionResult ExecutionResult { get; set; }

        /// <summary>
        /// Gets or sets the returned value from the script.
        /// </summary>
        public object? ReturnValue { get; set; }

        /// <summary>
        /// Gets or sets any error message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the execution duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets additional operation data.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        /// <summary>
        /// Gets or sets any JavaScript errors that were captured.
        /// </summary>
        public List<JavaScriptError> JavaScriptErrors { get; set; } = new();
    }

    /// <summary>
    /// Represents a JavaScript error that occurred during execution.
    /// </summary>
    public class JavaScriptError
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source file where the error occurred.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the line number where the error occurred.
        /// </summary>
        public int? LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the column number where the error occurred.
        /// </summary>
        public int? ColumnNumber { get; set; }

        /// <summary>
        /// Gets or sets the error stack trace.
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the error level (error, warning, info).
        /// </summary>
        public string Level { get; set; } = "error";
    }

    /// <summary>
    /// Represents a request to simulate a user event.
    /// </summary>
    public class EventSimulationRequest
    {
        /// <summary>
        /// Gets or sets the type of event to simulate.
        /// </summary>
        public EventType EventType { get; set; }

        /// <summary>
        /// Gets or sets the target element selector.
        /// </summary>
        [Required]
        public string ElementSelector { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional event properties.
        /// </summary>
        public Dictionary<string, object> EventProperties { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to use native browser events.
        /// </summary>
        public bool UseNativeEvents { get; set; } = true;

        /// <summary>
        /// Gets or sets the delay before triggering the event in milliseconds.
        /// </summary>
        public int DelayMs { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether to wait for the event to propagate.
        /// </summary>
        public bool WaitForPropagation { get; set; } = true;
    }

    /// <summary>
    /// Represents performance metrics for a page.
    /// </summary>
    public class PagePerformanceMetrics
    {
        /// <summary>
        /// Gets or sets the DOM content loaded time in milliseconds.
        /// </summary>
        public long DomContentLoadedTime { get; set; }

        /// <summary>
        /// Gets or sets the page load complete time in milliseconds.
        /// </summary>
        public long LoadCompleteTime { get; set; }

        /// <summary>
        /// Gets or sets the number of active network requests.
        /// </summary>
        public int ActiveNetworkRequests { get; set; }

        /// <summary>
        /// Gets or sets the JavaScript heap size in bytes.
        /// </summary>
        public long JavaScriptHeapSize { get; set; }

        /// <summary>
        /// Gets or sets the number of DOM nodes.
        /// </summary>
        public int DomNodeCount { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when metrics were captured.
        /// </summary>
        public DateTime CapturedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets whether the page is considered stable.
        /// </summary>
        public bool IsStable { get; set; }

        /// <summary>
        /// Gets or sets additional performance data.
        /// </summary>
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    /// <summary>
    /// Represents a Shadow DOM interaction request.
    /// </summary>
    public class ShadowDomRequest
    {
        /// <summary>
        /// Gets or sets the host element selector.
        /// </summary>
        [Required]
        public string HostElementSelector { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target element selector within the shadow root.
        /// </summary>
        [Required]
        public string ShadowElementSelector { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the interaction type (click, input, etc.).
        /// </summary>
        public string InteractionType { get; set; } = "click";

        /// <summary>
        /// Gets or sets the value to input (for input interactions).
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Gets or sets whether to pierce through nested shadow roots.
        /// </summary>
        public bool PierceNestedShadows { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum depth to search for nested shadows.
        /// </summary>
        public int MaxShadowDepth { get; set; } = 5;
    }

    /// <summary>
    /// Event arguments for JavaScript execution events.
    /// </summary>
    public class JavaScriptExecutionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the execution request.
        /// </summary>
        public JavaScriptExecutionRequest Request { get; set; } = new();

        /// <summary>
        /// Gets or sets the operation result.
        /// </summary>
        public JavaScriptOperationResult Result { get; set; } = new();

        /// <summary>
        /// Gets or sets additional event data.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for JavaScript error events.
    /// </summary>
    public class JavaScriptErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the JavaScript error.
        /// </summary>
        public JavaScriptError Error { get; set; } = new();

        /// <summary>
        /// Gets or sets the page URL where the error occurred.
        /// </summary>
        public string? PageUrl { get; set; }

        /// <summary>
        /// Gets or sets additional context data.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for performance metric events.
    /// </summary>
    public class PerformanceMetricsEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the performance metrics.
        /// </summary>
        public PagePerformanceMetrics Metrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the measurement context.
        /// </summary>
        public string Context { get; set; } = string.Empty;
    }
} 
