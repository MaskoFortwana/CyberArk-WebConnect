using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ChromeConnect.Models;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Service for tracking detection method performance, selector stability, and success rates
    /// to enhance the scoring system and guide fallback strategies.
    /// </summary>
    public class DetectionMetricsService
    {
        private readonly ILogger<DetectionMetricsService> _logger;
        private readonly ConcurrentDictionary<string, DetectionMethodMetrics> _methodMetrics = new();
        private readonly ConcurrentDictionary<string, SelectorStabilityData> _selectorStability = new();
        private readonly ConcurrentQueue<DetectionAttempt> _recentAttempts = new();
        private readonly object _lockObject = new object();
        
        private const int MaxRecentAttemptsTracked = 1000;
        private const int StabilityAnalysisWindowDays = 30;

        public DetectionMetricsService(ILogger<DetectionMetricsService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Records the start of a detection attempt for tracking purposes.
        /// </summary>
        /// <param name="url">The URL being analyzed</param>
        /// <param name="method">The detection method being used</param>
        /// <returns>A tracking ID for this detection attempt</returns>
        public string StartDetectionAttempt(string url, DetectionMethod method)
        {
            var attemptId = Guid.NewGuid().ToString("N")[..8]; // Short ID for logging
            
            var attempt = new DetectionAttempt
            {
                Id = attemptId,
                Url = url,
                Method = method,
                StartTime = DateTime.UtcNow,
                Status = DetectionStatus.InProgress
            };

            lock (_lockObject)
            {
                _recentAttempts.Enqueue(attempt);
                
                // Maintain queue size
                while (_recentAttempts.Count > MaxRecentAttemptsTracked)
                {
                    _recentAttempts.TryDequeue(out _);
                }
            }

            _logger.LogDebug($"Started detection attempt {attemptId} using {method} for URL: {url}");
            return attemptId;
        }

        /// <summary>
        /// Records a successful detection with detailed element information for analysis.
        /// </summary>
        /// <param name="attemptId">The tracking ID from StartDetectionAttempt</param>
        /// <param name="elements">The successfully detected form elements</param>
        /// <param name="confidence">Confidence score of the detection (0-100)</param>
        /// <param name="selectorDetails">Details about the selectors used for tracking stability</param>
        public void RecordSuccess(string attemptId, LoginFormElements elements, int confidence, Dictionary<string, SelectorDetails> selectorDetails)
        {
            var attempt = FindAttempt(attemptId);
            if (attempt == null)
            {
                _logger.LogWarning($"Could not find detection attempt with ID: {attemptId}");
                return;
            }

            attempt.Status = DetectionStatus.Success;
            attempt.EndTime = DateTime.UtcNow;
            attempt.Duration = attempt.EndTime.Value - attempt.StartTime;
            attempt.Confidence = confidence;
            attempt.ElementsFound = CountDetectedElements(elements);

            // Update method metrics
            UpdateMethodMetrics(attempt.Method, true, attempt.Duration.Value, confidence);
            
            // Track selector stability
            if (selectorDetails != null)
            {
                UpdateSelectorStability(attempt.Url, selectorDetails, true);
            }

            _logger.LogInformation($"Detection attempt {attemptId} succeeded with {confidence}% confidence in {attempt.Duration.Value.TotalMilliseconds:F0}ms");
        }

        /// <summary>
        /// Records a failed detection attempt for analysis.
        /// </summary>
        /// <param name="attemptId">The tracking ID from StartDetectionAttempt</param>
        /// <param name="reason">The reason for failure</param>
        /// <param name="selectorDetails">Details about the selectors that failed</param>
        public void RecordFailure(string attemptId, string reason, Dictionary<string, SelectorDetails>? selectorDetails = null)
        {
            var attempt = FindAttempt(attemptId);
            if (attempt == null)
            {
                _logger.LogWarning($"Could not find detection attempt with ID: {attemptId}");
                return;
            }

            attempt.Status = DetectionStatus.Failed;
            attempt.EndTime = DateTime.UtcNow;
            attempt.Duration = attempt.EndTime.Value - attempt.StartTime;
            attempt.FailureReason = reason;

            // Update method metrics
            UpdateMethodMetrics(attempt.Method, false, attempt.Duration.Value, 0);
            
            // Track selector stability for failures too
            if (selectorDetails != null)
            {
                UpdateSelectorStability(attempt.Url, selectorDetails, false);
            }

            _logger.LogWarning($"Detection attempt {attemptId} failed: {reason} (Duration: {attempt.Duration.Value.TotalMilliseconds:F0}ms)");
        }

        /// <summary>
        /// Gets the success rate for a specific detection method over the specified time period.
        /// </summary>
        /// <param name="method">The detection method to analyze</param>
        /// <param name="sinceDays">Number of days to look back (default: 7)</param>
        /// <returns>Success rate as a percentage (0-100)</returns>
        public double GetSuccessRate(DetectionMethod method, int sinceDays = 7)
        {
            if (!_methodMetrics.TryGetValue(method.ToString(), out var metrics))
                return 0.0;

            var cutoffDate = DateTime.UtcNow.AddDays(-sinceDays);
            var recentAttempts = _recentAttempts.Where(a => a.Method == method && a.StartTime >= cutoffDate).ToList();
            
            if (!recentAttempts.Any())
                return metrics.OverallSuccessRate;

            var successCount = recentAttempts.Count(a => a.Status == DetectionStatus.Success);
            return (double)successCount / recentAttempts.Count * 100.0;
        }

        /// <summary>
        /// Gets the average confidence score for successful detections using the specified method.
        /// </summary>
        /// <param name="method">The detection method to analyze</param>
        /// <param name="sinceDays">Number of days to look back (default: 7)</param>
        /// <returns>Average confidence score (0-100)</returns>
        public double GetAverageConfidence(DetectionMethod method, int sinceDays = 7)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-sinceDays);
            var successfulAttempts = _recentAttempts
                .Where(a => a.Method == method && 
                           a.StartTime >= cutoffDate && 
                           a.Status == DetectionStatus.Success &&
                           a.Confidence.HasValue)
                .ToList();

            return successfulAttempts.Any() ? successfulAttempts.Average(a => a.Confidence.Value) : 0.0;
        }

        /// <summary>
        /// Gets selector stability metrics for a specific URL or pattern.
        /// </summary>
        /// <param name="urlPattern">URL pattern to check stability for</param>
        /// <returns>Stability score (0-100) where higher means more stable</returns>
        public double GetSelectorStability(string urlPattern)
        {
            var domain = ExtractDomain(urlPattern);
            if (!_selectorStability.TryGetValue(domain, out var stability))
                return 50.0; // Neutral score for unknown selectors

            var recentData = stability.StabilityHistory
                .Where(h => h.Timestamp >= DateTime.UtcNow.AddDays(-StabilityAnalysisWindowDays))
                .ToList();

            if (!recentData.Any())
                return 50.0;

            // Calculate stability based on success rate and consistency
            var successRate = recentData.Count(h => h.WasSuccessful) / (double)recentData.Count;
            var consistencyScore = CalculateConsistencyScore(recentData);
            
            return (successRate * 0.7 + consistencyScore * 0.3) * 100.0;
        }

        /// <summary>
        /// Gets comprehensive detection analytics for the specified time period.
        /// </summary>
        /// <param name="sinceDays">Number of days to analyze (default: 7)</param>
        /// <returns>Detection analytics report</returns>
        public DetectionAnalytics GetAnalytics(int sinceDays = 7)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-sinceDays);
            var recentAttempts = _recentAttempts.Where(a => a.StartTime >= cutoffDate).ToList();

            var analytics = new DetectionAnalytics
            {
                ReportPeriod = TimeSpan.FromDays(sinceDays),
                GeneratedAt = DateTime.UtcNow,
                TotalAttempts = recentAttempts.Count,
                SuccessfulAttempts = recentAttempts.Count(a => a.Status == DetectionStatus.Success),
                MethodPerformance = new Dictionary<DetectionMethod, MethodPerformance>()
            };

            // Calculate overall success rate
            analytics.OverallSuccessRate = analytics.TotalAttempts > 0 
                ? (double)analytics.SuccessfulAttempts / analytics.TotalAttempts * 100.0 
                : 0.0;

            // Calculate average detection time
            var completedAttempts = recentAttempts.Where(a => a.Duration.HasValue).ToList();
            analytics.AverageDetectionTime = completedAttempts.Any() 
                ? completedAttempts.Average(a => a.Duration.Value.TotalMilliseconds) 
                : 0.0;

            // Analyze performance by method
            foreach (DetectionMethod method in Enum.GetValues<DetectionMethod>())
            {
                var methodAttempts = recentAttempts.Where(a => a.Method == method).ToList();
                if (methodAttempts.Any())
                {
                    var successful = methodAttempts.Count(a => a.Status == DetectionStatus.Success);
                    var avgConfidence = methodAttempts
                        .Where(a => a.Status == DetectionStatus.Success && a.Confidence.HasValue)
                        .Select(a => a.Confidence.Value)
                        .DefaultIfEmpty(0)
                        .Average();

                    analytics.MethodPerformance[method] = new MethodPerformance
                    {
                        TotalAttempts = methodAttempts.Count,
                        SuccessfulAttempts = successful,
                        SuccessRate = (double)successful / methodAttempts.Count * 100.0,
                        AverageConfidence = avgConfidence,
                        AverageDetectionTime = methodAttempts
                            .Where(a => a.Duration.HasValue)
                            .Select(a => a.Duration.Value.TotalMilliseconds)
                            .DefaultIfEmpty(0)
                            .Average()
                    };
                }
            }

            return analytics;
        }

        /// <summary>
        /// Gets the recommended detection method based on historical performance for a given URL.
        /// </summary>
        /// <param name="url">The URL to analyze</param>
        /// <returns>Recommended method with confidence reasoning</returns>
        public MethodRecommendation GetRecommendedMethod(string url)
        {
            var domain = ExtractDomain(url);
            var domainAttempts = _recentAttempts
                .Where(a => ExtractDomain(a.Url) == domain)
                .ToList();

            if (!domainAttempts.Any())
            {
                // No historical data, return default recommendation
                return new MethodRecommendation
                {
                    Method = DetectionMethod.UrlSpecific,
                    Confidence = 50.0,
                    Reasoning = "No historical data available. Starting with URL-specific configuration."
                };
            }

            // Analyze success rates by method for this domain
            var methodScores = new Dictionary<DetectionMethod, double>();
            
            foreach (DetectionMethod method in Enum.GetValues<DetectionMethod>())
            {
                var methodAttempts = domainAttempts.Where(a => a.Method == method).ToList();
                if (methodAttempts.Any())
                {
                    var successRate = methodAttempts.Count(a => a.Status == DetectionStatus.Success) / (double)methodAttempts.Count;
                    var avgConfidence = methodAttempts
                        .Where(a => a.Status == DetectionStatus.Success && a.Confidence.HasValue)
                        .Select(a => a.Confidence.Value)
                        .DefaultIfEmpty(0)
                        .Average();
                    
                    // Combine success rate and confidence for overall score
                    methodScores[method] = (successRate * 100.0 * 0.7) + (avgConfidence * 0.3);
                }
            }

            if (methodScores.Any())
            {
                var bestMethod = methodScores.OrderByDescending(kvp => kvp.Value).First();
                return new MethodRecommendation
                {
                    Method = bestMethod.Key,
                    Confidence = bestMethod.Value,
                    Reasoning = $"Based on {domainAttempts.Count} historical attempts for domain '{domain}'. " +
                              $"Method has {GetSuccessRate(bestMethod.Key):F1}% success rate."
                };
            }

            // Fallback to default
            return new MethodRecommendation
            {
                Method = DetectionMethod.UrlSpecific,
                Confidence = 50.0,
                Reasoning = "Insufficient historical performance data. Using default method."
            };
        }

        #region Private Helper Methods

        private DetectionAttempt? FindAttempt(string attemptId)
        {
            return _recentAttempts.FirstOrDefault(a => a.Id == attemptId);
        }

        private int CountDetectedElements(LoginFormElements elements)
        {
            int count = 0;
            if (elements.UsernameField != null) count++;
            if (elements.PasswordField != null) count++;
            if (elements.DomainField != null) count++;
            if (elements.SubmitButton != null) count++;
            return count;
        }

        private void UpdateMethodMetrics(DetectionMethod method, bool success, TimeSpan duration, int confidence)
        {
            var key = method.ToString();
            _methodMetrics.AddOrUpdate(key,
                // Add new metrics
                new DetectionMethodMetrics
                {
                    Method = method,
                    TotalAttempts = 1,
                    SuccessfulAttempts = success ? 1 : 0,
                    OverallSuccessRate = success ? 100.0 : 0.0,
                    AverageDetectionTime = duration.TotalMilliseconds,
                    AverageConfidence = confidence,
                    LastUsed = DateTime.UtcNow
                },
                // Update existing metrics
                (key, existing) =>
                {
                    existing.TotalAttempts++;
                    if (success) existing.SuccessfulAttempts++;
                    existing.OverallSuccessRate = (double)existing.SuccessfulAttempts / existing.TotalAttempts * 100.0;
                    
                    // Update running averages
                    existing.AverageDetectionTime = (existing.AverageDetectionTime + duration.TotalMilliseconds) / 2.0;
                    if (success && confidence > 0)
                    {
                        existing.AverageConfidence = (existing.AverageConfidence + confidence) / 2.0;
                    }
                    existing.LastUsed = DateTime.UtcNow;
                    return existing;
                });
        }

        private void UpdateSelectorStability(string url, Dictionary<string, SelectorDetails> selectorDetails, bool wasSuccessful)
        {
            var domain = ExtractDomain(url);
            
            _selectorStability.AddOrUpdate(domain,
                // Add new stability data
                new SelectorStabilityData
                {
                    Domain = domain,
                    StabilityHistory = new List<StabilityDataPoint>
                    {
                        new StabilityDataPoint
                        {
                            Timestamp = DateTime.UtcNow,
                            WasSuccessful = wasSuccessful,
                            SelectorDetails = selectorDetails
                        }
                    }
                },
                // Update existing data
                (key, existing) =>
                {
                    existing.StabilityHistory.Add(new StabilityDataPoint
                    {
                        Timestamp = DateTime.UtcNow,
                        WasSuccessful = wasSuccessful,
                        SelectorDetails = selectorDetails
                    });
                    
                    // Keep only recent data to prevent memory bloat
                    var cutoff = DateTime.UtcNow.AddDays(-StabilityAnalysisWindowDays * 2);
                    existing.StabilityHistory = existing.StabilityHistory
                        .Where(h => h.Timestamp >= cutoff)
                        .ToList();
                    
                    return existing;
                });
        }

        private string ExtractDomain(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.ToLowerInvariant();
            }
            catch
            {
                return url.ToLowerInvariant();
            }
        }

        private double CalculateConsistencyScore(List<StabilityDataPoint> dataPoints)
        {
            if (dataPoints.Count < 2) return 1.0;

            // Calculate variance in selector usage patterns
            // Lower variance = higher consistency = higher score
            var successfulPoints = dataPoints.Where(dp => dp.WasSuccessful).ToList();
            if (successfulPoints.Count < 2) return 0.5;

            // Simple consistency metric based on selector pattern similarity
            // In a more sophisticated implementation, this could analyze actual selector patterns
            var recentSuccessRate = successfulPoints.Count / (double)dataPoints.Count;
            var stability = Math.Min(1.0, recentSuccessRate * 1.2); // Boost factor for good performance
            
            return stability;
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// Represents different detection methods used in the system.
    /// </summary>
    public enum DetectionMethod
    {
        UrlSpecific,
        CommonAttributes,
        XPath,
        ShadowDOM
    }

    /// <summary>
    /// Status of a detection attempt.
    /// </summary>
    public enum DetectionStatus
    {
        InProgress,
        Success,
        Failed
    }

    /// <summary>
    /// Represents a single detection attempt with timing and result information.
    /// </summary>
    public class DetectionAttempt
    {
        public string Id { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DetectionMethod Method { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public DetectionStatus Status { get; set; }
        public int? Confidence { get; set; }
        public int ElementsFound { get; set; }
        public string? FailureReason { get; set; }
    }

    /// <summary>
    /// Metrics for a specific detection method.
    /// </summary>
    public class DetectionMethodMetrics
    {
        public DetectionMethod Method { get; set; }
        public int TotalAttempts { get; set; }
        public int SuccessfulAttempts { get; set; }
        public double OverallSuccessRate { get; set; }
        public double AverageDetectionTime { get; set; }
        public double AverageConfidence { get; set; }
        public DateTime LastUsed { get; set; }
    }

    /// <summary>
    /// Details about selectors used in detection for stability tracking.
    /// </summary>
    public class SelectorDetails
    {
        public string ElementType { get; set; } = string.Empty;
        public string Selector { get; set; } = string.Empty;
        public string AttributeUsed { get; set; } = string.Empty;
        public int Score { get; set; }
        public bool WasSuccessful { get; set; }
    }

    /// <summary>
    /// Stability data for selectors on a specific domain.
    /// </summary>
    public class SelectorStabilityData
    {
        public string Domain { get; set; } = string.Empty;
        public List<StabilityDataPoint> StabilityHistory { get; set; } = new List<StabilityDataPoint>();
    }

    /// <summary>
    /// A single data point in selector stability tracking.
    /// </summary>
    public class StabilityDataPoint
    {
        public DateTime Timestamp { get; set; }
        public bool WasSuccessful { get; set; }
        public Dictionary<string, SelectorDetails> SelectorDetails { get; set; } = new Dictionary<string, SelectorDetails>();
    }

    /// <summary>
    /// Comprehensive analytics about detection performance.
    /// </summary>
    public class DetectionAnalytics
    {
        public TimeSpan ReportPeriod { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int TotalAttempts { get; set; }
        public int SuccessfulAttempts { get; set; }
        public double OverallSuccessRate { get; set; }
        public double AverageDetectionTime { get; set; }
        public Dictionary<DetectionMethod, MethodPerformance> MethodPerformance { get; set; } = new Dictionary<DetectionMethod, MethodPerformance>();
    }

    /// <summary>
    /// Performance metrics for a specific detection method.
    /// </summary>
    public class MethodPerformance
    {
        public int TotalAttempts { get; set; }
        public int SuccessfulAttempts { get; set; }
        public double SuccessRate { get; set; }
        public double AverageConfidence { get; set; }
        public double AverageDetectionTime { get; set; }
    }

    /// <summary>
    /// Recommendation for which detection method to use.
    /// </summary>
    public class MethodRecommendation
    {
        public DetectionMethod Method { get; set; }
        public double Confidence { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    #endregion
} 