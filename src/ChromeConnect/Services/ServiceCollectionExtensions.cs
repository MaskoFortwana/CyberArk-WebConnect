using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ChromeConnect.Core;
using ChromeConnect.Services;
using ChromeConnect.Models;
using Serilog;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Extension methods for configuring ChromeConnect services in the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all ChromeConnect services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="setupAction">Optional action to configure additional settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddChromeConnectServices(this IServiceCollection services, Action<ChromeConnectOptions>? setupAction = null)
        {
            // Create and configure options
            var options = new ChromeConnectOptions();
            setupAction?.Invoke(options);

            // Register browser-related services
            services.AddSingleton<BrowserManager>();
            services.AddSingleton<LoginDetector>();
            services.AddSingleton<DetectionMetricsService>();

            // Register credential handling configuration and service with optimized performance
            services.AddSingleton<CredentialEntryConfig>(provider =>
            {
                return new CredentialEntryConfig
                {
                    TypingMode = TypingMode.OptimizedHuman,
                    MinDelayMs = 10,
                    MaxDelayMs = 30,
                    PostEntryDelayMs = 50,
                    SubmissionDelayMs = 500,
                    UseJavaScriptFallback = true,
                    LogTimingMetrics = true
                };
            });
            services.AddSingleton<CredentialManager>();
            
            // Register login verification configuration and service with optimized timeouts
            services.AddSingleton<LoginVerificationConfig>(provider =>
            {
                return new LoginVerificationConfig
                {
                    MaxVerificationTimeSeconds = 6,   // Further reduced to 6s for even faster response
                    InitialDelayMs = 100,             // Minimal initial delay for immediate detection
                    EnableTimingLogs = true,          // Enable performance monitoring
                    CaptureScreenshotsOnFailure = true // Capture screenshots on failures
                };
            });
            services.AddSingleton<LoginVerifier>();
            services.AddSingleton<IScreenshotCapture>(provider => provider.GetRequiredService<LoginVerifier>());

            // Register error handling services
            services.AddSingleton<ErrorHandler>();
            services.AddSingleton<TimeoutManager>();
            services.AddSingleton<ErrorMonitor>();

            // Register multi-step login navigation
            services.AddSingleton<MultiStepLoginConfiguration>(provider =>
            {
                return new MultiStepLoginConfiguration
                {
                    DefaultStepTimeoutSeconds = 30,
                    DefaultMaxRetries = 3,
                    RetryDelayMs = 1000,
                    CaptureScreenshotsOnFailure = true,
                    EnableDetailedLogging = true,
                    StopOnFirstFailure = true
                };
            });
            services.AddSingleton<MultiStepLoginNavigator>();

            // Register popup and iFrame handling
            services.AddSingleton<PopupAndIFrameConfiguration>(provider =>
            {
                return new PopupAndIFrameConfiguration
                {
                    PopupDetectionTimeoutSeconds = 10,
                    IFrameDetectionTimeoutSeconds = 5,
                    DetectionPollingIntervalMs = 500,
                    AutoCloseAbandonedPopups = true,
                    AutoSwitchBackToMain = true,
                    MaxNestedIFrameDepth = 5,
                    EnableDetailedLogging = true,
                    CrossDomainTimeoutSeconds = 3
                };
            });
            services.AddSingleton<PopupAndIFrameHandler>();

            // Register JavaScript interaction handling
            services.AddSingleton<JavaScriptInteractionConfiguration>(provider =>
            {
                return new JavaScriptInteractionConfiguration
                {
                    DefaultExecutionTimeoutSeconds = 30,
                    DefaultWaitTimeoutSeconds = 15,
                    PollingIntervalMs = 500,
                    EnablePerformanceMonitoring = true,
                    CaptureJavaScriptErrors = true,
                    MaxRetryAttempts = 3,
                    RetryDelayMs = 1000,
                    EnableDetailedLogging = true,
                    NetworkIdleTimeoutMs = 2000,
                    EnableShadowDomSupport = true
                };
            });
            services.AddSingleton<JavaScriptInteractionManager>();

            // Register session management
            services.AddSingleton<SessionManagementConfiguration>(provider =>
            {
                return new SessionManagementConfiguration
                {
                    DefaultSessionTimeoutMinutes = 30,
                    ValidationIntervalMinutes = 5,
                    PreferredStorageType = SessionStorageType.Cookies,
                    FallbackStorageTypes = new List<SessionStorageType>
                    {
                        SessionStorageType.LocalStorage,
                        SessionStorageType.SessionStorage,
                        SessionStorageType.Memory
                    },
                    EnableAutoRefresh = true,
                    RefreshStrategy = SessionRefreshStrategy.OnExpiry,
                    RefreshBeforeExpiryMinutes = 5,
                    EnableEncryption = true,
                    EnableAutoRecovery = true,
                    MaxRecoveryAttempts = 3,
                    EnableDetailedLogging = true,
                    SessionCookieName = "chromeconnect_session",
                    ValidationUrls = new List<string>()
                };
            });
            services.AddSingleton<SessionManager>();

            // Register main application service
            services.AddSingleton<ChromeConnectService>();

            // Register options to configure the timeout manager from settings
            services.AddSingleton(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
                return new TimeoutSettings
                {
                    DefaultTimeoutMs = 10000, // Reduced to 10s for better alignment with LoginVerifier
                    ElementTimeoutMs = settings.Browser.ElementWaitTimeSeconds * 1000,
                    ConditionTimeoutMs = settings.Browser.ElementWaitTimeSeconds * 1500,
                    UrlChangeTimeoutMs = 5000 // Default value
                };
            });

            return services;
        }

        /// <summary>
        /// Adds ChromeConnect configuration from the provided <see cref="IConfiguration"/>.
        /// </summary>
        /// <param name="services">The service collection to add configuration to.</param>
        /// <param name="configuration">The configuration to bind from.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddChromeConnectConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind configuration to AppSettings
            services.Configure<AppSettings>(configuration.GetSection("ChromeConnect"));

            // Register options to configure the error handler from settings
            services.AddSingleton(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
                return new ErrorHandlerSettings
                {
                    CaptureScreenshots = settings.ErrorHandling.CaptureScreenshotsOnError,
                    CloseDriverOnError = settings.ErrorHandling.CloseBrowserOnError,
                    DefaultRetryCount = settings.ErrorHandling.MaxRetryAttempts,
                    DefaultRetryDelayMs = settings.ErrorHandling.InitialRetryDelayMs,
                    MaxRetryDelayMs = settings.ErrorHandling.MaxRetryDelayMs,
                    BackoffMultiplier = settings.ErrorHandling.BackoffMultiplier,
                    AddJitter = settings.ErrorHandling.AddJitter
                };
            });

            return services;
        }

        // AddChromeConnectLogging methods (both overloads) are removed as Serilog handles logging configuration in Program.cs
        /*
        /// <summary>
        /// Adds ChromeConnect logging configuration.
        /// </summary>
        /// <param name="services">The service collection to add logging to.</param>
        /// <param name="logDirectory">The directory where log files will be stored.</param>
        /// <param name="debugMode">Whether to enable debug level logging.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddChromeConnectLogging(this IServiceCollection services, string logDirectory, bool debugMode = false)
        {
            services.AddLogging(configure =>
            {
                configure.AddConsole();

                // Add file logging
                string logFilePath = System.IO.Path.Combine(logDirectory, "chromeconnect-.log");
                configure.AddFile(logFilePath, options =>
                {
                    options.FileSizeLimitBytes = 10 * 1024 * 1024; // 10MB
                    options.RetainedFileCountLimit = 5; // Keep 5 rotated files
                });

                // Set minimum level based on debug flag
                if (debugMode)
                {
                    configure.SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    configure.SetMinimumLevel(LogLevel.Information);
                }
            });

            return services;
        }

        /// <summary>
        /// Adds ChromeConnect logging configuration based on application settings.
        /// </summary>
        /// <param name="services">The service collection to add logging to.</param>
        /// <param name="configuration">The configuration to use for logging settings.</param>
        /// <param name="debugMode">Whether to enable debug level logging.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddChromeConnectLogging(this IServiceCollection services, IConfiguration configuration, bool debugMode = false)
        {
            var settings = new LoggingSettings();
            configuration.GetSection("ChromeConnect:Logging").Bind(settings);

            services.AddLogging(configure =>
            {
                configure.AddConsole();

                // Add file logging
                string logFilePath = System.IO.Path.Combine(settings.LogDirectory, "chromeconnect-.log");
                configure.AddFile(logFilePath, options =>
                {
                    options.FileSizeLimitBytes = settings.MaxFileSizeMb * 1024 * 1024;
                    options.RetainedFileCountLimit = settings.RetainedFileCount;
                });

                // Set minimum level based on debug flag
                if (debugMode)
                {
                    configure.SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    configure.SetMinimumLevel(LogLevel.Information);
                }
            });

            return services;
        }
        */
    }

    /// <summary>
    /// Options for configuring ChromeConnect services.
    /// </summary>
    public class ChromeConnectOptions
    {
        /// <summary>
        /// Gets or sets the screenshot directory path.
        /// </summary>
        public string ScreenshotDirectory { get; set; } = "screenshots";

        /// <summary>
        /// Gets or sets whether to capture screenshots on error.
        /// </summary>
        public bool CaptureScreenshotsOnError { get; set; } = true;

        /// <summary>
        /// Gets or sets the default timeout for browser operations in milliseconds.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Gets or sets whether to close the browser on error.
        /// </summary>
        public bool CloseBrowserOnError { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable retry for transient errors.
        /// </summary>
        public bool EnableRetry { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;
    }
} 