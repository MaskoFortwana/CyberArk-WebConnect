using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ChromeConnect.Models
{
    /// <summary>
    /// Represents the type of context that can be handled.
    /// </summary>
    public enum ContextType
    {
        /// <summary>
        /// Main browser window.
        /// </summary>
        MainWindow,
        
        /// <summary>
        /// Popup window.
        /// </summary>
        Popup,
        
        /// <summary>
        /// iFrame within the current context.
        /// </summary>
        IFrame,
        
        /// <summary>
        /// Nested iFrame (iFrame within another iFrame).
        /// </summary>
        NestedIFrame
    }

    /// <summary>
    /// Represents the state of a context detection operation.
    /// </summary>
    public enum DetectionState
    {
        /// <summary>
        /// Detection has not started.
        /// </summary>
        NotStarted,
        
        /// <summary>
        /// Detection is in progress.
        /// </summary>
        InProgress,
        
        /// <summary>
        /// Context was detected successfully.
        /// </summary>
        Detected,
        
        /// <summary>
        /// Detection timed out.
        /// </summary>
        TimedOut,
        
        /// <summary>
        /// Detection failed with an error.
        /// </summary>
        Failed,
        
        /// <summary>
        /// Context was closed or no longer available.
        /// </summary>
        Closed
    }

    /// <summary>
    /// Configuration options for popup and iFrame handling.
    /// </summary>
    public class PopupAndIFrameConfiguration
    {
        /// <summary>
        /// Gets or sets the default timeout for popup detection in seconds.
        /// </summary>
        public int PopupDetectionTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Gets or sets the default timeout for iFrame detection in seconds.
        /// </summary>
        public int IFrameDetectionTimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets the polling interval for detection in milliseconds.
        /// </summary>
        public int DetectionPollingIntervalMs { get; set; } = 500;

        /// <summary>
        /// Gets or sets whether to automatically close abandoned popups.
        /// </summary>
        public bool AutoCloseAbandonedPopups { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to automatically switch back to the main window after popup operations.
        /// </summary>
        public bool AutoSwitchBackToMain { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum depth for nested iFrame detection.
        /// </summary>
        public int MaxNestedIFrameDepth { get; set; } = 5;

        /// <summary>
        /// Gets or sets whether to enable detailed logging for debugging.
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the timeout for cross-domain iFrame operations in seconds.
        /// </summary>
        public int CrossDomainTimeoutSeconds { get; set; } = 3;
    }

    /// <summary>
    /// Represents information about a detected context (popup or iFrame).
    /// </summary>
    public class ContextInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for this context.
        /// </summary>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of context.
        /// </summary>
        public ContextType Type { get; set; }

        /// <summary>
        /// Gets or sets the window handle (for popups).
        /// </summary>
        public string? WindowHandle { get; set; }

        /// <summary>
        /// Gets or sets the iFrame element selector (for iFrames).
        /// </summary>
        public string? IFrameSelector { get; set; }

        /// <summary>
        /// Gets or sets the iFrame index (for iFrames).
        /// </summary>
        public int? IFrameIndex { get; set; }

        /// <summary>
        /// Gets or sets the title of the context.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the URL of the context.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the parent context ID (for nested contexts).
        /// </summary>
        public string? ParentContextId { get; set; }

        /// <summary>
        /// Gets or sets the detection timestamp.
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the current state of this context.
        /// </summary>
        public DetectionState State { get; set; } = DetectionState.NotStarted;

        /// <summary>
        /// Gets or sets additional metadata about the context.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Represents the result of a context operation.
    /// </summary>
    public class ContextOperationResult
    {
        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the context that was operated on.
        /// </summary>
        public ContextInfo? Context { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the operation duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets additional operation data.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Represents a request to detect a specific context.
    /// </summary>
    public class ContextDetectionRequest
    {
        /// <summary>
        /// Gets or sets the type of context to detect.
        /// </summary>
        public ContextType ContextType { get; set; }

        /// <summary>
        /// Gets or sets the expected title pattern (regex supported).
        /// </summary>
        public string? ExpectedTitlePattern { get; set; }

        /// <summary>
        /// Gets or sets the expected URL pattern (regex supported).
        /// </summary>
        public string? ExpectedUrlPattern { get; set; }

        /// <summary>
        /// Gets or sets the iFrame selector (for iFrame detection).
        /// </summary>
        public string? IFrameSelector { get; set; }

        /// <summary>
        /// Gets or sets the timeout for this specific detection in seconds.
        /// </summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets whether to switch to the context automatically upon detection.
        /// </summary>
        public bool AutoSwitch { get; set; } = false;

        /// <summary>
        /// Gets or sets the parent context ID (for nested detection).
        /// </summary>
        public string? ParentContextId { get; set; }
    }

    /// <summary>
    /// Tracks the current context state and history.
    /// </summary>
    public class ContextTracker
    {
        /// <summary>
        /// Gets or sets the current active context.
        /// </summary>
        public ContextInfo? CurrentContext { get; set; }

        /// <summary>
        /// Gets or sets the main window context.
        /// </summary>
        public ContextInfo? MainWindowContext { get; set; }

        /// <summary>
        /// Gets or sets all detected contexts.
        /// </summary>
        public List<ContextInfo> DetectedContexts { get; set; } = new();

        /// <summary>
        /// Gets or sets the context switch history.
        /// </summary>
        public List<ContextSwitchRecord> SwitchHistory { get; set; } = new();

        /// <summary>
        /// Gets the context navigation path.
        /// </summary>
        public List<string> NavigationPath => SwitchHistory.Select(s => s.ToContextId).ToList();
    }

    /// <summary>
    /// Represents a record of context switching.
    /// </summary>
    public class ContextSwitchRecord
    {
        /// <summary>
        /// Gets or sets the source context ID.
        /// </summary>
        public string FromContextId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the destination context ID.
        /// </summary>
        public string ToContextId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the switch timestamp.
        /// </summary>
        public DateTime SwitchedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets whether the switch was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets any error message from the switch operation.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Event arguments for context detection events.
    /// </summary>
    public class ContextDetectionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the detected context.
        /// </summary>
        public ContextInfo Context { get; set; } = new();

        /// <summary>
        /// Gets or sets the detection request.
        /// </summary>
        public ContextDetectionRequest Request { get; set; } = new();

        /// <summary>
        /// Gets or sets additional event data.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for context switch events.
    /// </summary>
    public class ContextSwitchEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the switch record.
        /// </summary>
        public ContextSwitchRecord SwitchRecord { get; set; } = new();

        /// <summary>
        /// Gets or sets the context tracker state.
        /// </summary>
        public ContextTracker Tracker { get; set; } = new();
    }
} 