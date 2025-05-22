using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager.Helpers;

namespace ChromeConnect.Core;

public class BrowserManager
{
    private readonly ILogger<BrowserManager> _logger;

    public BrowserManager(ILogger<BrowserManager> logger)
    {
        _logger = logger;
    }

    public IWebDriver? LaunchBrowser(string url, bool incognito, bool kiosk, bool ignoreCertErrors)
    {
        try
        {
            _logger.LogInformation("Setting up Chrome browser");

            // Ensure ChromeDriver is installed and configured
            new DriverManager().SetUpDriver(new ChromeConfig(), VersionResolveStrategy.MatchingBrowser);

            // Configure Chrome options
            var options = new ChromeOptions();
            
            // Default Chrome flags that should always be present
            options.AddArguments(new List<string>
            {
                "--no-first-run",
                "--no-default-browser-check",
                "--disable-translate",
                "--disable-extensions",
                "--disable-infobars",
                "--disable-gpu"
            });

            // Configure the browser to stay open after the script exits
            options.AddAdditionalChromeOption("detach", true);

            // Add optional flags based on parameters
            if (incognito)
            {
                _logger.LogDebug("Adding --incognito flag");
                options.AddArgument("--incognito");
            }

            if (kiosk)
            {
                _logger.LogDebug("Adding --kiosk flag");
                options.AddArgument("--kiosk");
            }

            if (ignoreCertErrors)
            {
                _logger.LogDebug("Adding --ignore-certificate-errors flag");
                options.AddArgument("--ignore-certificate-errors");
            }

            // Create the ChromeDriver with configured options
            _logger.LogInformation("Creating Chrome driver");
            var driver = new ChromeDriver(options);
            
            // Set window size if not in kiosk mode
            if (!kiosk)
            {
                driver.Manage().Window.Size = new System.Drawing.Size(1280, 1024);
            }

            // Set timeouts
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

            // Navigate to the specified URL
            _logger.LogInformation($"Navigating to URL: {url}");
            driver.Navigate().GoToUrl(url);

            return driver;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch Chrome browser");
            return null;
        }
    }

    public void CloseBrowser(IWebDriver? driver)
    {
        if (driver != null)
        {
            try
            {
                _logger.LogInformation("Closing Chrome browser");
                driver.Quit();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing browser");
            }
        }
    }
}
