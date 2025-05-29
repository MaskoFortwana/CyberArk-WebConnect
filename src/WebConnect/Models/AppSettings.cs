namespace WebConnect.Models
{
    /// <summary>
    /// Represents application settings that can be loaded from configuration.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Gets or sets the default browser settings.
        /// </summary>
        public BrowserSettings Browser { get; set; } = new BrowserSettings();

        /// <summary>
        /// Gets or sets the logging settings.
        /// </summary>
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        /// <summary>
        /// Gets or sets the error handling settings.
        /// </summary>
        public ErrorHandlingSettings ErrorHandling { get; set; } = new ErrorHandlingSettings();
    }

    /// <summary>
    /// Settings related to browser configuration.
    /// </summary>
    public class BrowserSettings
    {
        /// <summary>
        /// Gets or sets the Chrome driver path override.
        /// If not specified, ChromeDriver will be downloaded automatically.
        /// </summary>
        public string ChromeDriverPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to use headless mode by default.
        /// </summary>
        public bool UseHeadless { get; set; } = false;

        /// <summary>
        /// Gets or sets additional Chrome arguments.
        /// </summary>
        public string[] AdditionalArguments { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the default page load timeout in seconds.
        /// </summary>
        public int PageLoadTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the wait time for element detection in seconds.
        /// </summary>
        public int ElementWaitTimeSeconds { get; set; } = 10;
    }

    /// <summary>
    /// Settings related to application logging.
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Gets or sets the log directory path.
        /// </summary>
        public string LogDirectory { get; set; } = "logs";

        /// <summary>
        /// Gets or sets the maximum log file size in megabytes.
        /// </summary>
        public int MaxFileSizeMb { get; set; } = 10;

        /// <summary>
        /// Gets or sets the number of log files to retain.
        /// </summary>
        public int RetainedFileCount { get; set; } = 5;

        /// <summary>
        /// Gets or sets a value indicating whether to log sensitive information.
        /// Should generally be false in production.
        /// </summary>
        public bool LogSensitiveInfo { get; set; } = false;
    }

    /// <summary>
    /// Settings related to error handling.
    /// </summary>
    public class ErrorHandlingSettings
    {
        /// <summary>
        /// Gets or sets the screenshot directory path.
        /// </summary>
        public string ScreenshotDirectory { get; set; } = "screenshots";

        /// <summary>
        /// Gets or sets a value indicating whether to capture screenshots on error.
        /// </summary>
        public bool CaptureScreenshotsOnError { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to close the browser on error.
        /// </summary>
        public bool CloseBrowserOnError { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable retry for transient errors.
        /// </summary>
        public bool EnableRetry { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the initial retry delay in milliseconds.
        /// </summary>
        public int InitialRetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum retry delay in milliseconds.
        /// </summary>
        public int MaxRetryDelayMs { get; set; } = 30000;

        /// <summary>
        /// Gets or sets a value indicating whether to add jitter to retry delays.
        /// </summary>
        public bool AddJitter { get; set; } = true;

        /// <summary>
        /// Gets or sets the backoff multiplier for retry delays.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;
    }
} 
