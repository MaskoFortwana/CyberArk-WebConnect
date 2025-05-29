using System.Diagnostics;

namespace WebConnect.Launcher;

/// <summary>
/// WebConnect Launcher - Simple executable to launch the main WebConnect application
/// from the WebConnect subdirectory while maintaining the two-tier deployment structure
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
            
            // Construct path to the real WebConnect.exe in the subdirectory
            var webConnectPath = Path.Combine(launcherDirectory, "WebConnect", "WebConnect.exe");
            
            // Verify the target executable exists
            if (!File.Exists(webConnectPath))
            {
                Console.Error.WriteLine($"ERROR: WebConnect application not found at: {webConnectPath}");
                Console.Error.WriteLine("Please ensure the WebConnect subdirectory contains the application files.");
                return 1;
            }
            
            // Create process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = webConnectPath,
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
                Console.Error.WriteLine("ERROR: Failed to start WebConnect application");
                return 1;
            }
            
            // Wait for the process to complete and return its exit code
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to launch WebConnect: {ex.Message}");
            return 1;
        }
    }
} 
