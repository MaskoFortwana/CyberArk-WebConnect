using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OpenQA.Selenium;

namespace WebConnect.Models
{
    /// <summary>
    /// Represents the type of action to perform in a login step.
    /// </summary>
    public enum LoginStepType
    {
        /// <summary>
        /// Enter credentials (username, password, domain).
        /// </summary>
        EnterCredentials,
        
        /// <summary>
        /// Click a button or link.
        /// </summary>
        ClickElement,
        
        /// <summary>
        /// Wait for page navigation or content to load.
        /// </summary>
        WaitForNavigation,
        
        /// <summary>
        /// Wait for a specific element to appear.
        /// </summary>
        WaitForElement,
        
        /// <summary>
        /// Validate that certain elements are present.
        /// </summary>
        ValidateElements,
        
        /// <summary>
        /// Execute custom JavaScript.
        /// </summary>
        ExecuteScript,
        
        /// <summary>
        /// Enter text into a specific field.
        /// </summary>
        EnterText,
        
        /// <summary>
        /// Select from a dropdown or combobox.
        /// </summary>
        SelectOption,
        
        /// <summary>
        /// Custom validation logic.
        /// </summary>
        CustomValidation
    }

    /// <summary>
    /// Represents the current state of a multi-step login flow.
    /// </summary>
    public enum LoginFlowState
    {
        /// <summary>
        /// Flow has not started yet.
        /// </summary>
        NotStarted,
        
        /// <summary>
        /// Flow is currently executing.
        /// </summary>
        InProgress,
        
        /// <summary>
        /// Flow completed successfully.
        /// </summary>
        Completed,
        
        /// <summary>
        /// Flow failed with an error.
        /// </summary>
        Failed,
        
        /// <summary>
        /// Flow was cancelled by user or timeout.
        /// </summary>
        Cancelled,
        
        /// <summary>
        /// Flow is waiting for user input.
        /// </summary>
        WaitingForInput,
        
        /// <summary>
        /// Flow is being retried after a failure.
        /// </summary>
        Retrying
    }

    /// <summary>
    /// Defines a single step in a multi-step login process.
    /// </summary>
    public class LoginStep
    {
        /// <summary>
        /// Gets or sets the unique identifier for this step.
        /// </summary>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable name for this step.
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of action to perform.
        /// </summary>
        public LoginStepType StepType { get; set; }

        /// <summary>
        /// Gets or sets the CSS selector or XPath for the target element.
        /// </summary>
        public string? ElementSelector { get; set; }

        /// <summary>
        /// Gets or sets the text to enter (for text input steps).
        /// </summary>
        public string? TextValue { get; set; }

        /// <summary>
        /// Gets or sets the JavaScript code to execute (for script steps).
        /// </summary>
        public string? ScriptCode { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds for this step.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets whether this step can be retried on failure.
        /// </summary>
        public bool CanRetry { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets validation selectors that must be present after this step.
        /// </summary>
        public List<string> ValidationSelectors { get; set; } = new();

        /// <summary>
        /// Gets or sets selectors that should NOT be present after this step.
        /// </summary>
        public List<string> NegativeValidationSelectors { get; set; } = new();

        /// <summary>
        /// Gets or sets the expected URL pattern after this step (regex supported).
        /// </summary>
        public string? ExpectedUrlPattern { get; set; }

        /// <summary>
        /// Gets or sets conditional logic for determining if this step should be executed.
        /// </summary>
        public string? Condition { get; set; }

        /// <summary>
        /// Gets or sets custom properties for extensibility.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// Defines a complete multi-step login flow.
    /// </summary>
    public class LoginFlow
    {
        /// <summary>
        /// Gets or sets the unique identifier for this flow.
        /// </summary>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable name for this flow.
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of this flow.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ordered list of steps in this flow.
        /// </summary>
        public List<LoginStep> Steps { get; set; } = new();

        /// <summary>
        /// Gets or sets the overall timeout for the entire flow in seconds.
        /// </summary>
        public int OverallTimeoutSeconds { get; set; } = 300; // 5 minutes

        /// <summary>
        /// Gets or sets whether to continue on individual step failures.
        /// </summary>
        public bool ContinueOnStepFailure { get; set; } = false;

        /// <summary>
        /// Gets or sets the URL patterns this flow is designed for (regex supported).
        /// </summary>
        public List<string> UrlPatterns { get; set; } = new();

        /// <summary>
        /// Gets or sets recovery steps to execute if the flow fails.
        /// </summary>
        public List<LoginStep> RecoverySteps { get; set; } = new();

        /// <summary>
        /// Gets or sets custom configuration properties.
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Represents the current execution state of a login flow.
    /// </summary>
    public class LoginFlowExecution
    {
        /// <summary>
        /// Gets or sets the flow being executed.
        /// </summary>
        [Required]
        public LoginFlow Flow { get; set; } = new();

        /// <summary>
        /// Gets or sets the current state of execution.
        /// </summary>
        public LoginFlowState State { get; set; } = LoginFlowState.NotStarted;

        /// <summary>
        /// Gets or sets the index of the currently executing step.
        /// </summary>
        public int CurrentStepIndex { get; set; } = -1;

        /// <summary>
        /// Gets or sets the currently executing step.
        /// </summary>
        public LoginStep? CurrentStep => CurrentStepIndex >= 0 && CurrentStepIndex < Flow.Steps.Count 
            ? Flow.Steps[CurrentStepIndex] 
            : null;

        /// <summary>
        /// Gets or sets the start time of the flow execution.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time of the flow execution.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets the total execution time.
        /// </summary>
        public TimeSpan ExecutionTime => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.Now - StartTime;

        /// <summary>
        /// Gets or sets the list of completed steps with their results.
        /// </summary>
        public List<StepExecutionResult> CompletedSteps { get; set; } = new();

        /// <summary>
        /// Gets or sets any errors that occurred during execution.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets additional context data for the execution.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Represents the result of executing a single step.
    /// </summary>
    public class StepExecutionResult
    {
        /// <summary>
        /// Gets or sets the step that was executed.
        /// </summary>
        [Required]
        public LoginStep Step { get; set; } = new();

        /// <summary>
        /// Gets or sets whether the step was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the step failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the start time of step execution.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time of step execution.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets the duration of step execution.
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Gets or sets the number of retry attempts made.
        /// </summary>
        public int RetryAttempts { get; set; } = 0;

        /// <summary>
        /// Gets or sets any additional data captured during step execution.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Configuration options for the multi-step login navigator.
    /// </summary>
    public class MultiStepLoginConfiguration
    {
        /// <summary>
        /// Gets or sets the default timeout for steps in seconds.
        /// </summary>
        public int DefaultStepTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the default maximum retry attempts.
        /// </summary>
        public int DefaultMaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay between retry attempts in milliseconds.
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether to capture screenshots on step failures.
        /// </summary>
        public bool CaptureScreenshotsOnFailure { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable detailed logging.
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to stop the entire flow on first step failure.
        /// </summary>
        public bool StopOnFirstFailure { get; set; } = true;

        /// <summary>
        /// Gets or sets custom element wait strategies.
        /// </summary>
        public Dictionary<string, int> ElementWaitTimeouts { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for step execution events.
    /// </summary>
    public class StepExecutionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the execution context.
        /// </summary>
        public LoginFlowExecution Execution { get; set; } = new();

        /// <summary>
        /// Gets or sets the step being executed.
        /// </summary>
        public LoginStep Step { get; set; } = new();

        /// <summary>
        /// Gets or sets the execution result (null for starting events).
        /// </summary>
        public StepExecutionResult? Result { get; set; }
    }

    /// <summary>
    /// Event arguments for flow execution events.
    /// </summary>
    public class FlowExecutionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the execution context.
        /// </summary>
        public LoginFlowExecution Execution { get; set; } = new();

        /// <summary>
        /// Gets or sets additional message information.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
} 
