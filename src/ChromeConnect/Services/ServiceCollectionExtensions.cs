using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ChromeConnect.Core;
using ChromeConnect.Services;
using ChromeConnect.Models;

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
        public static IServiceCollection AddChromeConnectServices(this IServiceCollection services, Action<ChromeConnectOptions> setupAction = null)
        {
            // Create and configure options
            var options = new ChromeConnectOptions();
            setupAction?.Invoke(options);

            // Register browser-related services
            services.AddSingleton<BrowserManager>();
            services.AddSingleton<LoginDetector>();

            // Register credential handling and verification
            services.AddSingleton<CredentialManager>();
            services.AddSingleton<LoginVerifier>();
            services.AddSingleton<IScreenshotCapture>(provider => provider.GetRequiredService<LoginVerifier>());

            // Register error handling services
            services.AddSingleton<ErrorHandler>();
            services.AddSingleton<TimeoutManager>();
            services.AddSingleton<ErrorMonitor>();

            // Register main application service
            services.AddSingleton<ChromeConnectService>();

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

            // Register options to configure the timeout manager from settings
            services.AddSingleton(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
                return new TimeoutSettings
                {
                    DefaultTimeoutMs = settings.Browser.PageLoadTimeoutSeconds * 1000,
                    ElementTimeoutMs = settings.Browser.ElementWaitTimeSeconds * 1000,
                    ConditionTimeoutMs = settings.Browser.ElementWaitTimeSeconds * 1500,
                    UrlChangeTimeoutMs = 5000 // Default value
                };
            });

            return services;
        }

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