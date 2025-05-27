using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ChromeConnect.Exceptions;

namespace ChromeConnect.Core;

public class BrowserManager
{
    private readonly ILogger<BrowserManager> _logger;
    
    // Static collection to track all ChromeDriver processes started by this application
    private static readonly List<Process> _driverProcesses = new List<Process>();
    private static readonly object _processLock = new object();

    public BrowserManager(ILogger<BrowserManager> logger)
    {
        _logger = logger;
    }

    public virtual IWebDriver? LaunchBrowser(string url, bool incognito, bool kiosk, bool ignoreCertErrors)
    {
        try
        {
            _logger.LogInformation("Setting up Chrome browser");

            // Verify ChromeDriver.exe exists in application directory
            string chromeDriverPath = GetChromeDriverPath();

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
                "--disable-dev-shm-usage", // Required for some environments
                "--start-maximized", // HARDCODED: Always start Chrome in maximized state for better automation visibility
                
                // PERFORMANCE FIX: Disable TensorFlow/ML features that cause 30-second delays
                "--disable-features=VizDisplayCompositor,TranslateUI,OptimizationHints,AutofillServerCommunication",
                "--disable-machine-learning-model-service",
                "--disable-ml-model-service", 
                "--disable-optimization-guide-model-downloading",
                "--disable-component-extensions-with-background-pages",
                "--disable-background-networking",
                "--disable-background-timer-throttling",
                "--disable-renderer-backgrounding",
                "--disable-backgrounding-occluded-windows",
                "--disable-ipc-flooding-protection",
                "--disable-client-side-phishing-detection",
                "--disable-sync",
                "--disable-features=MediaRouter",
                "--disable-features=Translate"
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

            // Note: --start-maximized is now hardcoded in the default flags above
            // This conditional logic is no longer needed

            if (ignoreCertErrors)
            {
                _logger.LogDebug("Adding --ignore-certificate-errors flag");
                options.AddArgument("--ignore-certificate-errors");
            }

            // Create ChromeDriverService with the verified path
            _logger.LogDebug($"Using ChromeDriver from: {chromeDriverPath}");
            string driverDir = Path.GetDirectoryName(chromeDriverPath)!;
            ChromeDriverService chromeDriverService = ChromeDriverService.CreateDefaultService(driverDir);
            
            // Hide the command prompt window for cleaner execution
            chromeDriverService.HideCommandPromptWindow = true;

            // Create the ChromeDriver with configured options
            _logger.LogInformation("Creating Chrome driver");
            var driver = new ChromeDriver(chromeDriverService, options);
            
            // Track the ChromeDriver process for cleanup
            TrackDriverProcess(chromeDriverService);
            
            // Window size is handled by --start-maximized flag, no manual sizing needed
            // Note: In kiosk mode, --kiosk flag overrides --start-maximized anyway

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

    /// <summary>
    /// Gets the path to ChromeDriver.exe, which must be located in the same directory as the application executable.
    /// For single-file published applications, this method correctly resolves the actual executable directory
    /// rather than the temporary extraction directory used by AppContext.BaseDirectory.
    /// </summary>
    /// <returns>The full path to ChromeDriver.exe</returns>
    /// <exception cref="ChromeDriverMissingException">Thrown when ChromeDriver.exe is not found in the application directory</exception>
    private string GetChromeDriverPath()
    {
        // For single-file published applications, we need to get the actual executable location
        // rather than AppContext.BaseDirectory which points to the temporary extraction directory
        string executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        string executableDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
        
        _logger.LogDebug($"Executable path: {executablePath}");
        _logger.LogDebug($"Executable directory: {executableDirectory}");
        
        // Primary path: Look for ChromeDriver in the same directory as the executable
        string chromeDriverPath = Path.Combine(executableDirectory, "chromedriver.exe");
        _logger.LogDebug($"Looking for ChromeDriver at: {chromeDriverPath}");
        
        if (File.Exists(chromeDriverPath))
        {
            _logger.LogInformation($"ChromeDriver found at: {chromeDriverPath}");
            return chromeDriverPath;
        }
        
        // Fallback: Try AppContext.BaseDirectory for compatibility with non-single-file deployments
        string appDir = AppContext.BaseDirectory;
        string fallbackPath = Path.Combine(appDir, "chromedriver.exe");
        
        _logger.LogDebug($"ChromeDriver not found at primary location, trying fallback: {fallbackPath}");
        
        if (File.Exists(fallbackPath))
        {
            _logger.LogInformation($"ChromeDriver found at fallback location: {fallbackPath}");
            return fallbackPath;
        }
        
        // ChromeDriver.exe not found in either location - throw detailed exception
        string errorMessage = $"ChromeDriver.exe not found in application directory: {executableDirectory}\n" +
                             $"Also checked fallback location: {appDir}\n\n" +
                             "REQUIRED: ChromeDriver.exe must be placed in the same folder as ChromeConnect.exe\n\n" +
                             "To fix this issue:\n" +
                             "1. Download ChromeDriver.exe that matches your Chrome browser version\n" +
                             "2. Place chromedriver.exe in the same folder as ChromeConnect.exe\n" +
                             "3. Ensure the file is named exactly 'chromedriver.exe'\n\n" +
                             "Download ChromeDriver from: https://chromedriver.chromium.org/";
        
        _logger.LogError(errorMessage);
        throw new ChromeDriverMissingException(errorMessage, chromeDriverPath);
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

    /// <summary>
    /// Tracks a ChromeDriver process for cleanup when the application exits.
    /// </summary>
    /// <param name="chromeDriverService">The ChromeDriverService instance.</param>
    private void TrackDriverProcess(ChromeDriverService chromeDriverService)
    {
        try
        {
            // Get the process ID from the service
            if (chromeDriverService.ProcessId > 0)
            {
                var process = Process.GetProcessById(chromeDriverService.ProcessId);
                lock (_processLock)
                {
                    _driverProcesses.Add(process);
                }
                _logger.LogDebug($"ChromeDriver process {process.Id} tracked for cleanup");
            }
        }
        catch (ArgumentException)
        {
            // Process already exited or invalid ID
            _logger.LogDebug("ChromeDriver process already exited or invalid ID");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error tracking ChromeDriver process");
        }
    }

    /// <summary>
    /// Cleans up all tracked ChromeDriver processes.
    /// This method is called when the application exits to ensure no orphaned processes remain.
    /// </summary>
    public static void CleanupDriverProcesses()
    {
        lock (_processLock)
        {
            if (_driverProcesses.Count == 0)
            {
                return;
            }

            Console.WriteLine($"Cleaning up {_driverProcesses.Count} ChromeDriver process(es)...");

            foreach (var process in _driverProcesses.ToList())
            {
                try
                {
                    if (!process.HasExited)
                    {
                        Console.WriteLine($"Terminating ChromeDriver process {process.Id}");
                        process.Kill();
                        
                        // Wait up to 3 seconds for the process to exit
                        if (!process.WaitForExit(3000))
                        {
                            Console.WriteLine($"Warning: ChromeDriver process {process.Id} did not exit within timeout");
                        }
                        else
                        {
                            Console.WriteLine($"ChromeDriver process {process.Id} terminated successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error terminating ChromeDriver process {process.Id}: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        process.Dispose();
                    }
                    catch
                    {
                        // Best effort disposal
                    }
                }
            }
            
            _driverProcesses.Clear();
            Console.WriteLine("ChromeDriver cleanup completed");
        }
    }

    /// <summary>
    /// Fallback method to clean up any orphaned ChromeDriver processes that might have been missed.
    /// This is a more aggressive cleanup that finds all chromedriver.exe processes.
    /// </summary>
    public static void CleanupOrphanedDrivers()
    {
        try
        {
            var potentialDrivers = Process.GetProcessesByName("chromedriver");
            
            if (potentialDrivers.Length > 0)
            {
                Console.WriteLine($"Found {potentialDrivers.Length} potential orphaned ChromeDriver process(es)");
                
                foreach (var driver in potentialDrivers)
                {
                    try
                    {
                        Console.WriteLine($"Terminating orphaned ChromeDriver process {driver.Id}");
                        driver.Kill();
                        driver.WaitForExit(1000);
                        Console.WriteLine($"Orphaned ChromeDriver process {driver.Id} terminated");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error terminating orphaned ChromeDriver process {driver.Id}: {ex.Message}");
                    }
                    finally
                    {
                        try
                        {
                            driver.Dispose();
                        }
                        catch
                        {
                            // Best effort disposal
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during orphaned ChromeDriver cleanup: {ex.Message}");
        }
    }
}
