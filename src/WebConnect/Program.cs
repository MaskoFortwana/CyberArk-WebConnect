using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using CommandLine.Text;
using WebConnect.Core;
using WebConnect.Models;
using WebConnect.Services;
using WebConnect.Configuration;
using WebConnect.Utilities;
using Serilog;
using Serilog.Events;

namespace WebConnect;

/// <summary>
/// Main program class and application entry point.
/// </summary>
public class Program
{
    /// <summary>
    /// The application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The exit code of the application.</returns>
    public static async Task<int> Main(string[] args)
    {
        // Parse command line arguments first to get debug mode
        var parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args);
        bool debugMode = false;
        CommandLineOptions options = null;
        
        if (parserResult is Parsed<CommandLineOptions> parsedOptions)
        {
            debugMode = parsedOptions.Value.Debug;
            options = parsedOptions.Value;
            
            // Validate command-line options
            var validationErrors = options.ValidateOptions();
            if (validationErrors.Any())
            {
                Console.WriteLine("❌ Command-line parameter validation failed:");
                foreach (var error in validationErrors)
                {
                    Console.WriteLine($"   • {error}");
                }
                Console.WriteLine();
                Console.WriteLine("Use --help for parameter information or --version for deployment requirements.");
                return 4; // Configuration error
            }
        }
        else if (parserResult is NotParsed<CommandLineOptions> notParsed)
        {
            // Check if user requested help or version info
            if (args.Contains("--help") || args.Contains("-h") || args.Contains("/?"))
            {
                Console.WriteLine(CommandLineOptions.GetExtendedHelpText());
                return 0;
            }
            
            // Show parsing errors
            Console.WriteLine("❌ Command-line argument parsing failed:");
            foreach (var error in notParsed.Errors)
            {
                if (error is MissingRequiredOptionError missingError)
                {
                    Console.WriteLine($"   • Missing required parameter: --{missingError.NameInfo.LongName}");
                }
                else if (error is UnknownOptionError unknownError)
                {
                    Console.WriteLine($"   • Unknown parameter: --{unknownError.Token}");
                }
                else
                {
                    Console.WriteLine($"   • {error}");
                }
            }
            Console.WriteLine();
            Console.WriteLine("Use --help for parameter information or --version for deployment requirements.");
            return 4; // Configuration error
        }

        // Initialize static configuration with command line overrides
        StaticConfiguration.Initialize(debugMode, options);

        // Configure Serilog logger using static configuration
        var logLevel = Enum.Parse<LogEventLevel>(StaticConfiguration.DefaultLogLevel);
        var microsoftLogLevel = Enum.Parse<LogEventLevel>(StaticConfiguration.MicrosoftLogLevel);
        var systemLogLevel = Enum.Parse<LogEventLevel>(StaticConfiguration.SystemLogLevel);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft", microsoftLogLevel)
            .MinimumLevel.Override("System", systemLogLevel)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(Path.Combine(StaticConfiguration.LogDirectory, "webconnect-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: StaticConfiguration.RetainedFileCount,
                fileSizeLimitBytes: StaticConfiguration.MaxFileSizeMb * 1024 * 1024,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting WebConnect application");
            Log.Information($"Logs will be written to: {StaticConfiguration.LogDirectory}");
            
            // Register cleanup handlers for ChromeDriver processes
            RegisterCleanupHandlers();
            
            // Perform log cleanup if enabled
            if (StaticConfiguration.EnableLogCleanup)
            {
                Log.Debug("Performing log cleanup...");
                LogCleanupUtility.CleanupOldLogs(
                    StaticConfiguration.LogDirectory, 
                    StaticConfiguration.MaxLogAgeDays, 
                    StaticConfiguration.MaxLogDirectorySizeMb);
                
                var logFileCount = LogCleanupUtility.GetLogFileCount(StaticConfiguration.LogDirectory);
                var logDirSizeMb = LogCleanupUtility.GetLogDirectorySize(StaticConfiguration.LogDirectory) / (1024.0 * 1024.0);
                Log.Information("Log cleanup completed. Files: {LogFileCount}, Directory size: {LogDirSizeMb:F2} MB", 
                    logFileCount, logDirSizeMb);
            }
            
            if (parserResult.Value?.ShowVersion == true)
            {
                Console.WriteLine($"{CoreConstants.ApplicationName} version {CoreConstants.Version}");
                Console.WriteLine();
                Console.WriteLine("🚀 DEPLOYMENT REQUIREMENTS:");
                Console.WriteLine($"   • ChromeDriver.exe must be in the same folder as {CoreConstants.ApplicationName}.exe");
                Console.WriteLine("   • No additional configuration files required");
                Console.WriteLine($"   • Logs: {StaticConfiguration.LogDirectory}");
                Console.WriteLine($"   • Screenshots: {StaticConfiguration.ScreenshotDirectory}");
                Console.WriteLine("   • Compatible with Windows 10/11 (x64)");
                Console.WriteLine();
                Console.WriteLine("Use --help for complete parameter reference.");
                return 0;
            }
            
            using var host = CreateHostBuilder(args, debugMode).Build();
            
            if (options == null)
            {
                Log.Error("Command line argument parsing failed.");
                return 1;
            }
            
            var webConnectService = host.Services.GetRequiredService<WebConnectService>();
            return await webConnectService.ExecuteAsync(options);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Creates and configures the host builder.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="debugMode">Whether debug mode is enabled.</param>
    /// <returns>The configured host builder.</returns>
    private static IHostBuilder CreateHostBuilder(string[] args, bool debugMode)
    {
        var logLevel = Enum.Parse<LogEventLevel>(StaticConfiguration.DefaultLogLevel);
        var microsoftLogLevel = Enum.Parse<LogEventLevel>(StaticConfiguration.MicrosoftLogLevel);
        var systemLogLevel = Enum.Parse<LogEventLevel>(StaticConfiguration.SystemLogLevel);

        return Host.CreateDefaultBuilder(args)
            .UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                .MinimumLevel.Is(logLevel)
                .MinimumLevel.Override("Microsoft", microsoftLogLevel)
                .MinimumLevel.Override("System", systemLogLevel)
                .Enrich.FromLogContext()
                .WriteTo.Console(debugMode ? LogEventLevel.Debug : logLevel,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(Path.Combine(StaticConfiguration.LogDirectory, "webconnect-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: StaticConfiguration.RetainedFileCount,
                    fileSizeLimitBytes: StaticConfiguration.MaxFileSizeMb * 1024 * 1024,
                    restrictedToMinimumLevel: debugMode ? LogEventLevel.Debug : logLevel,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            )
            .ConfigureServices((hostContext, services) =>
            {
                // StaticConfiguration.Initialize() already ensures directories exist
                services.AddWebConnectConfiguration();
                services.AddWebConnectServices();
            });
    }

    /// <summary>
    /// Registers cleanup handlers to ensure ChromeDriver processes are terminated when the application exits.
    /// </summary>
    private static void RegisterCleanupHandlers()
    {
        // Register handler for normal application exit
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            Console.WriteLine("Application exiting - cleaning up ChromeDriver processes...");
            
            BrowserManager.CleanupDriverProcesses();
            BrowserManager.CleanupOrphanedDrivers();
        };

        // Register handler for Ctrl+C and other console signals
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("Application interrupted - cleaning up ChromeDriver processes...");
            
            BrowserManager.CleanupDriverProcesses();
            BrowserManager.CleanupOrphanedDrivers();
            
            // Allow the application to exit gracefully
            e.Cancel = false;
        };

        // Register handler for unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Console.WriteLine("Unhandled exception occurred - cleaning up ChromeDriver processes...");
            
            BrowserManager.CleanupDriverProcesses();
            BrowserManager.CleanupOrphanedDrivers();
        };
    }
}
