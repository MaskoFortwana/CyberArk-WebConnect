using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using WebConnect.Configuration;

namespace WebConnect.Services;

/// <summary>
/// Factory for creating Polly resilience policies
/// </summary>
public class PolicyFactory
{
    private readonly ILogger<PolicyFactory> _logger;
    private readonly TimeoutConfig _timeoutConfig;

    public PolicyFactory(ILogger<PolicyFactory> logger, TimeoutConfig timeoutConfig)
    {
        _logger = logger;
        _timeoutConfig = timeoutConfig;
        _timeoutConfig.Validate(); // Ensure configuration is valid
    }

    /// <summary>
    /// Creates a timeout policy for login verification operations
    /// </summary>
    public IAsyncPolicy CreateLoginVerificationTimeoutPolicy()
    {
        return Policy.TimeoutAsync(
            _timeoutConfig.ExternalTimeout,
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (context, timeout, task) =>
            {
                _logger.LogWarning("Login verification timed out after {Timeout}s (external timeout)", 
                    timeout.TotalSeconds);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Creates a retry policy with exponential backoff for transient failures
    /// </summary>
    public IAsyncPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<Exception>(ex => IsTransientException(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry attempt {RetryCount} after {Delay}s due to: {Exception}",
                        retryCount, timespan.TotalSeconds, outcome?.Message);
                });
    }

    /// <summary>
    /// Creates a combined policy with timeout and retry
    /// </summary>
    public IAsyncPolicy CreateCombinedPolicy()
    {
        var timeoutPolicy = CreateLoginVerificationTimeoutPolicy();
        var retryPolicy = CreateRetryPolicy();

        // Wrap timeout around retry (timeout applies to each retry attempt)
        return Policy.WrapAsync(timeoutPolicy, retryPolicy);
    }

    /// <summary>
    /// Creates a policy specifically for URL polling operations
    /// </summary>
    public IAsyncPolicy CreateUrlPollingPolicy()
    {
        return Policy.TimeoutAsync(
            _timeoutConfig.InternalTimeout,
            TimeoutStrategy.Optimistic,
            onTimeoutAsync: (context, timeout, task) =>
            {
                _logger.LogDebug("URL polling timed out after {Timeout}s (internal timeout)", 
                    timeout.TotalSeconds);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried
    /// </summary>
    private static bool IsTransientException(Exception ex)
    {
        return ex switch
        {
            TimeoutRejectedException => false, // Don't retry timeouts
            OperationCanceledException => false, // Don't retry cancellations
            ArgumentException => false, // Don't retry argument errors
            InvalidOperationException => false, // Don't retry invalid operations
            _ => true // Retry other exceptions (WebDriver issues, network problems, etc.)
        };
    }
} 
