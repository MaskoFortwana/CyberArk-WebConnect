using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebConnect.Exceptions;

namespace WebConnect.Services
{
    /// <summary>
    /// Service responsible for monitoring, aggregating, and reporting errors.
    /// </summary>
    public class ErrorMonitor
    {
        private readonly ILogger<ErrorMonitor> _logger;
        private readonly ErrorMonitorSettings _settings;
        private readonly ConcurrentDictionary<string, ErrorMetrics> _errorMetrics = new ConcurrentDictionary<string, ErrorMetrics>();
        private readonly ConcurrentQueue<ErrorEvent> _recentErrors = new ConcurrentQueue<ErrorEvent>();
        private DateTime _lastReportTime = DateTime.UtcNow;
        private int _totalErrorCount = 0;
        private Timer? _reportingTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorMonitor"/> class.
        /// </summary>
        /// <param name="logger">The logger to use for reporting.</param>
        /// <param name="settings">Optional settings for the error monitor.</param>
        public ErrorMonitor(
            ILogger<ErrorMonitor> logger,
            ErrorMonitorSettings? settings = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? new ErrorMonitorSettings();
            
            if (_settings.EnablePeriodicReporting)
            {
                // Set up timer for periodic reporting
                _reportingTimer = new Timer(
                    ReportErrorStatistics, 
                    null, 
                    TimeSpan.FromSeconds(_settings.ReportingIntervalSeconds),
                    TimeSpan.FromSeconds(_settings.ReportingIntervalSeconds));
            }
        }

        /// <summary>
        /// Records an error event for monitoring.
        /// </summary>
        /// <param name="exception">The exception to record.</param>
        /// <param name="source">Optional source identifier for the error.</param>
        /// <param name="additionalData">Optional additional contextual data about the error.</param>
        public void RecordError(Exception exception, string? source = null, Dictionary<string, object>? additionalData = null)
        {
            if (exception == null) return;
            
            try
            {
                // Increment total error count
                Interlocked.Increment(ref _totalErrorCount);
                
                // Create categorized error key
                string errorTypeKey = GetErrorTypeKey(exception);
                
                // Update metrics for this error type
                _errorMetrics.AddOrUpdate(
                    errorTypeKey,
                    // Add new metrics if this is the first occurrence
                    key => new ErrorMetrics { 
                        ErrorType = exception.GetType().Name,
                        FirstOccurrence = DateTime.UtcNow,
                        LastOccurrence = DateTime.UtcNow,
                        Count = 1
                    },
                    // Update existing metrics
                    (key, existing) => {
                        existing.LastOccurrence = DateTime.UtcNow;
                        existing.Count++;
                        return existing;
                    });
                
                // Store recent error details if enabled
                if (_settings.TrackRecentErrors)
                {
                    // Create error event
                    var errorEvent = new ErrorEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        Exception = exception,
                        Source = source ?? "Unknown",
                        AdditionalData = additionalData ?? new Dictionary<string, object>()
                    };
                    
                    // Add to recent errors queue
                    _recentErrors.Enqueue(errorEvent);
                    
                    // Ensure queue doesn't exceed max size
                    while (_recentErrors.Count > _settings.MaxRecentErrorsTracked)
                    {
                        _recentErrors.TryDequeue(out _);
                    }
                }
                
                // Check for error threshold alerts
                CheckErrorThresholds(errorTypeKey);
                
                // Get error severity level
                LogLevel severity = GetSeverityLevel(exception);
                
                // Log the error categorization
                _logger.Log(severity, "Error categorized as {ErrorType} from {Source}", 
                    errorTypeKey, source ?? "Unknown");
            }
            catch (Exception ex)
            {
                // Don't let errors in the error monitor bring down the app
                _logger.LogError(ex, "Error in ErrorMonitor.RecordError");
            }
        }

        /// <summary>
        /// Gets metrics for all errors recorded since the monitor was created.
        /// </summary>
        /// <returns>A collection of error metrics.</returns>
        public IEnumerable<ErrorMetrics> GetAllErrorMetrics()
        {
            return _errorMetrics.Values.ToList();
        }

        /// <summary>
        /// Gets the most recent error events up to the configured limit.
        /// </summary>
        /// <param name="count">Optional number of recent errors to retrieve. Defaults to all available.</param>
        /// <returns>A collection of recent error events.</returns>
        public IEnumerable<ErrorEvent> GetRecentErrors(int? count = null)
        {
            int limit = count ?? _recentErrors.Count;
            return _recentErrors.Take(Math.Min(limit, _recentErrors.Count)).ToList();
        }

        /// <summary>
        /// Gets metrics for a specific error type.
        /// </summary>
        /// <param name="errorType">The error type name or key.</param>
        /// <returns>The error metrics, or null if no metrics exist for the specified type.</returns>
        public ErrorMetrics GetErrorMetricsForType(string errorType)
        {
            _errorMetrics.TryGetValue(errorType, out var metrics);
            return metrics;
        }

        /// <summary>
        /// Gets the total number of errors recorded since the monitor was created.
        /// </summary>
        /// <returns>The total error count.</returns>
        public int GetTotalErrorCount()
        {
            return _totalErrorCount;
        }

        /// <summary>
        /// Generates an error report for the specified time period.
        /// </summary>
        /// <param name="sinceDateUtc">Optional start date for the report period. If null, includes all recorded errors.</param>
        /// <returns>An error report object.</returns>
        public ErrorReport GenerateReport(DateTime? sinceDateUtc = null)
        {
            DateTime startDate = sinceDateUtc ?? DateTime.MinValue;
            DateTime now = DateTime.UtcNow;
            
            var report = new ErrorReport
            {
                GeneratedAt = now,
                ReportPeriodStart = startDate,
                ReportPeriodEnd = now,
                TotalErrorCount = _totalErrorCount,
                ErrorCountsByType = new Dictionary<string, int>(),
                MostFrequentErrors = new List<ErrorTypeFrequency>(),
                RecentErrors = new List<ErrorEvent>()
            };
            
            // Add error counts by type
            foreach (var metrics in _errorMetrics.Values)
            {
                if (metrics.LastOccurrence >= startDate)
                {
                    report.ErrorCountsByType[metrics.ErrorType] = metrics.Count;
                }
            }
            
            // Add most frequent errors
            report.MostFrequentErrors = _errorMetrics.Values
                .Where(m => m.LastOccurrence >= startDate)
                .OrderByDescending(m => m.Count)
                .Take(10)
                .Select(m => new ErrorTypeFrequency { ErrorType = m.ErrorType, Count = m.Count })
                .ToList();
            
            // Add recent errors
            report.RecentErrors = _recentErrors
                .Where(e => e.Timestamp >= startDate)
                .Take(10)
                .ToList();
            
            return report;
        }

        /// <summary>
        /// Resets all error metrics and recent error tracking.
        /// </summary>
        public void Reset()
        {
            _errorMetrics.Clear();
            
            while (_recentErrors.TryDequeue(out _)) { }
            
            _totalErrorCount = 0;
            _lastReportTime = DateTime.UtcNow;
            
            _logger.LogInformation("Error monitor reset");
        }

        /// <summary>
        /// Disposes resources used by the error monitor.
        /// </summary>
        public void Dispose()
        {
            _reportingTimer?.Dispose();
        }

        #region Private Methods

        /// <summary>
        /// Gets a key that categorizes the error type.
        /// </summary>
        private string GetErrorTypeKey(Exception exception)
        {
            // For WebConnect exceptions, use the most specific type name
            var exceptionTypeName = GetExceptionTypeName(exception);
            
            // Add error code if it's a WebConnect exception with an error code
            if (exception is WebConnectException ccException &&
                !string.IsNullOrEmpty(ccException.ErrorCode))
            {
                return $"{exceptionTypeName}:{ccException.ErrorCode}";
            }
            
            return exceptionTypeName;
        }

        /// <summary>
        /// Determines the severity level for an exception.
        /// </summary>
        private LogLevel GetSeverityLevel(Exception exception)
        {
            // Cancelled operations are warnings
            if (exception is AppOperationCanceledException)
                return LogLevel.Warning;
                
            // Authentication/authorization errors are warnings
            if (exception is InvalidCredentialsException)
                return LogLevel.Warning;
                
            // Default most exceptions to error level
            return LogLevel.Error;
        }

        /// <summary>
        /// Checks if any error thresholds have been exceeded and logs alerts.
        /// </summary>
        private void CheckErrorThresholds(string errorTypeKey)
        {
            if (!_settings.EnableErrorThresholdAlerts)
                return;
                
            // Check if we have metrics for this error type
            if (_errorMetrics.TryGetValue(errorTypeKey, out var metrics))
            {
                // Alert for frequency thresholds
                if (metrics.Count == _settings.AlertThreshold ||
                    (metrics.Count > _settings.AlertThreshold && 
                     metrics.Count % _settings.AlertThreshold == 0))
                {
                    _logger.LogCritical(
                        "ERROR THRESHOLD ALERT: {ErrorType} has occurred {Count} times since {FirstOccurrence}",
                        metrics.ErrorType,
                        metrics.Count,
                        metrics.FirstOccurrence);
                }
                
                // Alert for repeated errors in short time period
                TimeSpan timeSinceFirst = DateTime.UtcNow - metrics.FirstOccurrence;
                if (metrics.Count >= 5 && timeSinceFirst.TotalMinutes <= 5)
                {
                    _logger.LogCritical(
                        "RAPID ERROR ALERT: {ErrorType} has occurred {Count} times in the last 5 minutes",
                        metrics.ErrorType,
                        metrics.Count);
                }
            }
            
            // Check for overall error rate alert
            TimeSpan timeSinceLast = DateTime.UtcNow - _lastReportTime;
            if (timeSinceLast.TotalMinutes >= 1.0)
            {
                double errorRate = _totalErrorCount / timeSinceLast.TotalMinutes;
                if (errorRate >= _settings.ErrorRateAlertThreshold)
                {
                    _logger.LogCritical(
                        "ERROR RATE ALERT: {ErrorRate:F2} errors/minute observed over the last {Minutes:F1} minutes",
                        errorRate,
                        timeSinceLast.TotalMinutes);
                }
                
                _lastReportTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Generates and logs periodic error statistics.
        /// </summary>
        private void ReportErrorStatistics(object state)
        {
            try
            {
                // Skip if no errors
                if (_totalErrorCount == 0)
                    return;
                    
                // Generate report for the interval
                ErrorReport report = GenerateReport(_lastReportTime);
                
                // Log summary
                StringBuilder summary = new StringBuilder();
                summary.AppendLine($"Error Report ({report.ReportPeriodStart:HH:mm:ss} - {report.ReportPeriodEnd:HH:mm:ss}):");
                summary.AppendLine($"Total Errors: {report.TotalErrorCount}");
                
                if (report.MostFrequentErrors.Any())
                {
                    summary.AppendLine("Most Frequent Errors:");
                    foreach (var freq in report.MostFrequentErrors.Take(3))
                    {
                        summary.AppendLine($"  - {freq.ErrorType}: {freq.Count} occurrences");
                    }
                }
                
                _logger.LogInformation(summary.ToString());
                
                // Update last report time
                _lastReportTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                // Don't let errors in the reporting bring down the app
                _logger.LogError(ex, "Error in ErrorMonitor.ReportErrorStatistics");
            }
        }

        #endregion
    }

    /// <summary>
    /// Metrics for a specific type of error.
    /// </summary>
    public class ErrorMetrics
    {
        /// <summary>
        /// Gets or sets the error type.
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the first time this error type was observed.
        /// </summary>
        public DateTime FirstOccurrence { get; set; }

        /// <summary>
        /// Gets or sets the most recent time this error type was observed.
        /// </summary>
        public DateTime LastOccurrence { get; set; }

        /// <summary>
        /// Gets or sets the number of occurrences of this error type.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Represents a single error event with context.
    /// </summary>
    public class ErrorEvent
    {
        /// <summary>
        /// Gets or sets the timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the exception that occurred.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets the source of the error.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional contextual data about the error.
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents the frequency of a specific error type.
    /// </summary>
    public class ErrorTypeFrequency
    {
        /// <summary>
        /// Gets or sets the error type.
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the count of occurrences.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Represents a report of error statistics for a time period.
    /// </summary>
    public class ErrorReport
    {
        /// <summary>
        /// Gets or sets the time when the report was generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Gets or sets the start of the reporting period.
        /// </summary>
        public DateTime ReportPeriodStart { get; set; }

        /// <summary>
        /// Gets or sets the end of the reporting period.
        /// </summary>
        public DateTime ReportPeriodEnd { get; set; }

        /// <summary>
        /// Gets or sets the total count of errors in the reporting period.
        /// </summary>
        public int TotalErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the counts of errors by type in the reporting period.
        /// </summary>
        public Dictionary<string, int> ErrorCountsByType { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets or sets the most frequent error types in the reporting period.
        /// </summary>
        public List<ErrorTypeFrequency> MostFrequentErrors { get; set; } = new List<ErrorTypeFrequency>();

        /// <summary>
        /// Gets or sets the recent error events in the reporting period.
        /// </summary>
        public List<ErrorEvent> RecentErrors { get; set; } = new List<ErrorEvent>();
    }

    /// <summary>
    /// Settings for the <see cref="ErrorMonitor"/> class.
    /// </summary>
    public class ErrorMonitorSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to track recent error details.
        /// </summary>
        public bool TrackRecentErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of recent errors to track.
        /// </summary>
        public int MaxRecentErrorsTracked { get; set; } = 100;

        /// <summary>
        /// Gets or sets a value indicating whether to enable error threshold alerts.
        /// </summary>
        public bool EnableErrorThresholdAlerts { get; set; } = true;

        /// <summary>
        /// Gets or sets the threshold count that triggers an alert.
        /// </summary>
        public int AlertThreshold { get; set; } = 10;

        /// <summary>
        /// Gets or sets the error rate (errors per minute) that triggers a rate alert.
        /// </summary>
        public double ErrorRateAlertThreshold { get; set; } = 5.0;

        /// <summary>
        /// Gets or sets a value indicating whether to enable periodic error reporting.
        /// </summary>
        public bool EnablePeriodicReporting { get; set; } = true;

        /// <summary>
        /// Gets or sets the interval in seconds for periodic error reporting.
        /// </summary>
        public int ReportingIntervalSeconds { get; set; } = 300; // 5 minutes
    }
} 
