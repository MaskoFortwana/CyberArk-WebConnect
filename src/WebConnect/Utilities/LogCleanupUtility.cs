using System;
using System.IO;
using System.Linq;
using Serilog;

namespace WebConnect.Utilities;

/// <summary>
/// Utility class for managing log file cleanup and rotation to prevent excessive disk usage.
/// </summary>
public static class LogCleanupUtility
{
    /// <summary>
    /// Default maximum age for log files in days (30 days)
    /// </summary>
    public const int DefaultMaxLogAgeDays = 30;

    /// <summary>
    /// Default maximum total log directory size in MB (100 MB)
    /// </summary>
    public const int DefaultMaxTotalSizeMb = 100;

    /// <summary>
    /// Performs cleanup of old log files based on age and total directory size.
    /// </summary>
    /// <param name="logDirectory">The directory containing log files</param>
    /// <param name="maxAgeDays">Maximum age of log files in days (default: 30)</param>
    /// <param name="maxTotalSizeMb">Maximum total size of log directory in MB (default: 100)</param>
    public static void CleanupOldLogs(string logDirectory, int maxAgeDays = DefaultMaxLogAgeDays, int maxTotalSizeMb = DefaultMaxTotalSizeMb)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                return;
            }

            var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.CreationTime)
                .ToList();

            if (!logFiles.Any())
            {
                return;
            }

            // Remove files older than maxAgeDays
            var cutoffDate = DateTime.Now.AddDays(-maxAgeDays);
            var oldFiles = logFiles.Where(f => f.CreationTime < cutoffDate).ToList();
            
            foreach (var oldFile in oldFiles)
            {
                try
                {
                    oldFile.Delete();
                    Log.Debug("Deleted old log file: {FileName}", oldFile.Name);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to delete old log file: {FileName}", oldFile.Name);
                }
            }

            // Refresh the list after deleting old files
            logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.CreationTime)
                .ToList();

            // Check total directory size and remove oldest files if necessary
            var totalSizeBytes = logFiles.Sum(f => f.Length);
            var maxTotalSizeBytes = maxTotalSizeMb * 1024L * 1024L;

            if (totalSizeBytes > maxTotalSizeBytes)
            {
                Log.Information("Log directory size ({TotalSizeMb:F2} MB) exceeds limit ({MaxSizeMb} MB), cleaning up oldest files", 
                    totalSizeBytes / (1024.0 * 1024.0), maxTotalSizeMb);

                // Remove oldest files until we're under the size limit
                foreach (var file in logFiles)
                {
                    if (totalSizeBytes <= maxTotalSizeBytes)
                    {
                        break;
                    }

                    try
                    {
                        var fileSize = file.Length;
                        file.Delete();
                        totalSizeBytes -= fileSize;
                        Log.Debug("Deleted log file for size cleanup: {FileName} ({FileSizeMb:F2} MB)", 
                            file.Name, fileSize / (1024.0 * 1024.0));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to delete log file during size cleanup: {FileName}", file.Name);
                    }
                }
            }

            Log.Debug("Log cleanup completed. Directory: {LogDirectory}, Files remaining: {FileCount}, Total size: {TotalSizeMb:F2} MB", 
                logDirectory, 
                Directory.GetFiles(logDirectory, "*.log").Length,
                Directory.GetFiles(logDirectory, "*.log").Sum(f => new FileInfo(f).Length) / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to perform log cleanup in directory: {LogDirectory}", logDirectory);
        }
    }

    /// <summary>
    /// Gets the current total size of log files in the specified directory.
    /// </summary>
    /// <param name="logDirectory">The directory containing log files</param>
    /// <returns>Total size in bytes, or 0 if directory doesn't exist or has no log files</returns>
    public static long GetLogDirectorySize(string logDirectory)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                return 0;
            }

            return Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
                .Sum(f => new FileInfo(f).Length);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to calculate log directory size: {LogDirectory}", logDirectory);
            return 0;
        }
    }

    /// <summary>
    /// Gets the count of log files in the specified directory.
    /// </summary>
    /// <param name="logDirectory">The directory containing log files</param>
    /// <returns>Number of log files, or 0 if directory doesn't exist</returns>
    public static int GetLogFileCount(string logDirectory)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                return 0;
            }

            return Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly).Length;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to count log files: {LogDirectory}", logDirectory);
            return 0;
        }
    }
} 
