using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager;

namespace ChromeConnect.Core;

public class BrowserManager
{
    private readonly ILogger<BrowserManager> _logger;

    public BrowserManager(ILogger<BrowserManager> logger)
    {
        _logger = logger;
    }

    public virtual IWebDriver? LaunchBrowser(string url, bool incognito, bool kiosk, bool ignoreCertErrors)
    {
        try
        {
            _logger.LogInformation("Setting up Chrome browser");

            // Configure WebDriverManager for self-contained deployment
            string chromeDriverPath = SetupChromeDriver();

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
                "--disable-gpu",
                "--no-sandbox", // Required for some environments
                "--disable-dev-shm-usage" // Required for some environments
            });

            // Configure the browser to stay open after the script exits
            options.LeaveBrowserRunning = true;

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

            // Create ChromeDriverService with the correct path
            ChromeDriverService chromeDriverService;
            if (!string.IsNullOrEmpty(chromeDriverPath) && File.Exists(chromeDriverPath))
            {
                _logger.LogDebug($"Using ChromeDriver from: {chromeDriverPath}");
                string driverDir = Path.GetDirectoryName(chromeDriverPath)!;
                chromeDriverService = ChromeDriverService.CreateDefaultService(driverDir);
            }
            else
            {
                _logger.LogDebug("Using default ChromeDriverService");
                chromeDriverService = ChromeDriverService.CreateDefaultService();
            }

            // Create the ChromeDriver with configured options
            _logger.LogInformation("Creating Chrome driver");
            var driver = new ChromeDriver(chromeDriverService, options);
            
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

    private string SetupChromeDriver()
    {
        try
        {
            _logger.LogInformation("Setting up ChromeDriver using WebDriverManager");
            
            // Get the application directory
            string appDir = AppContext.BaseDirectory;
            string driversDir = Path.Combine(appDir, "drivers");
            
            // Ensure drivers directory exists
            Directory.CreateDirectory(driversDir);
            
            // Configure WebDriverManager with custom cache path for self-contained deployment
            var driverManager = new DriverManager();
            
            // Set up the Chrome driver configuration
            var chromeConfig = new ChromeConfig();
            
            // Try to set up the driver and get its path
            string driverPath = driverManager.SetUpDriver(chromeConfig);
            
            _logger.LogInformation($"ChromeDriver set up successfully at: {driverPath}");
            return driverPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebDriverManager setup failed, falling back to system PATH");
            
            // Try to find chromedriver in system PATH or common locations
            string[] commonPaths = {
                "chromedriver.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "chromedriver.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chromedriver.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chromedriver.exe")
            };
            
            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation($"Found ChromeDriver at: {path}");
                    return path;
                }
            }
            
            _logger.LogWarning("ChromeDriver not found in common locations, will use Selenium's default behavior");
            return string.Empty;
        }
    }

    public virtual void CloseBrowser(IWebDriver? driver)
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
