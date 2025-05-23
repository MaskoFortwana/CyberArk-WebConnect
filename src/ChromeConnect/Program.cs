using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using CommandLine;
using ChromeConnect.Core;
using ChromeConnect.Models;
using ChromeConnect.Services;
using Serilog;
using Serilog.Events;

namespace ChromeConnect;

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
        // Configure Serilog logger first
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        Log.Logger = new LoggerConfiguration()
            // .ReadFrom.Configuration(configuration) // Remove this line - causes issues in single-file deployment
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "chromeconnect-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger(); // Use CreateBootstrapLogger for early logging, then replaced by Host

        try
        {
            Log.Information("Starting ChromeConnect application");

            var parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args);
            CommandLineOptions options = null;
            
            if (parserResult.Value?.ShowVersion == true)
            {
                Console.WriteLine($"{CoreConstants.ApplicationName} version {CoreConstants.Version}");
                return 0;
            }
            
            if (parserResult is Parsed<CommandLineOptions> parsed)
            {
                options = parsed.Value;
            }
            
            using var host = CreateHostBuilder(args, options?.Debug ?? false, configuration).Build();
            
            if (options == null)
            {
                Log.Error("Command line argument parsing failed.");
                return 1;
            }
            
            var chromeConnectService = host.Services.GetRequiredService<ChromeConnectService>();
            return await chromeConnectService.ExecuteAsync(options);
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
    /// <param name="configuration">The configuration.</param>
    /// <returns>The configured host builder.</returns>
    private static IHostBuilder CreateHostBuilder(string[] args, bool debugMode, IConfigurationRoot configuration)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                // .ReadFrom.Configuration(context.Configuration) // Remove this line - causes issues in single-file deployment
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(debugMode ? LogEventLevel.Debug : LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "chromeconnect-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    restrictedToMinimumLevel: debugMode ? LogEventLevel.Debug : LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                // File sink should be configured in appsettings.json under "Serilog" section
            )
            .ConfigureAppConfiguration((hostContext, configBuilder) =>
            {
                // Configuration is already built and passed in, 
                // but Host.CreateDefaultBuilder adds some default sources.
                // We can clear them if we want full control from the initial IConfigurationRoot
                // configBuilder.Sources.Clear(); // Optional: if you want to remove default sources
                // configBuilder.AddConfiguration(configuration); // Add our pre-built configuration
                // Default setup is fine, CreateDefaultBuilder reads appsettings.json, env vars, cmd line already.
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Create logs directory if it doesn't exist (Serilog might need it if configured for file sink)
                string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDirectory); // Idempotent
                
                services.AddChromeConnectConfiguration(hostContext.Configuration);
                // services.AddChromeConnectLogging(hostContext.Configuration, debugMode); // REMOVED - Serilog handles this
                services.AddChromeConnectServices();
            });
    }
}
