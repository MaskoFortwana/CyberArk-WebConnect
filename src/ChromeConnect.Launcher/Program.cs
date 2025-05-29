using System.Diagnostics;

namespace ChromeConnect.Launcher;

/// <summary>
/// ChromeConnect Launcher - Simple executable to launch the main ChromeConnect application
/// from the ChromeConnect subdirectory while maintaining the two-tier deployment structure
/// required by CyberArk PSM Components.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Entry point for the launcher
    /// </summary>
    /// <param name="args">Command line arguments to pass through to the main application</param>
    /// <returns>Exit code from the main application</returns>
    private static int Main(string[] args)
    {
        try
        {
            // Get the directory where this launcher is located
            var launcherDirectory = AppContext.BaseDirectory;
            
            // Construct path to the real ChromeConnect.exe in the subdirectory
            var chromeConnectPath = Path.Combine(launcherDirectory, "ChromeConnect", "ChromeConnect.exe");
            
            // Verify the target executable exists
            if (!File.Exists(chromeConnectPath))
            {
                Console.Error.WriteLine($"ERROR: ChromeConnect application not found at: {chromeConnectPath}");
                Console.Error.WriteLine("Please ensure the ChromeConnect subdirectory contains the application files.");
                return 1;
            }
            
            // Create process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = chromeConnectPath,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false
            };
            
            // Add all arguments passed to the launcher
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
            
            // Start the process
            using var process = Process.Start(startInfo);
            
            if (process == null)
            {
                Console.Error.WriteLine("ERROR: Failed to start ChromeConnect application");
                return 1;
            }
            
            // Wait for the process to complete and return its exit code
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to launch ChromeConnect: {ex.Message}");
            return 1;
        }
    }
} 