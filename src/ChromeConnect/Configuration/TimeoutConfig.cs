using System;

namespace ChromeConnect.Configuration;

/// <summary>
/// Configuration for timeout settings in ChromeConnect
/// </summary>
public class TimeoutConfig
{
    /// <summary>
    /// Internal timeout for login verification operations (default: 15 seconds)
    /// </summary>
    public TimeSpan InternalTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// External timeout that wraps internal operations (default: 20 seconds)
    /// Should be slightly longer than InternalTimeout to allow proper cleanup
    /// </summary>
    public TimeSpan ExternalTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Initial delay before starting verification checks (default: 100ms)
    /// Reduced from 500ms for faster response to immediate redirects
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Timeout for quick error detection phase (default: 2 seconds)
    /// </summary>
    public TimeSpan QuickErrorTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Polling interval for URL change detection (default: 100ms)
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum time to allocate per verification method (default: 3 seconds)
    /// </summary>
    public TimeSpan MaxTimePerMethod { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Minimum time to allocate per verification method (default: 1 second)
    /// </summary>
    public TimeSpan MinTimePerMethod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Validates the timeout configuration to ensure proper cascading
    /// </summary>
    public void Validate()
    {
        if (ExternalTimeout <= InternalTimeout)
        {
            throw new InvalidOperationException(
                $"ExternalTimeout ({ExternalTimeout.TotalSeconds}s) must be greater than InternalTimeout ({InternalTimeout.TotalSeconds}s)");
        }

        if (InitialDelay >= InternalTimeout)
        {
            throw new InvalidOperationException(
                $"InitialDelay ({InitialDelay.TotalMilliseconds}ms) must be less than InternalTimeout ({InternalTimeout.TotalSeconds}s)");
        }

        if (MinTimePerMethod >= MaxTimePerMethod)
        {
            throw new InvalidOperationException(
                $"MinTimePerMethod ({MinTimePerMethod.TotalSeconds}s) must be less than MaxTimePerMethod ({MaxTimePerMethod.TotalSeconds}s)");
        }

        if (PollingInterval >= InternalTimeout)
        {
            throw new InvalidOperationException(
                $"PollingInterval ({PollingInterval.TotalMilliseconds}ms) must be much less than InternalTimeout ({InternalTimeout.TotalSeconds}s)");
        }
    }
} 