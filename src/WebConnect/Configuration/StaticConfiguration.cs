using System;
using System.IO;

namespace WebConnect.Configuration;

/// <summary>
/// Static configuration class that contains all hardcoded default values for WebConnect.
/// This replaces the appsettings.json dependency for single executable deployment.
/// </summary>
public static class StaticConfiguration
{
    #region Serilog Configuration

    /// <summary>
    /// Default minimum log level for the application (Information)
    /// </summary>
    public static string DefaultLogLevel { get; set; } = "Information";

    /// <summary>
    /// Default minimum log level for Microsoft components (Warning)
    /// </summary>
    public static string MicrosoftLogLevel { get; set; } = "Warning";

    /// <summary>
    /// Default minimum log level for System components (Warning)
    /// </summary>
    public static string SystemLogLevel { get; set; } = "Warning";

    #endregion

    #region Browser Configuration

    /// <summary>
    /// Path to ChromeDriver executable. Empty string means auto-detect in application directory.
    /// </summary>
    public static string ChromeDriverPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use headless mode by default (false)
    /// </summary>
    public static bool UseHeadless { get; set; } = false;

    /// <summary>
    /// Additional Chrome arguments to pass to the browser
    /// </summary>
    public static string[] AdditionalArguments { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default page load timeout in seconds (30)
    /// </summary>
    public static int PageLoadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Default element wait time in seconds (10)
    /// </summary>
    public static int ElementWaitTimeSeconds { get; set; } = 10;

    /// <summary>
    /// Whether to start Chrome maximized by default (true)
    /// </summary>
    public static bool StartMaximized { get; set; } = true;

    #endregion

    #region Input Blocking Configuration

    /// <summary>
    /// Whether to enable system-wide input blocking during automation (true)
    /// </summary>
    public static bool InputBlockingEnabled { get; set; } = true;

    /// <summary>
    /// Timeout for input blocking in seconds before automatic restoration (150)
    /// </summary>
    public static int InputBlockingTimeoutSeconds { get; set; } = 150;

    #endregion

    #region Timeout Configuration

    /// <summary>
    /// Internal timeout for login verification operations in seconds (15)
    /// </summary>
    public static int InternalTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// External timeout that wraps internal operations in seconds (20)
    /// </summary>
    public static int ExternalTimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Initial delay before starting verification checks in milliseconds (100)
    /// </summary>
    public static int InitialDelayMs { get; set; } = 100;

    /// <summary>
    /// Timeout for quick error detection phase in seconds (2)
    /// </summary>
    public static int QuickErrorTimeoutSeconds { get; set; } = 2;

    /// <summary>
    /// Polling interval for URL change detection in milliseconds (100)
    /// </summary>
    public static int PollingIntervalMs { get; set; } = 100;

    /// <summary>
    /// Maximum time to allocate per verification method in seconds (3)
    /// </summary>
    public static int MaxTimePerMethodSeconds { get; set; } = 3;

    /// <summary>
    /// Minimum time to allocate per verification method in seconds (1)
    /// </summary>
    public static int MinTimePerMethodSeconds { get; set; } = 1;

    #endregion

    #region Logging Configuration

    /// <summary>
    /// Log directory path. Uses Windows temp folder by default.
    /// </summary>
    public static string LogDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "WebConnect");

    /// <summary>
    /// Maximum log file size in MB (10)
    /// </summary>
    public static int MaxFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Number of retained log files (5)
    /// </summary>
    public static int RetainedFileCount { get; set; } = 5;

    /// <summary>
    /// Whether to log sensitive information like passwords (false)
    /// </summary>
    public static bool LogSensitiveInfo { get; set; } = false;

    /// <summary>
    /// Maximum age of log files in days before they are deleted (30)
    /// </summary>
    public static int MaxLogAgeDays { get; set; } = 30;

    /// <summary>
    /// Maximum total size of log directory in MB before cleanup (100)
    /// </summary>
    public static int MaxLogDirectorySizeMb { get; set; } = 100;

    /// <summary>
    /// Whether to perform automatic log cleanup on startup (true)
    /// </summary>
    public static bool EnableLogCleanup { get; set; } = true;

    #endregion

    #region Error Handling Configuration

    /// <summary>
    /// Directory for storing screenshots on error. Uses Windows temp folder by default.
    /// </summary>
    public static string ScreenshotDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "WebConnect", "screenshots");

    /// <summary>
    /// Whether to capture screenshots on error (true)
    /// </summary>
    public static bool CaptureScreenshotsOnError { get; set; } = true;

    /// <summary>
    /// Whether to close browser on error (true)
    /// </summary>
    public static bool CloseBrowserOnError { get; set; } = true;

    /// <summary>
    /// Whether to enable retry mechanism (true)
    /// </summary>
    public static bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts (3)
    /// </summary>
    public static int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial retry delay in milliseconds (1000)
    /// </summary>
    public static int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum retry delay in milliseconds (30000)
    /// </summary>
    public static int MaxRetryDelayMs { get; set; } = 30000;

    /// <summary>
    /// Whether to add jitter to retry delays (true)
    /// </summary>
    public static bool AddJitter { get; set; } = true;

    /// <summary>
    /// Backoff multiplier for retry delays (2.0)
    /// </summary>
    public static double BackoffMultiplier { get; set; } = 2.0;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the TimeoutConfig object with current static values
    /// </summary>
    /// <returns>TimeoutConfig instance with current static configuration values</returns>
    public static TimeoutConfig GetTimeoutConfig()
    {
        return new TimeoutConfig
        {
            InternalTimeout = TimeSpan.FromSeconds(InternalTimeoutSeconds),
            ExternalTimeout = TimeSpan.FromSeconds(ExternalTimeoutSeconds),
            InitialDelay = TimeSpan.FromMilliseconds(InitialDelayMs),
            QuickErrorTimeout = TimeSpan.FromSeconds(QuickErrorTimeoutSeconds),
            PollingInterval = TimeSpan.FromMilliseconds(PollingIntervalMs),
            MaxTimePerMethod = TimeSpan.FromSeconds(MaxTimePerMethodSeconds),
            MinTimePerMethod = TimeSpan.FromSeconds(MinTimePerMethodSeconds)
        };
    }

    /// <summary>
    /// Ensures that required directories exist
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            if (!Directory.Exists(ScreenshotDirectory))
            {
                Directory.CreateDirectory(ScreenshotDirectory);
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the application
            Console.WriteLine($"Warning: Could not create directories: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates the configuration values to ensure they are sensible
    /// </summary>
    public static void ValidateConfiguration()
    {
        if (ExternalTimeoutSeconds <= InternalTimeoutSeconds)
        {
            throw new InvalidOperationException(
                $"ExternalTimeoutSeconds ({ExternalTimeoutSeconds}) must be greater than InternalTimeoutSeconds ({InternalTimeoutSeconds})");
        }

        if (InitialDelayMs >= InternalTimeoutSeconds * 1000)
        {
            throw new InvalidOperationException(
                $"InitialDelayMs ({InitialDelayMs}) must be less than InternalTimeoutSeconds ({InternalTimeoutSeconds * 1000}ms)");
        }

        if (MinTimePerMethodSeconds >= MaxTimePerMethodSeconds)
        {
            throw new InvalidOperationException(
                $"MinTimePerMethodSeconds ({MinTimePerMethodSeconds}) must be less than MaxTimePerMethodSeconds ({MaxTimePerMethodSeconds})");
        }

        if (MaxFileSizeMb <= 0)
        {
            throw new InvalidOperationException($"MaxFileSizeMb ({MaxFileSizeMb}) must be greater than 0");
        }

        if (RetainedFileCount <= 0)
        {
            throw new InvalidOperationException($"RetainedFileCount ({RetainedFileCount}) must be greater than 0");
        }

        if (MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException($"MaxRetryAttempts ({MaxRetryAttempts}) must be 0 or greater");
        }

        if (MaxLogAgeDays <= 0)
        {
            throw new InvalidOperationException($"MaxLogAgeDays ({MaxLogAgeDays}) must be greater than 0");
        }

        if (MaxLogDirectorySizeMb <= 0)
        {
            throw new InvalidOperationException($"MaxLogDirectorySizeMb ({MaxLogDirectorySizeMb}) must be greater than 0");
        }
    }

    /// <summary>
    /// Applies command-line parameter overrides to the static configuration
    /// </summary>
    /// <param name="debugMode">Whether debug mode is enabled (affects logging levels)</param>
    /// <param name="options">Optional command-line options for additional overrides</param>
    public static void ApplyCommandLineOverrides(bool debugMode = false, object options = null)
    {
        if (debugMode)
        {
            DefaultLogLevel = "Debug";
            MicrosoftLogLevel = "Information";
            SystemLogLevel = "Information";
            
            // Enable more detailed logging in debug mode
            LogSensitiveInfo = false; // Keep sensitive info masked even in debug mode
            
            // Reduce log cleanup frequency in debug mode for troubleshooting
            MaxLogAgeDays = 7; // Keep logs for a week in debug mode
            MaxLogDirectorySizeMb = 200; // Allow more log storage in debug mode
        }
        
        // Future: Add support for additional command-line configuration overrides
        // This method can be extended to accept specific configuration parameters
        // from command-line arguments if needed for advanced scenarios
    }

    /// <summary>
    /// Initializes the configuration by ensuring directories exist and validating settings
    /// </summary>
    /// <param name="debugMode">Whether debug mode is enabled</param>
    /// <param name="options">Optional command-line options for configuration overrides</param>
    public static void Initialize(bool debugMode = false, object options = null)
    {
        ApplyCommandLineOverrides(debugMode, options);
        EnsureDirectoriesExist();
        ValidateConfiguration();
    }

    #endregion
} 
