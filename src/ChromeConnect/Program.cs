using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CommandLine;
using ChromeConnect.Core;

namespace ChromeConnect;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Set up dependency injection
            var serviceProvider = new ServiceCollection()
                .AddLogging(configure => configure.AddConsole())
                .AddSingleton<BrowserManager>()
                .AddSingleton<LoginDetector>()
                .AddSingleton<CredentialManager>()
                .AddSingleton<LoginVerifier>()
                .BuildServiceProvider();

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("ChromeConnect starting");

            // Parse command-line arguments
            var result = await Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult(
                    async (CommandLineOptions options) =>
                    {
                        return await RunWithOptionsAsync(options, serviceProvider);
                    },
                    errors =>
                    {
                        logger.LogError("Invalid command-line arguments");
                        return Task.FromResult(1);
                    });

            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunWithOptionsAsync(CommandLineOptions options, ServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var browserManager = serviceProvider.GetRequiredService<BrowserManager>();
        var loginDetector = serviceProvider.GetRequiredService<LoginDetector>();
        var credentialManager = serviceProvider.GetRequiredService<CredentialManager>();
        var loginVerifier = serviceProvider.GetRequiredService<LoginVerifier>();

        // Log options (masking sensitive fields)
        logger.LogInformation("URL: {Url}", options.Url);
        logger.LogInformation("Username: {Username}", options.Username);
        logger.LogInformation("Password: ****");
        logger.LogInformation("Domain: {Domain}", options.Domain);
        logger.LogInformation("Incognito: {Incognito}", options.Incognito ? "yes" : "no");
        logger.LogInformation("Kiosk: {Kiosk}", options.Kiosk ? "yes" : "no");
        logger.LogInformation("Certificate Validation: {CertVal}", options.IgnoreCertErrors ? "ignore" : "enforce");

        // Launch browser
        logger.LogInformation("Launching Chrome browser");
        var driver = browserManager.LaunchBrowser(
            options.Url,
            options.Incognito,
            options.Kiosk,
            options.IgnoreCertErrors);

        if (driver == null)
        {
            logger.LogError("Failed to launch browser");
            return 1;
        }

        try
        {
            // Detect login form
            logger.LogInformation("Detecting login form");
            var loginForm = await loginDetector.DetectLoginFormAsync(driver);
            
            if (loginForm == null)
            {
                logger.LogError("Could not detect login form");
                return 1;
            }

            // Enter credentials
            logger.LogInformation("Entering credentials");
            bool credentialsEntered = await credentialManager.EnterCredentialsAsync(
                driver, loginForm, options.Username, options.Password, options.Domain);
            
            if (!credentialsEntered)
            {
                logger.LogError("Failed to enter credentials");
                return 1;
            }

            // Verify login success
            logger.LogInformation("Verifying login success");
            bool loginSuccess = await loginVerifier.VerifyLoginSuccessAsync(driver);

            if (loginSuccess)
            {
                logger.LogInformation("Login successful!");
                logger.LogInformation("Browser will remain open. Script exiting.");
                return 0; // Success
            }
            else
            {
                logger.LogError("Login failed");
                browserManager.CloseBrowser(driver);
                return 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during login process");
            browserManager.CloseBrowser(driver);
            return 1;
        }
    }
}
