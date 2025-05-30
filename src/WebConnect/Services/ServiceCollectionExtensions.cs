using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebConnect.Core;
using WebConnect.Services;
using WebConnect.Models;
using WebConnect.Configuration;
using Serilog;
using Microsoft.Extensions.Configuration;

namespace WebConnect.Services
{
    /// <summary>
    /// Extension methods for configuring WebConnect services in the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all WebConnect services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="setupAction">Optional action to configure additional settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWebConnectServices(this IServiceCollection services, Action<WebConnectOptions>? setupAction = null)
        {
            // Create and configure options
            var options = new WebConnectOptions();
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
            
            // Register timeout configuration from static configuration
            services.AddSingleton<TimeoutConfig>(provider =>
            {
                return StaticConfiguration.GetTimeoutConfig();
            });

            // Register PolicyFactory for Polly policies
            services.AddSingleton<PolicyFactory>();

            // Register login verification configuration and service with optimized timeouts
            services.AddSingleton<LoginVerificationConfig>(provider =>
            {
                var timeoutConfig = provider.GetRequiredService<TimeoutConfig>();
                return new LoginVerificationConfig
                {
                    MaxVerificationTimeSeconds = (int)timeoutConfig.InternalTimeout.TotalSeconds,
                    InitialDelayMs = (int)timeoutConfig.InitialDelay.TotalMilliseconds,
                    PostSubmissionDelayMs = 0, // Removed 2 seconds delay - no longer needed
                    EnableTimingLogs = true,          // Enable performance monitoring
                    CaptureScreenshotsOnFailure = true, // Capture screenshots on failures
                    // New properties for page transition detection
                    UsePageTransitionDetection = true,
                    MaxTransitionWaitTimeSeconds = 3,
                    InitialPollingIntervalMs = 100,
                    MaxPollingIntervalMs = 500,
                    PollingIntervalGrowthFactor = 1.5,
                    StableCheckCount = 2,
                    // Site-specific configurations - empty by default, can be configured externally
                    SiteSpecificConfigurations = new Dictionary<string, SiteSpecificConfig>()
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
                    SessionCookieName = "webconnect_session",
                    ValidationUrls = new List<string>()
                };
            });
            services.AddSingleton<SessionManager>();

            // Register main application service
            services.AddSingleton<WebConnectService>();

            return services;
        }

        /// <summary>
        /// Adds WebConnect configuration using static configuration values.
        /// </summary>
        /// <param name="services">The service collection to add configuration to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWebConnectConfiguration(this IServiceCollection services)
        {
            // Register error handler settings from static configuration
            services.AddSingleton(provider =>
            {
                return new ErrorHandlerSettings
                {
                    CaptureScreenshots = StaticConfiguration.CaptureScreenshotsOnError,
                    CloseDriverOnError = StaticConfiguration.CloseBrowserOnError,
                    DefaultRetryCount = StaticConfiguration.MaxRetryAttempts,
                    DefaultRetryDelayMs = StaticConfiguration.InitialRetryDelayMs,
                    MaxRetryDelayMs = StaticConfiguration.MaxRetryDelayMs,
                    BackoffMultiplier = StaticConfiguration.BackoffMultiplier,
                    AddJitter = StaticConfiguration.AddJitter
                };
            });

            // Register timeout manager settings from static configuration
            services.AddSingleton(provider =>
            {
                return new TimeoutSettings
                {
                    DefaultTimeoutMs = StaticConfiguration.PageLoadTimeoutSeconds * 1000,
                    ElementTimeoutMs = StaticConfiguration.ElementWaitTimeSeconds * 1000,
                    ConditionTimeoutMs = StaticConfiguration.ElementWaitTimeSeconds * 1500,
                    UrlChangeTimeoutMs = 5000 // Default value
                };
            });

            return services;
        }

        // AddWebConnectLogging methods (both overloads) are removed as Serilog handles logging configuration in Program.cs
        /*
        /// <summary>
        /// Adds WebConnect logging configuration.
        /// </summary>
        /// <param name="services">The service collection to add logging to.</param>
        /// <param name="logDirectory">The directory where log files will be stored.</param>
        /// <param name="debugMode">Whether to enable debug level logging.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWebConnectLogging(this IServiceCollection services, string logDirectory, bool debugMode = false)
        {
            services.AddLogging(configure =>
            {
                configure.AddConsole();

                // Add file logging
                string logFilePath = System.IO.Path.Combine(logDirectory, "webconnect-.log");
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
        /// Adds WebConnect logging configuration based on application settings.
        /// </summary>
        /// <param name="services">The service collection to add logging to.</param>
        /// <param name="configuration">The configuration to use for logging settings.</param>
        /// <param name="debugMode">Whether to enable debug level logging.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWebConnectLogging(this IServiceCollection services, IConfiguration configuration, bool debugMode = false)
        {
            var settings = new LoggingSettings();
            configuration.GetSection("WebConnect:Logging").Bind(settings);

            services.AddLogging(configure =>
            {
                configure.AddConsole();

                // Add file logging
                string logFilePath = System.IO.Path.Combine(settings.LogDirectory, "webconnect-.log");
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
    /// Options for configuring WebConnect services.
    /// </summary>
    public class WebConnectOptions
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
