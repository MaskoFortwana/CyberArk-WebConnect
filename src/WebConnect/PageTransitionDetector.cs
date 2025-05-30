using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;

namespace WebConnect
{
    /// <summary>
    /// Detects page transitions after form submissions, particularly useful for login forms.
    /// Monitors DOM state changes, URL changes, title changes, and overall page stability.
    /// </summary>
    public class PageTransitionDetector
    {
        private readonly ILogger<PageTransitionDetector> _logger;
        private readonly IWebDriver _driver;
        private readonly string _initialUrl;
        private readonly string _initialTitle;
        private readonly string? _initialPageSource;
        private readonly int _initialPageSourceHash;

        /// <summary>
        /// Minimum time to wait before starting transition detection (milliseconds)
        /// </summary>
        public int InitialWaitMs { get; set; } = 500;

        /// <summary>
        /// Starting interval for polling (milliseconds)
        /// </summary>
        public int InitialPollingIntervalMs { get; set; } = 100;

        /// <summary>
        /// Maximum interval for polling (milliseconds)
        /// </summary>
        public int MaxPollingIntervalMs { get; set; } = 500;

        /// <summary>
        /// Factor by which to increase polling interval on each iteration
        /// </summary>
        public double PollingIntervalGrowthFactor { get; set; } = 1.5;

        /// <summary>
        /// Number of consecutive stable checks required to consider page stable
        /// </summary>
        public int StableCheckCount { get; set; } = 2;

        /// <summary>
        /// Threshold for page source change detection (percentage)
        /// </summary>
        public double PageSourceChangeThreshold { get; set; } = 0.1; // 10% change

        public PageTransitionDetector(IWebDriver driver, ILogger<PageTransitionDetector> logger)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Capture initial state
            try
            {
                _initialUrl = driver.Url;
                _initialTitle = driver.Title;
                _initialPageSource = driver.PageSource;
                _initialPageSourceHash = _initialPageSource?.GetHashCode() ?? 0;

                _logger.LogDebug("PageTransitionDetector initialized - URL: '{Url}', Title: '{Title}', SourceHash: {Hash}",
                    _initialUrl, _initialTitle, _initialPageSourceHash);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error capturing initial page state");
                _initialUrl = "";
                _initialTitle = "";
                _initialPageSource = null;
                _initialPageSourceHash = 0;
            }
        }

        /// <summary>
        /// Waits for a page transition to occur or timeout.
        /// Returns true if a transition was detected, false if timeout occurred.
        /// </summary>
        public async Task<bool> WaitForPageTransition(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var sessionId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogInformation("[{SessionId}] Starting page transition detection with {Timeout}s timeout",
                sessionId, timeout.TotalSeconds);

            try
            {
                // Initial wait to allow page to start transitioning
                if (InitialWaitMs > 0)
                {
                    _logger.LogDebug("[{SessionId}] Waiting {InitialWait}ms before starting detection",
                        sessionId, InitialWaitMs);
                    await Task.Delay(InitialWaitMs, cancellationToken);
                }

                var pollingInterval = InitialPollingIntervalMs;
                var stableCount = 0;
                var lastState = CapturePageState();
                var transitionDetected = false;

                while (DateTime.UtcNow - startTime < timeout && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(pollingInterval, cancellationToken);

                    var currentState = CapturePageState();
                    var changes = DetectChanges(lastState, currentState);

                    if (changes.HasSignificantChanges)
                    {
                        _logger.LogInformation("[{SessionId}] Page transition detected: {Changes}",
                            sessionId, changes.Description);
                        transitionDetected = true;
                        stableCount = 0; // Reset stability counter
                    }
                    else if (transitionDetected)
                    {
                        // We've seen a transition, now check for stability
                        stableCount++;
                        _logger.LogDebug("[{SessionId}] Page stable for {Count}/{Required} checks",
                            sessionId, stableCount, StableCheckCount);

                        if (stableCount >= StableCheckCount)
                        {
                            _logger.LogInformation("[{SessionId}] Page transition completed and stabilized after {Duration}ms",
                                sessionId, (DateTime.UtcNow - startTime).TotalMilliseconds);
                            return true;
                        }
                    }

                    // Increase polling interval progressively
                    pollingInterval = Math.Min(
                        (int)(pollingInterval * PollingIntervalGrowthFactor),
                        MaxPollingIntervalMs);

                    lastState = currentState;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("[{SessionId}] Page transition detection cancelled", sessionId);
                    return transitionDetected;
                }

                _logger.LogWarning("[{SessionId}] Page transition detection timed out after {Duration}ms. Transition detected: {Detected}",
                    sessionId, (DateTime.UtcNow - startTime).TotalMilliseconds, transitionDetected);
                return transitionDetected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{SessionId}] Error during page transition detection", sessionId);
                return false;
            }
        }

        /// <summary>
        /// Fast transition detection that bypasses WebDriver implicit wait for immediate checks.
        /// This method quickly checks for title and URL changes without being affected by the 
        /// 10-second implicit wait timeout, making login verification much faster.
        /// </summary>
        public async Task<bool> WaitForPageTransitionFast(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var sessionId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogInformation("[{SessionId}] Starting FAST page transition detection with {Timeout}s timeout",
                sessionId, timeout.TotalSeconds);

            try
            {
                // Store original implicit wait setting
                var originalImplicitWait = _driver.Manage().Timeouts().ImplicitWait;
                
                try
                {
                    // Set very short implicit wait for fast detection
                    _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(100);

                    // Skip initial wait for fast detection - start checking immediately
                    _logger.LogDebug("[{SessionId}] Starting immediate fast detection checks", sessionId);

                    var pollingInterval = 100; // Check every 100ms for responsiveness
                    var transitionDetected = false;

                    while (DateTime.UtcNow - startTime < timeout && !cancellationToken.IsCancellationRequested)
                    {
                        var currentState = CaptureFastPageState();
                        var changes = DetectFastChanges(currentState);

                        if (changes.HasSignificantChanges)
                        {
                            _logger.LogInformation("[{SessionId}] FAST transition detected in {Duration}ms: {Changes}",
                                sessionId, (DateTime.UtcNow - startTime).TotalMilliseconds, changes.Description);
                            transitionDetected = true;
                            
                            // For fast detection, return immediately on first significant change
                            return true;
                        }

                        // Short polling interval for responsiveness
                        await Task.Delay(pollingInterval, cancellationToken);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("[{SessionId}] Fast page transition detection cancelled", sessionId);
                        return transitionDetected;
                    }

                    _logger.LogDebug("[{SessionId}] Fast page transition detection completed after {Duration}ms. Transition detected: {Detected}",
                        sessionId, (DateTime.UtcNow - startTime).TotalMilliseconds, transitionDetected);
                    return transitionDetected;
                }
                finally
                {
                    // Restore original implicit wait setting
                    _driver.Manage().Timeouts().ImplicitWait = originalImplicitWait;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{SessionId}] Error during fast page transition detection", sessionId);
                return false;
            }
        }

        /// <summary>
        /// Captures the current state of the page optimized for fast detection
        /// </summary>
        private FastPageState CaptureFastPageState()
        {
            try
            {
                var state = new FastPageState
                {
                    Url = _driver.Url,
                    Title = _driver.Title,
                    Timestamp = DateTime.UtcNow
                };

                return state;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error capturing fast page state");
                return new FastPageState
                {
                    Url = "",
                    Title = "",
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Detects changes for fast transition detection by comparing against initial state
        /// </summary>
        private FastPageChangeInfo DetectFastChanges(FastPageState current)
        {
            var changes = new FastPageChangeInfo();
            var changeDescriptions = new List<string>();

            // Check URL change
            if (!string.Equals(_initialUrl, current.Url, StringComparison.OrdinalIgnoreCase))
            {
                changes.UrlChanged = true;
                changeDescriptions.Add($"URL: '{_initialUrl}' → '{current.Url}'");
            }

            // Check title change
            if (!string.Equals(_initialTitle, current.Title, StringComparison.Ordinal))
            {
                changes.TitleChanged = true;
                changeDescriptions.Add($"Title: '{_initialTitle}' → '{current.Title}'");
            }

            // For fast detection, either URL or title change indicates significant transition
            changes.HasSignificantChanges = changes.UrlChanged || changes.TitleChanged;
            changes.Description = string.Join(", ", changeDescriptions);

            return changes;
        }

        /// <summary>
        /// Captures the current state of the page
        /// </summary>
        private PageState CapturePageState()
        {
            // Store original implicit wait setting to restore later
            var originalImplicitWait = _driver.Manage().Timeouts().ImplicitWait;
            
            try
            {
                // Set very short implicit wait for standard detection to avoid 10-second delays
                _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);

                var state = new PageState
                {
                    Url = _driver.Url,
                    Title = _driver.Title,
                    ReadyState = GetDocumentReadyState(),
                    PageSourceHash = _driver.PageSource?.GetHashCode() ?? 0,
                    Timestamp = DateTime.UtcNow
                };

                // Check for loading indicators
                try
                {
                    var loadingElements = _driver.FindElements(By.CssSelector(
                        ".loading:not([style*='display: none']), " +
                        ".spinner:not([style*='display: none']), " +
                        "*[class*='loading']:not([style*='display: none']), " +
                        "*[class*='spinner']:not([style*='display: none'])"));

                    state.HasVisibleLoadingIndicators = loadingElements.Any(e =>
                    {
                        try { return e.Displayed; } catch { return false; }
                    });
                }
                catch
                {
                    state.HasVisibleLoadingIndicators = false;
                }

                return state;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error capturing page state");
                return new PageState
                {
                    Url = "",
                    Title = "",
                    ReadyState = "unknown",
                    PageSourceHash = 0,
                    Timestamp = DateTime.UtcNow
                };
            }
            finally
            {
                // Restore original implicit wait setting
                _driver.Manage().Timeouts().ImplicitWait = originalImplicitWait;
            }
        }

        /// <summary>
        /// Gets the document ready state using JavaScript
        /// </summary>
        private string GetDocumentReadyState()
        {
            try
            {
                if (_driver is IJavaScriptExecutor jsExecutor)
                {
                    var readyState = jsExecutor.ExecuteScript("return document.readyState") as string;
                    return readyState ?? "unknown";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting document ready state");
            }
            return "unknown";
        }

        /// <summary>
        /// Detects changes between two page states
        /// </summary>
        private PageChangeInfo DetectChanges(PageState previous, PageState current)
        {
            var changes = new PageChangeInfo();
            var changeDescriptions = new List<string>();

            // Check URL change
            if (!string.Equals(previous.Url, current.Url, StringComparison.OrdinalIgnoreCase))
            {
                changes.UrlChanged = true;
                changeDescriptions.Add($"URL: '{previous.Url}' -> '{current.Url}'");
            }

            // Check title change
            if (!string.Equals(previous.Title, current.Title, StringComparison.Ordinal))
            {
                changes.TitleChanged = true;
                changeDescriptions.Add($"Title: '{previous.Title}' -> '{current.Title}'");
            }

            // Check ready state change
            if (!string.Equals(previous.ReadyState, current.ReadyState, StringComparison.OrdinalIgnoreCase))
            {
                changes.ReadyStateChanged = true;
                changeDescriptions.Add($"ReadyState: '{previous.ReadyState}' -> '{current.ReadyState}'");
            }

            // Check page source change (significant change only)
            if (previous.PageSourceHash != current.PageSourceHash)
            {
                changes.PageSourceChanged = true;
                changeDescriptions.Add("Page source changed significantly");
            }

            // Check loading indicators
            if (previous.HasVisibleLoadingIndicators != current.HasVisibleLoadingIndicators)
            {
                changes.LoadingStateChanged = true;
                changeDescriptions.Add($"Loading indicators: {previous.HasVisibleLoadingIndicators} -> {current.HasVisibleLoadingIndicators}");
            }

            // Determine if changes are significant
            changes.HasSignificantChanges = changes.UrlChanged || changes.TitleChanged || 
                                          (changes.PageSourceChanged && !current.HasVisibleLoadingIndicators);
            
            changes.Description = string.Join(", ", changeDescriptions);

            return changes;
        }

        /// <summary>
        /// Represents the state of a page at a point in time
        /// </summary>
        private class PageState
        {
            public string Url { get; set; } = "";
            public string Title { get; set; } = "";
            public string ReadyState { get; set; } = "";
            public int PageSourceHash { get; set; }
            public bool HasVisibleLoadingIndicators { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Information about changes detected between page states
        /// </summary>
        private class PageChangeInfo
        {
            public bool UrlChanged { get; set; }
            public bool TitleChanged { get; set; }
            public bool ReadyStateChanged { get; set; }
            public bool PageSourceChanged { get; set; }
            public bool LoadingStateChanged { get; set; }
            public bool HasSignificantChanges { get; set; }
            public string Description { get; set; } = "";
        }

        /// <summary>
        /// Represents the state of a page for fast detection
        /// </summary>
        private class FastPageState
        {
            public string Url { get; set; } = "";
            public string Title { get; set; } = "";
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Information about changes detected for fast transition detection
        /// </summary>
        private class FastPageChangeInfo
        {
            public bool UrlChanged { get; set; }
            public bool TitleChanged { get; set; }
            public bool HasSignificantChanges { get; set; }
            public string Description { get; set; } = "";
        }
    }
} 
