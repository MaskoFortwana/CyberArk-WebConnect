using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace WebConnect.Utilities
{
    /// <summary>
    /// Provides system-level input blocking functionality using Windows API to prevent user interference during automated processes.
    /// </summary>
    public class InputBlocker : IDisposable
    {
        private readonly ILogger<InputBlocker>? _logger;
        private bool _isBlocking = false;
        private readonly int _timeoutMs;
        private Timer? _safetyTimer;
        private static readonly object _lockObject = new object();
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the InputBlocker class.
        /// </summary>
        /// <param name="timeoutMs">Maximum time in milliseconds to keep input blocked before automatic restoration. Default is 60 seconds.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public InputBlocker(int timeoutMs = 60000, ILogger<InputBlocker>? logger = null)
        {
            _timeoutMs = timeoutMs;
            _logger = logger;
        }

        /// <summary>
        /// Starts blocking user input system-wide.
        /// </summary>
        /// <returns>True if input blocking was successfully enabled, false otherwise.</returns>
        public bool StartBlocking()
        {
            lock (_lockObject)
            {
                if (_disposed)
                {
                    _logger?.LogWarning("Cannot start blocking on disposed InputBlocker");
                    return false;
                }

                if (_isBlocking)
                {
                    _logger?.LogDebug("Input blocking already active");
                    return true;
                }

                try
                {
                    _logger?.LogInformation("Starting system-wide input blocking");
                    bool success = NativeMethods.BlockInput(true);
                    
                    if (success)
                    {
                        _isBlocking = true;
                        
                        // Start safety timer to ensure input is restored even if something goes wrong
                        _safetyTimer = new Timer(
                            _ => ForceUnblock("Safety timeout triggered"), 
                            null, 
                            _timeoutMs, 
                            Timeout.Infinite);
                        
                        _logger?.LogInformation("Input blocking activated successfully. Safety timeout: {TimeoutMs}ms", _timeoutMs);
                    }
                    else
                    {
                        _logger?.LogWarning("Failed to activate input blocking - BlockInput API returned false");
                    }
                    
                    return success;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception occurred while starting input blocking");
                    return false;
                }
            }
        }

        /// <summary>
        /// Stops blocking user input and restores normal input functionality.
        /// </summary>
        /// <returns>True if input was successfully restored, false otherwise.</returns>
        public bool StopBlocking()
        {
            lock (_lockObject)
            {
                if (_disposed)
                {
                    _logger?.LogWarning("Cannot stop blocking on disposed InputBlocker");
                    return false;
                }

                if (!_isBlocking)
                {
                    _logger?.LogDebug("Input blocking not currently active");
                    return true;
                }

                try
                {
                    _logger?.LogInformation("Stopping system-wide input blocking");
                    
                    // Dispose safety timer
                    _safetyTimer?.Dispose();
                    _safetyTimer = null;
                    
                    bool success = NativeMethods.BlockInput(false);
                    
                    if (success)
                    {
                        _isBlocking = false;
                        _logger?.LogInformation("Input blocking deactivated successfully");
                    }
                    else
                    {
                        _logger?.LogError("Failed to deactivate input blocking - BlockInput API returned false");
                        // Still mark as not blocking since we tried
                        _isBlocking = false;
                    }
                    
                    return success;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception occurred while stopping input blocking");
                    // Force unblock as safety measure
                    ForceUnblock($"Exception during stop: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets whether input blocking is currently active.
        /// </summary>
        public bool IsBlocking => _isBlocking;

        /// <summary>
        /// Emergency method to force unblock input. Used by safety timer and exception handlers.
        /// </summary>
        private void ForceUnblock(string reason)
        {
            try
            {
                _logger?.LogWarning("Force unblocking input - Reason: {Reason}", reason);
                
                // Emergency unblocking - don't rely on locks or state checks
                NativeMethods.BlockInput(false);
                _isBlocking = false;
                
                _logger?.LogInformation("Force unblock completed");
            }
            catch (Exception ex)
            {
                // This is our last resort - log but can't do much more
                _logger?.LogCritical(ex, "CRITICAL: Failed to force unblock input - system may require manual intervention");
            }
        }

        /// <summary>
        /// Disposes the InputBlocker and ensures input is restored.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_disposed)
                    return;

                _logger?.LogDebug("Disposing InputBlocker");
                
                // Ensure input is unblocked
                StopBlocking();
                
                // Clean up timer
                _safetyTimer?.Dispose();
                _safetyTimer = null;
                
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Native Windows API methods for input blocking.
    /// </summary>
    public static class NativeMethods
    {
        /// <summary>
        /// Blocks or unblocks keyboard and mouse input events from reaching applications.
        /// </summary>
        /// <param name="fBlockIt">
        /// If true, keyboard and mouse input events are blocked.
        /// If false, keyboard and mouse events are unblocked.
        /// </param>
        /// <returns>
        /// If the function succeeds, the return value is nonzero.
        /// If input is already blocked, the return value is zero.
        /// </returns>
        /// <remarks>
        /// This function requires Windows NT/2000/XP or later.
        /// When input is blocked, the system does not respond to user input from the keyboard or mouse.
        /// Applications should use this function sparingly and always ensure input is restored.
        /// </remarks>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BlockInput([MarshalAs(UnmanagedType.Bool)] bool fBlockIt);
    }
} 
