using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using CommandLine;
using ChromeConnect.Core;
using ChromeConnect.Models;
using ChromeConnect.Services;

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
        try
        {
            // Parse command-line options first to check for debug mode and version info
            var parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args);
            CommandLineOptions options = null;
            
            // Handle the case where we just need to show version info and exit
            if (parserResult.Value?.ShowVersion == true)
            {
                Console.WriteLine($"{CoreConstants.ApplicationName} version {CoreConstants.Version}");
                return 0;
            }
            
            // Store the options if parsing was successful
            if (parserResult is Parsed<CommandLineOptions> parsed)
            {
                options = parsed.Value;
            }
            
            // Build the host
            using var host = CreateHostBuilder(args, options?.Debug ?? false).Build();
            
            if (options == null)
            {
                // If command line parsing failed, return error code
                return 1;
            }
            
            // Resolve the ChromeConnectService from the DI container and run it
            var chromeConnectService = host.Services.GetRequiredService<ChromeConnectService>();
            return await chromeConnectService.ExecuteAsync(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
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
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                // Add appsettings.json
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                
                // Add appsettings.{Environment}.json if it exists
                config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", 
                    optional: true, 
                    reloadOnChange: true);
                
                // Add environment variables
                config.AddEnvironmentVariables();
                
                // Add command-line args
                config.AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Create logs directory if it doesn't exist
                string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                // Add ChromeConnect services and configuration
                services.AddChromeConnectConfiguration(hostContext.Configuration);
                services.AddChromeConnectLogging(hostContext.Configuration, debugMode);
                services.AddChromeConnectServices();
            });
    }
}
