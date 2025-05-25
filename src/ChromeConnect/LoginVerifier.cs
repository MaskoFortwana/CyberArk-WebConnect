using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Services;
using ChromeConnect.Configuration;
using System.Threading;
using System.Linq;
using Polly;

namespace ChromeConnect.Core;

public class LoginVerifier : IScreenshotCapture
{
    private readonly ILogger<LoginVerifier> _logger;
    private readonly LoginVerificationConfig _config;
    private readonly TimeoutConfig _timeoutConfig;
    private readonly PolicyFactory _policyFactory;
    private string _initialLoginUrl = string.Empty; // NEW: Store initial URL for comparison

    public LoginVerifier(ILogger<LoginVerifier> logger, LoginVerificationConfig config, TimeoutConfig timeoutConfig, PolicyFactory policyFactory)
    {
        _logger = logger;
        _config = config;
        _timeoutConfig = timeoutConfig;
        _policyFactory = policyFactory;
    }

    /// <summary>
    /// Fast URL-based success detection for immediate redirects (optimized for your use case)
    /// </summary>
    public virtual async Task<bool> WaitForSuccessUrlAsync(IWebDriver driver, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation("=== FAST URL SUCCESS DETECTION {SessionId} STARTED ===", sessionId);
        _logger.LogInformation("[{SessionId}] Waiting for URL change with {Timeout}s timeout. Initial URL: {InitialUrl}", 
                              sessionId, _timeoutConfig.InternalTimeout.TotalSeconds, driver.Url);

        _initialLoginUrl = driver.Url;
        
        try
        {
            // Use URL polling policy for fast detection
            var urlPolicy = _policyFactory.CreateUrlPollingPolicy();
            
            var result = await urlPolicy.ExecuteAsync(async () =>
            {
                var endTime = DateTime.Now.Add(_timeoutConfig.InternalTimeout);
                
                // Minimal initial delay for immediate redirects
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                
                while (DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    string currentUrl = driver.Url;
                    
                    // Check for URL change
                    if (!currentUrl.Equals(_initialLoginUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        var duration = DateTime.Now - startTime;
                        _logger.LogInformation("[{SessionId}] ✅ URL CHANGE DETECTED in {Duration}ms! From: {InitialUrl} To: {CurrentUrl}", 
                                              sessionId, duration.TotalMilliseconds, _initialLoginUrl, currentUrl);
                        
                        // Check for success indicators in URL
                        var successIndicators = new[] { "success", "welcome", "dashboard", "home", "main", "portal", "app", "logged", "authenticated" };
                        var hasSuccessIndicator = successIndicators.Any(indicator => 
                            currentUrl.Contains(indicator, StringComparison.OrdinalIgnoreCase));
                        
                        if (hasSuccessIndicator)
                        {
                            _logger.LogInformation("[{SessionId}] ✅ SUCCESS INDICATOR found in URL: {CurrentUrl}", sessionId, currentUrl);
                            return true;
                        }
                        
                        // Check for path change (different from login page)
                        try
                        {
                            var initialUri = new Uri(_initialLoginUrl);
                            var currentUri = new Uri(currentUrl);
                            
                            if (!initialUri.AbsolutePath.Equals(currentUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("[{SessionId}] ✅ PATH CHANGE detected: '{InitialPath}' → '{CurrentPath}'", 
                                                      sessionId, initialUri.AbsolutePath, currentUri.AbsolutePath);
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[{SessionId}] Error parsing URLs, assuming success due to URL change", sessionId);
                            return true;
                        }
                        
                        // Any URL change is likely success unless it contains error indicators
                        var errorIndicators = new[] { "error", "invalid", "incorrect", "failed", "denied", "wrong" };
                        var hasErrorIndicator = errorIndicators.Any(indicator => 
                            currentUrl.Contains(indicator, StringComparison.OrdinalIgnoreCase));
                        
                        if (!hasErrorIndicator)
                        {
                            _logger.LogInformation("[{SessionId}] ✅ URL CHANGED without error indicators, assuming success: {CurrentUrl}", 
                                                  sessionId, currentUrl);
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("[{SessionId}] ❌ URL changed but contains error indicators: {CurrentUrl}", 
                                              sessionId, currentUrl);
                            return false;
                        }
                    }
                    
                    // Use fast polling for immediate redirects
                    await Task.Delay(_timeoutConfig.PollingInterval, cancellationToken);
                }
                
                var timeoutDuration = DateTime.Now - startTime;
                _logger.LogWarning("[{SessionId}] ⏰ TIMEOUT after {Duration}ms - no URL change detected", 
                                  sessionId, timeoutDuration.TotalMilliseconds);
                return false;
            });
            
            var totalDuration = DateTime.Now - startTime;
            var resultSymbol = result ? "✅" : "❌";
            _logger.LogInformation("=== FAST URL SUCCESS DETECTION {SessionId} ENDED: {Result} in {Duration}ms ===", 
                                  sessionId, result ? "SUCCESS" : "TIMEOUT", totalDuration.TotalMilliseconds);
            
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogWarning("[{SessionId}] ⏰ CANCELLED after {Duration}ms", sessionId, duration.TotalMilliseconds);
            _logger.LogInformation("=== FAST URL SUCCESS DETECTION {SessionId} ENDED: CANCELLED ===", sessionId);
            return false;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "[{SessionId}] 💥 ERROR after {Duration}ms: {ErrorMessage}", 
                           sessionId, duration.TotalMilliseconds, ex.Message);
            _logger.LogInformation("=== FAST URL SUCCESS DETECTION {SessionId} ENDED: ERROR ===", sessionId);
            return false;
        }
    }

    public virtual async Task<bool> VerifyLoginSuccessAsync(IWebDriver driver, CancellationToken externalToken = default)
    {
        var startTime = DateTime.Now;
        var sessionId = Guid.NewGuid().ToString("N")[..8]; // Short session ID for tracking
        
        _logger.LogInformation("=== LOGIN VERIFICATION SESSION {SessionId} STARTED ===", sessionId);
        _logger.LogInformation("[{SessionId}] Starting login verification with {InternalTimeout}s internal timeout " +
                              "and {ExternalTimeout}s external timeout. Initial page URL: {InitialUrl}", 
                              sessionId, _timeoutConfig.InternalTimeout.TotalSeconds, 
                              _timeoutConfig.ExternalTimeout.TotalSeconds, driver.Url);

        // NEW: Store initial URL for comparison and enhanced session tracking
        _initialLoginUrl = driver.Url;
        _logger.LogInformation("[{SessionId}] Session configuration - InternalTimeout: {InternalTimeout}s, " +
                              "ExternalTimeout: {ExternalTimeout}s, InitialDelay: {InitialDelay}ms, " +
                              "TimingLogs: {TimingLogs}, Screenshots: {Screenshots}", 
                              sessionId, _timeoutConfig.InternalTimeout.TotalSeconds, 
                              _timeoutConfig.ExternalTimeout.TotalSeconds, _timeoutConfig.InitialDelay.TotalMilliseconds,
                              _config.EnableTimingLogs, _config.CaptureScreenshotsOnFailure);

        // ENHANCED: Capture initial page state for diagnostics
        try
        {
            var initialPageInfo = new
            {
                Title = driver.Title,
                Url = driver.Url,
                HasPasswordFields = driver.FindElements(By.CssSelector("input[type='password']")).Count,
                HasForms = driver.FindElements(By.TagName("form")).Count,
                PageSource = driver.PageSource?.Length ?? 0
            };
            
            _logger.LogDebug("[{SessionId}] Initial page state - Title: '{Title}', " +
                           "Password fields: {PasswordFields}, Forms: {Forms}, PageSource length: {PageSourceLength} chars",
                           sessionId, initialPageInfo.Title, initialPageInfo.HasPasswordFields, 
                           initialPageInfo.HasForms, initialPageInfo.PageSource);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[{SessionId}] Could not capture initial page state: {Error}", sessionId, ex.Message);
        }

        // Create a CancellationTokenSource for the internal verification timeout
        using var internalTimeoutCts = new CancellationTokenSource(_timeoutConfig.InternalTimeout);
        
        // Link the internal CTS with the external token from TimeoutManager
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(internalTimeoutCts.Token, externalToken);
        var combinedToken = combinedCts.Token; // Use this token for all operations

        try
        {
            _logger.LogInformation("[{SessionId}] Starting login verification process with {InternalTimeout}s internal timeout " +
                                  "and {ExternalTimeout}s external timeout", sessionId, 
                                  _timeoutConfig.InternalTimeout.TotalSeconds, _timeoutConfig.ExternalTimeout.TotalSeconds);
            
            // Create the combined policy for resilient verification
            var combinedPolicy = _policyFactory.CreateCombinedPolicy();
            
            // Execute verification with Polly policy
            var result = await combinedPolicy.ExecuteAsync(async () =>
            {
                // Pass the combinedToken to QuickErrorDetectionAsync
                var quickCheckStart = DateTime.Now;
                if (await QuickErrorDetectionAsync(driver, combinedToken, sessionId))
                {
                    var quickCheckDuration = DateTime.Now - quickCheckStart;
                    _logger.LogError("[{SessionId}] ❌ LOGIN FAILED - immediate error detected in {Duration}ms during quick check", 
                                   sessionId, quickCheckDuration.TotalMilliseconds);
                    CaptureScreenshot(driver, $"LoginFailed_QuickDetection_{sessionId}");
                    _logger.LogInformation("=== LOGIN VERIFICATION SESSION {SessionId} ENDED: QUICK FAILURE ===", sessionId);
                    return false;
                }
                var quickCheckDuration2 = DateTime.Now - quickCheckStart;
                _logger.LogDebug("[{SessionId}] ✅ Quick error detection passed in {Duration}ms", 
                               sessionId, quickCheckDuration2.TotalMilliseconds);

                combinedToken.ThrowIfCancellationRequested(); // Check if timeout already exceeded

                // Pass the combinedToken to ProgressiveVerificationAsync
                var progressiveStart = DateTime.Now;
                _logger.LogInformation("[{SessionId}] Starting progressive verification phase", sessionId);
                var progressiveResult = await ProgressiveVerificationAsync(driver, startTime, combinedToken, sessionId);
                var progressiveDuration = DateTime.Now - progressiveStart;
                
                var totalDuration = DateTime.Now - startTime;
                var resultSymbol = progressiveResult ? "✅" : "❌";
                _logger.LogInformation("[{SessionId}] {Symbol} LOGIN VERIFICATION COMPLETED in {TotalDuration}ms " +
                                      "(quick: {QuickDuration}ms, progressive: {ProgressiveDuration}ms) " +
                                      "with result: {Result}", 
                                      sessionId, resultSymbol, totalDuration.TotalMilliseconds, 
                                      quickCheckDuration2.TotalMilliseconds, progressiveDuration.TotalMilliseconds,
                                      progressiveResult ? "SUCCESS" : "FAILURE");
                
                return progressiveResult;
            });
            
            _logger.LogInformation("=== LOGIN VERIFICATION SESSION {SessionId} ENDED: {Result} ===", 
                                  sessionId, result ? "SUCCESS" : "FAILURE");
                
            return result;
        }
        catch (OperationCanceledException) when (combinedToken.IsCancellationRequested) // Catch cancellation from either source
        {
            var duration = DateTime.Now - startTime;
            string reason = externalToken.IsCancellationRequested ? "external timeout" : "internal 10s timeout";
            _logger.LogWarning("[{SessionId}] ⏰ LOGIN VERIFICATION TIMED OUT by {Reason} after {Duration}ms " +
                              "(internal: {InternalTimeout}s, external: {ExternalTimeout}s). Final URL: {FinalUrl}", 
                              sessionId, reason, duration.TotalMilliseconds, 
                              _timeoutConfig.InternalTimeout.TotalSeconds, _timeoutConfig.ExternalTimeout.TotalSeconds, driver.Url);
            CaptureScreenshot(driver, $"LoginVerification_Timeout_{sessionId}");
            _logger.LogInformation("=== LOGIN VERIFICATION SESSION {SessionId} ENDED: TIMEOUT ===", sessionId);
            return false;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "[{SessionId}] 💥 ERROR during login verification after {Duration}ms. " +
                           "Final URL: {FinalUrl}, Error: {ErrorMessage}", 
                           sessionId, duration.TotalMilliseconds, driver.Url, ex.Message);
            CaptureScreenshot(driver, $"VerificationError_{sessionId}");
            _logger.LogInformation("=== LOGIN VERIFICATION SESSION {SessionId} ENDED: ERROR ===", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Quick error detection with under 500ms timeout for immediate failures
    /// </summary>
    private async Task<bool> QuickErrorDetectionAsync(IWebDriver driver, CancellationToken combinedToken, string sessionId)
    {
        try
        {
            // Quick check for immediate error indicators using configured timeout
            // Link the external token with a short internal timeout for this specific check.
            using var quickCheckCts = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
            quickCheckCts.CancelAfter(_timeoutConfig.QuickErrorTimeout); // Use configured timeout
            
            var quickWait = new WebDriverWait(driver, _timeoutConfig.QuickErrorTimeout); // WebDriverWait internal timeout
            // WebDriverWait doesn't directly take a CancellationToken in all its .Until forms easily without custom setup.
            // Relying on quickCheckCts.Token.IsCancellationRequested within loops or after potentially blocking calls.
            // Or, better, make WebDriverWait itself aware of the token if possible (see ProgressiveVerificationAsync methods).
            // For now, the explicit CancelAfter(100) on a linked token and quickWait internal timeout is the main guard.

            var errorSelectors = new[]
            {
                "div.error:not(:empty)",
                "span.error:not(:empty)", 
                "p.error:not(:empty)",
                "div.alert-danger:not(:empty)",
                "div.alert-error:not(:empty)",
                "[role='alert']:not(:empty)"
            };

            foreach (var selector in errorSelectors)
            {
                combinedToken.ThrowIfCancellationRequested(); // Use combinedToken
                try
                {
                    // WebDriverWait itself doesn't directly accept a CancellationToken in older Selenium versions easily for all .Until calls.
                    // The main protection here is that quickWait has its own short timeout.
                    var errorElement = quickWait.Until(d => d.FindElement(By.CssSelector(selector)));
                    if (errorElement?.Displayed == true && !string.IsNullOrWhiteSpace(errorElement.Text))
                    {
                        _logger.LogDebug("[{SessionId}] Quick error detected: {Selector} - {Text}", 
                                       sessionId, selector, errorElement.Text);
                        return true;
                    }
                }
                catch { /* Continue to next selector */ }
            }

            // Check page source for immediate error text patterns (only first 5000 chars for speed)
            combinedToken.ThrowIfCancellationRequested(); // Use combinedToken
            var pageSource = driver.PageSource;
            if (pageSource.Length > 5000) pageSource = pageSource.Substring(0, 5000);
            
            var errorPatterns = new[] { "invalid credentials", "login failed", "incorrect password", "access denied" };
            foreach (var pattern in errorPatterns)
            {
                combinedToken.ThrowIfCancellationRequested(); // Use combinedToken
                if (pageSource.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("[{SessionId}] Quick error pattern detected: {Pattern}", 
                                   sessionId, pattern);
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If quick detection fails, continue with normal verification
            return false;
        }
    }

    /// <summary>
    /// Progressive verification with escalating timeouts for thorough checking
    /// </summary>
    private async Task<bool> ProgressiveVerificationAsync(IWebDriver driver, DateTime startTime, CancellationToken combinedToken, string sessionId)
    {
        var maxDuration = _timeoutConfig.InternalTimeout;
        
        await Task.Delay(_timeoutConfig.InitialDelay, combinedToken); 
        
        // Check if we've already exceeded the overall timeout before starting verification
        if (DateTime.Now - startTime > maxDuration || combinedToken.IsCancellationRequested) 
        {
            _logger.LogDebug("[{SessionId}] Overall timeout exceeded before verification steps could complete", sessionId);
            return false;
        }
        
        // IMPROVED: Use configurable time allocation per method to ensure all methods get a chance to run
        var remainingTime = maxDuration - (DateTime.Now - startTime);
        var timePerMethod = Math.Max(_timeoutConfig.MinTimePerMethod.TotalSeconds, 
                                   Math.Min(_timeoutConfig.MaxTimePerMethod.TotalSeconds, 
                                          remainingTime.TotalSeconds / 4.0)); // Configurable min-max seconds per method
        
        _logger.LogDebug("[{SessionId}] Allocating {TimePerMethod}s per verification method, {RemainingTime}s total remaining", 
                        sessionId, timePerMethod, remainingTime.TotalSeconds);
        
        // Initialize verification results with confidence scores
        var results = new VerificationResults();
        
        // Method 1: URL Change Detection - Most reliable for many applications
        try
        {
            using var methodTimeout = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
            methodTimeout.CancelAfter(TimeSpan.FromSeconds(timePerMethod));
            
            results.UrlChanged = await CheckUrlChangedAsync(driver, (int)timePerMethod, methodTimeout.Token);
            results.UrlChangedConfidence = results.UrlChanged ? 0.9 : 0.1;
            _logger.LogDebug("[{SessionId}] URL changed check result: {Result} (confidence: {Confidence})", 
                           sessionId, results.UrlChanged, results.UrlChangedConfidence);
        }
        catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
        {
            _logger.LogDebug("[{SessionId}] URL change check cancelled by timeout", sessionId);
            results.UrlChangedConfidence = 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{SessionId}] Error in URL change check", sessionId);
            results.UrlChangedConfidence = 0.0;
        }

        // Early success check - if URL changed definitively, we can be confident
        if (results.UrlChanged && results.UrlChangedConfidence >= 0.8)
        {
            _logger.LogInformation("[{SessionId}] ✅ HIGH-CONFIDENCE URL CHANGE DETECTED - login appears successful", sessionId);
            CaptureScreenshot(driver, $"Success_UrlChanged_HighConfidence_{sessionId}");
            return true;
        }

        // Method 2: Login Form Disappearance - Good indicator for traditional forms
        try
        {
            using var methodTimeout = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
            methodTimeout.CancelAfter(TimeSpan.FromSeconds(timePerMethod));
            
            results.FormGone = await CheckLoginFormGoneAsync(driver, methodTimeout.Token);
            results.FormGoneConfidence = results.FormGone ? 0.8 : 0.2;
            _logger.LogDebug("[{SessionId}] Login form gone check result: {Result} (confidence: {Confidence})", 
                           sessionId, results.FormGone, results.FormGoneConfidence);
        }
        catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
        {
            _logger.LogDebug("[{SessionId}] Form gone check cancelled by timeout", sessionId);
            results.FormGoneConfidence = 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{SessionId}] Error in form gone check", sessionId);
            results.FormGoneConfidence = 0.0;
        }

        // Method 3: Success Elements Detection - Good for identifying post-login UI
        try
        {
            using var methodTimeout = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
            methodTimeout.CancelAfter(TimeSpan.FromSeconds(timePerMethod));
            
            results.SuccessElements = await CheckForSuccessElementsAsync(driver, (int)timePerMethod, methodTimeout.Token, sessionId);
            results.SuccessElementsConfidence = results.SuccessElements ? 0.85 : 0.15;
            _logger.LogDebug("[{SessionId}] Success elements check result: {Result} (confidence: {Confidence})", 
                           sessionId, results.SuccessElements, results.SuccessElementsConfidence);
        }
        catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
        {
            _logger.LogDebug("[{SessionId}] Success elements check cancelled by timeout", sessionId);
            results.SuccessElementsConfidence = 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{SessionId}] Error in success elements check", sessionId);
            results.SuccessElementsConfidence = 0.0;
        }

        // Method 4: Error Messages Detection - Critical for negative confirmation
        try
        {
            using var methodTimeout = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
            methodTimeout.CancelAfter(TimeSpan.FromSeconds(timePerMethod));
            
            results.ErrorMessages = await CheckForErrorMessagesAsync(driver, (int)timePerMethod, methodTimeout.Token, sessionId);
            results.ErrorMessagesConfidence = results.ErrorMessages ? 0.95 : 0.1;
            _logger.LogDebug("[{SessionId}] Error messages check result: {Result} (confidence: {Confidence})", 
                           sessionId, results.ErrorMessages, results.ErrorMessagesConfidence);
        }
        catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
        {
            _logger.LogDebug("[{SessionId}] Error messages check cancelled by timeout", sessionId);
            results.ErrorMessagesConfidence = 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{SessionId}] Error in error messages check", sessionId);
            results.ErrorMessagesConfidence = 0.0;
        }

        // ENHANCED: Comprehensive success assessment using confidence scoring
        var finalDuration = DateTime.Now - startTime;
        _logger.LogInformation("[{SessionId}] 📊 VERIFICATION METHODS COMPLETED in {Duration}ms. Results summary:", 
                              sessionId, finalDuration.TotalMilliseconds);
        _logger.LogInformation("[{SessionId}]   • URL changed: {UrlChanged} (confidence: {UrlConfidence:F2})", 
                              sessionId, results.UrlChanged, results.UrlChangedConfidence);
        _logger.LogInformation("[{SessionId}]   • Form gone: {FormGone} (confidence: {FormConfidence:F2})", 
                              sessionId, results.FormGone, results.FormGoneConfidence);
        _logger.LogInformation("[{SessionId}]   • Success elements: {SuccessElements} (confidence: {SuccessConfidence:F2})", 
                              sessionId, results.SuccessElements, results.SuccessElementsConfidence);
        _logger.LogInformation("[{SessionId}]   • Error messages: {ErrorMessages} (confidence: {ErrorConfidence:F2})", 
                              sessionId, results.ErrorMessages, results.ErrorMessagesConfidence);

        // Strong negative confirmation - if we found explicit error messages with high confidence
        if (results.ErrorMessages && results.ErrorMessagesConfidence >= 0.8)
        {
            _logger.LogError("[{SessionId}] ❌ HIGH-CONFIDENCE ERROR MESSAGES DETECTED - confirming login failure", sessionId);
            CaptureScreenshot(driver, $"LoginFailed_HighConfidenceErrors_{sessionId}");
            return false;
        }

        // Strong positive confirmation - any high-confidence success indicator
        var positiveIndicators = new[]
        {
            (results.UrlChanged, results.UrlChangedConfidence, "URL changed"),
            (results.FormGone, results.FormGoneConfidence, "Form disappeared"),
            (results.SuccessElements, results.SuccessElementsConfidence, "Success elements found")
        };

        foreach (var (indicator, confidence, description) in positiveIndicators)
        {
            if (indicator && confidence >= 0.8)
            {
                _logger.LogInformation("[{SessionId}] ✅ HIGH-CONFIDENCE SUCCESS INDICATOR: {Description} " +
                                      "(confidence: {Confidence:F2}) - confirming login success", 
                                      sessionId, description, confidence);
                CaptureScreenshot(driver, $"Success_HighConfidenceIndicator_{sessionId}");
                return true;
            }
        }

        // IMPROVED: Weighted scoring system for ambiguous cases
        var successScore = 0.0;
        var totalWeight = 0.0;

        // Weight the confidence scores
        var weights = new[] { 0.3, 0.25, 0.25, -0.4 }; // URL, Form, Elements, -Errors
        var confidenceScores = new[] 
        { 
            results.UrlChangedConfidence, 
            results.FormGoneConfidence, 
            results.SuccessElementsConfidence, 
            -results.ErrorMessagesConfidence // Negative weight for errors
        };

        for (int i = 0; i < weights.Length; i++)
        {
            successScore += weights[i] * confidenceScores[i];
            totalWeight += Math.Abs(weights[i]);
        }

        var normalizedScore = successScore / totalWeight;
        
        _logger.LogInformation("[{SessionId}] 🧮 WEIGHTED SCORE CALCULATION:", sessionId);
        _logger.LogInformation("[{SessionId}]   Formula: URL(30%) + Form(25%) + Elements(25%) - Errors(40%)", sessionId);
        _logger.LogInformation("[{SessionId}]   Breakdown: URL({UrlScore:F3}) + Form({FormScore:F3}) + " +
                              "Elements({ElementsScore:F3}) - Errors({ErrorScore:F3}) = {TotalScore:F3}", 
                              sessionId,
                              weights[0] * confidenceScores[0],
                              weights[1] * confidenceScores[1], 
                              weights[2] * confidenceScores[2],
                              Math.Abs(weights[3] * confidenceScores[3]),
                              normalizedScore);

        // IMPROVED: Lower threshold for success assumption (was implicit binary logic)
        if (normalizedScore >= 0.4)
        {
            _logger.LogInformation("[{SessionId}] ✅ SUCCESS SCORE {Score:F3} ABOVE THRESHOLD (0.4) - assuming login success", 
                                  sessionId, normalizedScore);
            CaptureScreenshot(driver, $"Success_ScoreBasedDecision_{sessionId}");
            return true;
        }
        else if (normalizedScore <= -0.3)
        {
            _logger.LogError("[{SessionId}] ❌ FAILURE SCORE {Score:F3} BELOW THRESHOLD (-0.3) - confirming login failure", 
                           sessionId, normalizedScore);
            CaptureScreenshot(driver, $"LoginFailed_ScoreBasedDecision_{sessionId}");
            return false;
        }
        else
        {
            // ENHANCED: Final ambiguous case handling - assume success if no explicit errors
            _logger.LogInformation("[{SessionId}] ⚖️ AMBIGUOUS SCORE ({Score:F3}) - applying final decision logic", 
                                  sessionId, normalizedScore);
            
            if (!results.ErrorMessages || results.ErrorMessagesConfidence < 0.5)
            {
                _logger.LogInformation("[{SessionId}] ✅ AMBIGUOUS RESULT WITH NO CLEAR ERRORS - assuming success " +
                                      "(score: {Score:F3}, error confidence: {ErrorConfidence:F2})",
                                      sessionId, normalizedScore, results.ErrorMessagesConfidence);
                CaptureScreenshot(driver, $"AssumeSuccess_AmbiguousNoErrors_{sessionId}");
                return true;
            }
            else
            {
                _logger.LogWarning("[{SessionId}] ❌ AMBIGUOUS RESULT WITH POTENTIAL ERRORS - failing safely " +
                                  "(score: {Score:F3}, error confidence: {ErrorConfidence:F2})",
                                  sessionId, normalizedScore, results.ErrorMessagesConfidence);
                CaptureScreenshot(driver, $"LoginFailed_AmbiguousWithErrors_{sessionId}");
                return false;
            }
        }
    }

    /// <summary>
    /// Holds verification results with confidence scores
    /// </summary>
    private class VerificationResults
    {
        public bool UrlChanged { get; set; }
        public double UrlChangedConfidence { get; set; }
        
        public bool FormGone { get; set; }
        public double FormGoneConfidence { get; set; }
        
        public bool SuccessElements { get; set; }
        public double SuccessElementsConfidence { get; set; }
        
        public bool ErrorMessages { get; set; }
        public double ErrorMessagesConfidence { get; set; }
    }

    private async Task<bool> CheckUrlChangedAsync(IWebDriver driver, int timeoutSeconds, CancellationToken combinedToken)
    {
        try
        {
            string currentUrl = driver.Url;
            _logger.LogDebug("Checking URL change - Initial: {InitialUrl}, Current: {CurrentUrl}", _initialLoginUrl, currentUrl);

            // **FIX 1: Direct URL comparison - if URL changed, it's likely success**
            if (!string.IsNullOrEmpty(_initialLoginUrl) && !currentUrl.Equals(_initialLoginUrl, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("URL changed detected! From: {InitialUrl} To: {CurrentUrl}", _initialLoginUrl, currentUrl);
                
                // **FIX 2: Check for positive success indicators in the new URL**
                var successIndicators = new[] { "success", "welcome", "dashboard", "home", "main", "portal", "app", "logged", "authenticated" };
                var positiveSuccessFound = successIndicators.Any(indicator => 
                    currentUrl.Contains(indicator, StringComparison.OrdinalIgnoreCase));
                
                if (positiveSuccessFound)
                {
                    _logger.LogInformation("URL change contains success indicators - confirming login success");
                    return true;
                }

                // **FIX 3: Path-based comparison - if we moved to a different path, likely success**
                try
                {
                    var initialUri = new Uri(_initialLoginUrl);
                    var currentUri = new Uri(currentUrl);
                    
                    // Different paths indicate successful navigation
                    if (!initialUri.AbsolutePath.Equals(currentUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("URL path changed from '{InitialPath}' to '{CurrentPath}' - indicating navigation success", 
                                             initialUri.AbsolutePath, currentUri.AbsolutePath);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error parsing URLs for path comparison, falling back to full URL comparison");
                    // URL changed but couldn't parse - still likely success since URL is different
                    return true;
                }
            }

            // **FIX 4: Enhanced login page detection - check for EXPLICIT login failure indicators**
            // Only return false if we're definitely still on a login page WITH errors
            var loginIndicators = new[] { "login", "signin", "auth", "logon", "sign-in", "log-in" };
            var errorIndicators = new[] { "error", "invalid", "incorrect", "failed", "denied", "wrong" };
            
            bool containsLoginKeyword = loginIndicators.Any(indicator => 
                currentUrl.Contains(indicator, StringComparison.OrdinalIgnoreCase));
            
            bool containsErrorKeyword = errorIndicators.Any(indicator => 
                currentUrl.Contains(indicator, StringComparison.OrdinalIgnoreCase));

            // If URL contains both login keywords AND error keywords, it's likely a login failure
            if (containsLoginKeyword && containsErrorKeyword)
            {
                _logger.LogWarning("URL contains both login and error indicators - likely login failure: {CurrentUrl}", currentUrl);
                return false;
            }

            // **FIX 5: Efficient polling for positive changes instead of WebDriverWait**
            var endTime = DateTime.Now.AddSeconds(timeoutSeconds);
            
            try
            {
                while (DateTime.Now < endTime && !combinedToken.IsCancellationRequested)
                {
                    combinedToken.ThrowIfCancellationRequested();
                    
                    // Check if URL changed
                    string newUrl = driver.Url;
                    if (!newUrl.Equals(_initialLoginUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("URL change detected during polling: {NewUrl}", newUrl);
                        return true;
                    }
                    
                    // Check page source for success indicators
                    try
                    {
                        string pageSource = driver.PageSource;
                        var pageSuccessIndicators = new[] { "welcome", "dashboard", "logout", "sign out", "signed in", "authenticated", "logged in" };
                        bool hasSuccessContent = pageSuccessIndicators.Any(indicator => 
                            pageSource.Contains(indicator, StringComparison.OrdinalIgnoreCase));
                            
                        if (hasSuccessContent)
                        {
                            _logger.LogDebug("Success content detected in page source");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error checking page source during URL polling");
                    }
                    
                    // Use configurable polling interval for efficient checking
                    await Task.Delay(_timeoutConfig.PollingInterval, combinedToken);
                }
                
                _logger.LogDebug("Timeout waiting for URL change or success indicators");
                return false;
            }
            catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
            {
                _logger.LogDebug("CheckUrlChangedAsync cancelled by timeout");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error in enhanced URL change detection");
            return false;
        }
    }

    private async Task<bool> CheckLoginFormGoneAsync(IWebDriver driver, CancellationToken combinedToken)
    {
        try
        {
            combinedToken.ThrowIfCancellationRequested();
            // Quick check if password field is still present and visible
            var passwordFields = driver.FindElements(By.CssSelector("input[type='password']")); // Can be slow
            combinedToken.ThrowIfCancellationRequested();

            if (passwordFields.Count == 0)
            {
                return true; // No password field found, likely logged in
            }
            
            // Check if password field exists but is hidden (common after login)
            return !passwordFields[0].Displayed; // Can be slow if element is stale
        }
        catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
        {
            _logger.LogDebug("CheckLoginFormGoneAsync cancelled.");
            return false;
        }
    }

    private async Task<bool> CheckForSuccessElementsAsync(IWebDriver driver, int timeoutSeconds, CancellationToken combinedToken, string sessionId)
    {
        try
        {
            _logger.LogDebug("[{SessionId}] Starting success elements check with {TimeoutSeconds}s timeout", sessionId, timeoutSeconds);
            
            // IMPROVED: Use shorter waits but check more selectors efficiently
            var quickWait = new WebDriverWait(driver, TimeSpan.FromSeconds(1));
            var mediumWait = new WebDriverWait(driver, TimeSpan.FromSeconds(Math.Max(2, timeoutSeconds / 2)));
            quickWait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
            mediumWait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

            // ENHANCED: Categorized success selectors by reliability and frequency
            var highPrioritySelectors = new[]
            {
                // Logout/signout indicators (most reliable - always present when logged in)
                "a[href*='logout']:not([style*='display: none']):not([style*='display:none'])", 
                "a[href*='signout']:not([style*='display: none']):not([style*='display:none'])", 
                "a[href*='sign-out']:not([style*='display: none']):not([style*='display:none'])",
                "button[onclick*='logout']:not([style*='display: none']):not([style*='display:none'])", 
                "button[onclick*='signout']:not([style*='display: none']):not([style*='display:none'])",
                "*[data-action*='logout']:not([style*='display: none']):not([style*='display:none'])", 
                "*[data-action*='signout']:not([style*='display: none']):not([style*='display:none'])",
                
                // User profile/menu indicators (very common)
                ".user-profile:not([style*='display: none']):not([style*='display:none'])", 
                ".profile-menu:not([style*='display: none']):not([style*='display:none'])", 
                "#user-menu:not([style*='display: none']):not([style*='display:none'])", 
                ".user-dropdown:not([style*='display: none']):not([style*='display:none'])",
                ".account-menu:not([style*='display: none']):not([style*='display:none'])", 
                ".user-header:not([style*='display: none']):not([style*='display:none'])",
                
                // Welcome messages and username displays
                "span.username:not(:empty)", "div.username:not(:empty)", ".user-name:not(:empty)", 
                ".username-display:not(:empty)", "div.welcome:not(:empty)", "span.welcome:not(:empty)", 
                ".welcome-message:not(:empty)",
            };

            var mediumPrioritySelectors = new[]
            {
                // Dashboard and main application areas
                "div.dashboard", ".dashboard-container", "#dashboard", "main.dashboard",
                ".main-content", ".app-content", "#main-content", "nav.user-menu", 
                ".user-navigation", ".main-navigation",
                
                // Navigation and structural elements
                ".sidebar:not([style*='display: none']):not([style*='display:none'])", 
                "#sidebar:not([style*='display: none']):not([style*='display:none'])", 
                "nav.sidebar:not([style*='display: none']):not([style*='display:none'])", 
                ".main-sidebar:not([style*='display: none']):not([style*='display:none'])",
                ".top-navigation:not([style*='display: none']):not([style*='display:none'])", 
                ".settings-menu:not([style*='display: none']):not([style*='display:none'])",
                
                // Content areas and workspaces
                ".workspace", ".workarea", ".content-area", ".user-workspace",
                ".home-page", ".landing-page", ".member-area",
            };

            var lowPrioritySelectors = new[]
            {
                // Navigation links (common but less specific)
                "a[href*='profile']", "a[href*='account']", "a[href*='settings']",
                "a[href*='preferences']", "a[href*='dashboard']", "a[href*='home']",
                
                // Generic patterns (fallback)
                "*[id*='success']:not(:empty)", "*[class*='success']:not(:empty)",
                "*[data-role='main']", "*[role='main']", 
                "form:not([action*='login']):not([action*='signin'])",
                
                // Framework-specific (if all else fails)
                ".ng-scope", "[ng-controller]", "[data-react-root]", // Angular/React apps
                ".v-application", "[data-v-]" // Vue.js apps
            };

            // PHASE 1: Quick check for high-priority elements (most reliable indicators)
            combinedToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[{SessionId}] 🔍 Phase 1: Checking high-priority success elements", sessionId);
            
            foreach (var selector in highPrioritySelectors)
            {
                try
                {
                    combinedToken.ThrowIfCancellationRequested();
                    var element = quickWait.Until(d => {
                        combinedToken.ThrowIfCancellationRequested();
                        var foundElement = d.FindElement(By.CssSelector(selector));
                        return foundElement?.Displayed == true ? foundElement : null;
                    });

                    if (element != null)
                    {
                        // Additional validation for high-priority elements
                        var elementText = element.Text?.Trim() ?? "";
                        var elementHref = element.GetAttribute("href") ?? "";
                        var elementOnClick = element.GetAttribute("onclick") ?? "";
                        
                        _logger.LogInformation("[{SessionId}] ✅ HIGH-PRIORITY SUCCESS ELEMENT FOUND: {Selector} " +
                                             "(text: '{Text}', href: '{Href}', onclick: '{OnClick}')", 
                                             sessionId, selector, 
                                             elementText.Length > 30 ? elementText.Substring(0, 30) + "..." : elementText,
                                             elementHref.Length > 50 ? elementHref.Substring(0, 50) + "..." : elementHref,
                                             elementOnClick.Length > 30 ? elementOnClick.Substring(0, 30) + "..." : elementOnClick);
                        return true;
                    }
                }
                catch (WebDriverTimeoutException)
                {
                    continue; // Try next selector
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] Error checking high-priority selector {Selector}", sessionId, selector);
                    continue;
                }
            }

            // PHASE 2: Medium-priority elements if no high-priority found
            combinedToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[{SessionId}] 🔍 Phase 2: Checking medium-priority success elements", sessionId);
            
            foreach (var selector in mediumPrioritySelectors)
            {
                try
                {
                    combinedToken.ThrowIfCancellationRequested();
                    var element = quickWait.Until(d => {
                        combinedToken.ThrowIfCancellationRequested();
                        var foundElement = d.FindElement(By.CssSelector(selector));
                        return foundElement?.Displayed == true ? foundElement : null;
                    });

                    if (element != null)
                    {
                        var elementText = element.Text?.Trim() ?? "";
                        _logger.LogInformation("[{SessionId}] ✅ MEDIUM-PRIORITY SUCCESS ELEMENT FOUND: {Selector} " +
                                             "(text preview: '{Text}')", 
                                             sessionId, selector, 
                                             elementText.Length > 40 ? elementText.Substring(0, 40) + "..." : elementText);
                        return true;
                    }
                }
                catch (WebDriverTimeoutException)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] Error checking medium-priority selector {Selector}", sessionId, selector);
                    continue;
                }
            }

            // PHASE 3: Extended wait for low-priority elements (if time permits)
            var elapsedTime = DateTime.Now.Subtract(DateTime.Now.AddSeconds(-timeoutSeconds)).TotalSeconds;
            var remainingTime = Math.Max(0, timeoutSeconds - elapsedTime);
            
            if (remainingTime > 1)
            {
                combinedToken.ThrowIfCancellationRequested();
                _logger.LogDebug("[{SessionId}] 🔍 Phase 3: Extended check for low-priority elements ({RemainingTime:F1}s remaining)", 
                               sessionId, remainingTime);
                
                var extendedWait = new WebDriverWait(driver, TimeSpan.FromSeconds(Math.Min(remainingTime, 2)));
                extendedWait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

                foreach (var selector in lowPrioritySelectors.Take(10)) // Limit to first 10 for performance
                {
                    try
                    {
                        combinedToken.ThrowIfCancellationRequested();
                        var element = extendedWait.Until(d => {
                            combinedToken.ThrowIfCancellationRequested();
                            var foundElement = d.FindElement(By.CssSelector(selector));
                            return foundElement?.Displayed == true ? foundElement : null;
                        });

                        if (element != null)
                        {
                            _logger.LogInformation("[{SessionId}] ✅ LOW-PRIORITY SUCCESS ELEMENT FOUND: {Selector}", sessionId, selector);
                            return true;
                        }
                    }
                    catch (WebDriverTimeoutException)
                    {
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[{SessionId}] Error checking low-priority selector {Selector}", sessionId, selector);
                        continue;
                    }
                }
            }

            // PHASE 4: Final comprehensive check - look for absence of login elements
            combinedToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[{SessionId}] 🔍 Phase 4: Checking for absence of login-specific elements", sessionId);
            
            try
            {
                var loginSpecificElements = driver.FindElements(By.CssSelector(
                    "input[type='password']:not([style*='display: none']):not([style*='display:none']), " +
                    "form[action*='login']:not([style*='display: none']):not([style*='display:none']), " +
                    "form[action*='signin']:not([style*='display: none']):not([style*='display:none']), " +
                    ".login-form:not([style*='display: none']):not([style*='display:none']), " +
                    ".signin-form:not([style*='display: none']):not([style*='display:none']), " +
                    "#login-form:not([style*='display: none']):not([style*='display:none']), " +
                    "#signin-form:not([style*='display: none']):not([style*='display:none'])"));
                
                var visibleLoginElements = loginSpecificElements.Where(e => {
                    try { return e.Displayed; } catch { return false; }
                }).ToList();

                if (!visibleLoginElements.Any())
                {
                    _logger.LogDebug("[{SessionId}] No visible login-specific elements found - checking URL and page characteristics", sessionId);
                    
                    // Additional validation: check if we're on a reasonable page (not error page)
                    var currentUrl = driver.Url?.ToLower() ?? "";
                    var isErrorPage = new[] { "error", "404", "403", "500", "unauthorized", "forbidden", "denied" }
                        .Any(error => currentUrl.Contains(error));
                    
                    if (!isErrorPage)
                    {
                        // Check for some basic page structure indicating a functional page
                        var basicStructure = driver.FindElements(By.CssSelector("body, html, main, div, nav, header"));
                        if (basicStructure.Any())
                        {
                            _logger.LogInformation("[{SessionId}] ✅ SUCCESS INFERRED: No login elements visible + not on error page + has basic page structure", sessionId);
                            return true;
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("[{SessionId}] Found {Count} visible login-specific elements, login may not be complete", 
                                   sessionId, visibleLoginElements.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[{SessionId}] Error in final login element absence check", sessionId);
            }

            _logger.LogDebug("[{SessionId}] No definitive success elements found in any phase", sessionId);
            return false;
        }
        catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
        {
            _logger.LogDebug("[{SessionId}] CheckForSuccessElementsAsync cancelled by timeout", sessionId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{SessionId}] Error in enhanced success elements detection", sessionId);
            return false;
        }
    }

    private async Task<bool> CheckForErrorMessagesAsync(IWebDriver driver, int timeoutSeconds, CancellationToken combinedToken, string sessionId)
    {
        try
        {
            _logger.LogDebug("[{SessionId}] Starting error message detection with {TimeoutSeconds}s timeout", sessionId, timeoutSeconds);
            
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

            // ENHANCED: Categorize error selectors by severity and specificity
            var criticalErrorSelectors = new[]
            {
                // High-confidence login failure indicators
                "div.error:not(:empty):contains('invalid')", "div.error:not(:empty):contains('incorrect')",
                "div.error:not(:empty):contains('failed')", "div.error:not(:empty):contains('denied')",
                "span.error:not(:empty):contains('invalid')", "span.error:not(:empty):contains('incorrect')",
                "p.error:not(:empty):contains('failed')", "p.error:not(:empty):contains('denied')",
                
                // Alert-based error messages
                "div.alert-danger:not(:empty)", "div.alert-error:not(:empty)",
                "[role='alert']:not(:empty)[class*='error']", "[role='alert']:not(:empty)[class*='danger']",
                
                // Login-specific error containers
                ".login-error:not(:empty)", ".authentication-error:not(:empty)", 
                ".signin-error:not(:empty)", ".auth-error:not(:empty)",
                "#login-error:not(:empty)", "#authentication-error:not(:empty)",
                
                // Common error patterns with explicit failure terms
                "*[class*='error']:not(:empty):contains('login')", "*[class*='error']:not(:empty):contains('password')",
                "*[class*='error']:not(:empty):contains('username')", "*[class*='error']:not(:empty):contains('credential')"
            };

            var moderateErrorSelectors = new[]
            {
                // General error containers (need content validation)
                "div.error:not(:empty)", "span.error:not(:empty)", "p.error:not(:empty)",
                "div.alert:not(:empty)", "[role='alert']:not(:empty)",
                ".validation-error:not(:empty)", ".field-error:not(:empty)",
                ".form-error:not(:empty)", ".message-error:not(:empty)"
            };

            // PHASE 1: Check for critical/specific error messages
            combinedToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[{SessionId}] 🔍 Phase 1: Checking for critical error indicators", sessionId);
            
            foreach (var selector in criticalErrorSelectors)
            {
                try
                {
                    combinedToken.ThrowIfCancellationRequested();
                    
                    // For selectors with :contains(), we need to handle them specially since WebDriver doesn't support CSS :contains()
                    var baseSelector = selector.Split(':')[0]; // Get base selector before :contains()
                    var containsText = ExtractContainsText(selector);
                    
                    var elements = driver.FindElements(By.CssSelector(baseSelector));
                    foreach (var element in elements)
                    {
                        try
                        {
                            if (element.Displayed && !string.IsNullOrWhiteSpace(element.Text))
                            {
                                var elementText = element.Text.ToLower();
                                
                                // Check contains condition if specified
                                if (!string.IsNullOrEmpty(containsText))
                                {
                                    if (!elementText.Contains(containsText.ToLower()))
                                        continue;
                                }
                                
                                // Additional validation for critical errors
                                if (IsCriticalLoginError(elementText))
                                {
                                    _logger.LogError("[{SessionId}] ❌ CRITICAL ERROR MESSAGE DETECTED: {Selector} - '{Text}'", 
                                                   sessionId, selector, element.Text.Trim());
                                    return true;
                                }
                            }
                        }
                        catch (StaleElementReferenceException)
                        {
                            continue; // Element became stale, try next
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] Error checking critical selector {Selector}", sessionId, selector);
                    continue;
                }
            }

            // PHASE 2: Check moderate error selectors with content filtering
            combinedToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[{SessionId}] 🔍 Phase 2: Checking moderate error indicators with content validation", sessionId);
            
            foreach (var selector in moderateErrorSelectors)
            {
                try
                {
                    combinedToken.ThrowIfCancellationRequested();
                    
                    var errorElement = wait.Until(d => {
                        combinedToken.ThrowIfCancellationRequested();
                        var element = d.FindElement(By.CssSelector(selector));
                        return element?.Displayed == true && !string.IsNullOrWhiteSpace(element.Text) ? element : null;
                    });
                    
                    if (errorElement != null)
                    {
                        var errorText = errorElement.Text.Trim();
                        _logger.LogDebug("[{SessionId}] Found potential error element: {Selector} with text: '{Text}'", 
                                       sessionId, selector, errorText);
                        
                        // ENHANCED: Filter out false positives
                        if (IsActualLoginError(errorText))
                        {
                            _logger.LogError("[{SessionId}] ❌ VALIDATED ERROR MESSAGE indicating login failure: {Selector} - '{Text}'", 
                                           sessionId, selector, errorText);
                            return true;
                        }
                        else
                        {
                            _logger.LogDebug("[{SessionId}] Error element dismissed as non-critical: '{Text}'", sessionId, errorText);
                        }
                    }
                }
                catch (WebDriverTimeoutException)
                {
                    continue; // No element found with this selector
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] Error checking moderate selector {Selector}", sessionId, selector);
                    continue;
                }
            }

            // PHASE 3: Page source analysis for error patterns (last resort)
            combinedToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[{SessionId}] 🔍 Phase 3: Analyzing page source for error patterns", sessionId);
            
            try
            {
                var pageSource = driver.PageSource;
                if (pageSource.Length > 10000) 
                {
                    // Focus on first 10k chars for performance, but also check around common error locations
                    var snippet1 = pageSource.Substring(0, Math.Min(5000, pageSource.Length));
                    var snippet2 = pageSource.Length > 5000 ? 
                        pageSource.Substring(Math.Max(0, pageSource.Length - 5000)) : "";
                    pageSource = snippet1 + " " + snippet2;
                }
                
                var criticalErrorPatterns = new[] 
                { 
                    "invalid credentials", "invalid username", "invalid password",
                    "login failed", "authentication failed", "signin failed",
                    "incorrect password", "incorrect username", "incorrect credentials",
                    "access denied", "login denied", "authentication denied",
                    "account locked", "account disabled", "account suspended"
                };
                
                foreach (var pattern in criticalErrorPatterns)
                {
                    combinedToken.ThrowIfCancellationRequested();
                    if (pageSource.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("[{SessionId}] ❌ CRITICAL ERROR PATTERN found in page source: '{Pattern}'", sessionId, pattern);
                        return true;
                    }
                }
                
                _logger.LogDebug("[{SessionId}] No critical error patterns found in page source analysis", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[{SessionId}] Error during page source analysis", sessionId);
            }

            _logger.LogDebug("[{SessionId}] No definitive error messages detected", sessionId);
            return false;
        }
        catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
        {
            _logger.LogDebug("[{SessionId}] CheckForErrorMessagesAsync cancelled by timeout", sessionId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{SessionId}] Error in enhanced error message detection", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Extracts the text from a CSS selector's :contains() pseudo-class
    /// </summary>
    private string ExtractContainsText(string selector)
    {
        var containsIndex = selector.IndexOf(":contains('");
        if (containsIndex == -1) return string.Empty;
        
        var startIndex = containsIndex + ":contains('".Length;
        var endIndex = selector.IndexOf("')", startIndex);
        if (endIndex == -1) return string.Empty;
        
        return selector.Substring(startIndex, endIndex - startIndex);
    }

    /// <summary>
    /// Determines if error text indicates a critical login failure
    /// </summary>
    private bool IsCriticalLoginError(string errorText)
    {
        var criticalKeywords = new[]
        {
            "invalid", "incorrect", "wrong", "failed", "denied", "unauthorized",
            "locked", "disabled", "suspended", "blocked", "expired"
        };
        
        var loginContextKeywords = new[]
        {
            "login", "signin", "password", "username", "credential", "authentication", "auth"
        };
        
        var lowerText = errorText.ToLower();
        
        // Must contain both a critical keyword AND a login context keyword
        var hasCriticalKeyword = criticalKeywords.Any(keyword => lowerText.Contains(keyword));
        var hasLoginContext = loginContextKeywords.Any(keyword => lowerText.Contains(keyword));
        
        return hasCriticalKeyword && hasLoginContext;
    }

    /// <summary>
    /// Determines if error text represents an actual login error vs. validation hint
    /// </summary>
    private bool IsActualLoginError(string errorText)
    {
        var lowerText = errorText.ToLower();
        
        // WHITELIST: These are definitely login errors
        var definiteErrors = new[]
        {
            "invalid credentials", "invalid username", "invalid password",
            "incorrect password", "incorrect username", "incorrect credentials", 
            "wrong password", "wrong username", "wrong credentials",
            "login failed", "authentication failed", "signin failed", "sign-in failed",
            "access denied", "login denied", "authentication denied",
            "account locked", "account disabled", "account suspended", "account blocked",
            "password expired", "account expired", "session expired"
        };
        
        if (definiteErrors.Any(error => lowerText.Contains(error)))
        {
            return true;
        }
        
        // BLACKLIST: These are likely just validation hints, not login failures
        var validationHints = new[]
        {
            "required", "field is required", "cannot be empty", "please enter",
            "must be", "should be", "format", "length", "character", "special character",
            "number", "digit", "uppercase", "lowercase", "match", "confirm",
            "email format", "@", ".com", "valid email", "phone", "hint:", "example:",
            "minimum", "maximum", "between", "range"
        };
        
        if (validationHints.Any(hint => lowerText.Contains(hint)))
        {
            _logger.LogDebug("Error text appears to be validation hint, not login failure: '{Text}'", errorText);
            return false;
        }
        
        // For ambiguous cases, apply stricter criteria
        if (IsCriticalLoginError(errorText))
        {
            return true;
        }
        
        // If it's just generic "error" text without specific login failure context, probably not critical
        if (lowerText.Length < 15 && (lowerText.Contains("error") || lowerText.Contains("invalid")))
        {
            _logger.LogDebug("Generic short error text, likely not login failure: '{Text}'", errorText);
            return false;
        }
        
        return false;
    }

    /// <summary>
    /// Fast title change detection method for login success verification.
    /// Compares the current page title with the initial title to detect navigation.
    /// </summary>
    /// <param name="driver">The WebDriver instance to check the title from.</param>
    /// <param name="initialTitle">The initial page title before login attempt.</param>
    /// <param name="sessionId">Session ID for logging purposes (optional).</param>
    /// <returns>True if the title has changed (indicating successful navigation), false otherwise.</returns>
    private bool DetectTitleChange(IWebDriver driver, string initialTitle, string sessionId = "")
    {
        var startTime = DateTime.Now;
        
        try
        {
            // Get current page title
            string currentTitle = driver.Title ?? string.Empty;
            string safeInitialTitle = initialTitle ?? string.Empty;
            
            _logger.LogDebug("[{SessionId}] Title detection - Initial: '{InitialTitle}' | Current: '{CurrentTitle}'", 
                           sessionId, safeInitialTitle, currentTitle);
            
            // Handle edge cases
            if (string.IsNullOrWhiteSpace(safeInitialTitle) && string.IsNullOrWhiteSpace(currentTitle))
            {
                _logger.LogDebug("[{SessionId}] Both titles are empty/null - no change detected", sessionId);
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(safeInitialTitle))
            {
                _logger.LogDebug("[{SessionId}] Initial title was empty but current title exists - change detected", sessionId);
                return !string.IsNullOrWhiteSpace(currentTitle);
            }
            
            if (string.IsNullOrWhiteSpace(currentTitle))
            {
                _logger.LogDebug("[{SessionId}] Current title is empty but initial title existed - change detected", sessionId);
                return true;
            }
            
            // Compare titles (case-insensitive for robustness)
            bool titleChanged = !string.Equals(safeInitialTitle.Trim(), currentTitle.Trim(), StringComparison.OrdinalIgnoreCase);
            
            var duration = DateTime.Now - startTime;
            
            if (titleChanged)
            {
                _logger.LogInformation("[{SessionId}] ✅ TITLE CHANGE DETECTED in {Duration}ms: '{InitialTitle}' → '{CurrentTitle}'", 
                                     sessionId, duration.TotalMilliseconds, safeInitialTitle, currentTitle);
            }
            else
            {
                _logger.LogDebug("[{SessionId}] No title change detected in {Duration}ms: '{Title}'", 
                               sessionId, duration.TotalMilliseconds, currentTitle);
            }
            
            return titleChanged;
        }
        catch (WebDriverException ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogWarning(ex, "[{SessionId}] WebDriver error during title detection after {Duration}ms: {ErrorMessage}", 
                             sessionId, duration.TotalMilliseconds, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "[{SessionId}] Unexpected error during title detection after {Duration}ms: {ErrorMessage}", 
                           sessionId, duration.TotalMilliseconds, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Fast URL change detection method for login success verification.
    /// Compares the current page URL with the initial URL to detect navigation.
    /// </summary>
    /// <param name="driver">The WebDriver instance to check the URL from.</param>
    /// <param name="initialUrl">The initial page URL before login attempt.</param>
    /// <param name="sessionId">Session ID for logging purposes (optional).</param>
    /// <returns>True if the URL has changed meaningfully (indicating successful navigation), false otherwise.</returns>
    private bool DetectUrlChange(IWebDriver driver, string initialUrl, string sessionId = "")
    {
        var startTime = DateTime.Now;
        
        try
        {
            // Get current page URL
            string currentUrl = driver.Url ?? string.Empty;
            string safeInitialUrl = initialUrl ?? string.Empty;
            
            _logger.LogDebug("[{SessionId}] URL detection - Initial: '{InitialUrl}' | Current: '{CurrentUrl}'", 
                           sessionId, safeInitialUrl, currentUrl);
            
            // Handle edge cases
            if (string.IsNullOrWhiteSpace(safeInitialUrl) && string.IsNullOrWhiteSpace(currentUrl))
            {
                _logger.LogDebug("[{SessionId}] Both URLs are empty/null - no change detected", sessionId);
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(safeInitialUrl))
            {
                _logger.LogDebug("[{SessionId}] Initial URL was empty but current URL exists - change detected", sessionId);
                return !string.IsNullOrWhiteSpace(currentUrl);
            }
            
            if (string.IsNullOrWhiteSpace(currentUrl))
            {
                _logger.LogDebug("[{SessionId}] Current URL is empty but initial URL existed - change detected", sessionId);
                return true;
            }
            
            // Fast path: direct string comparison (case-insensitive)
            if (string.Equals(safeInitialUrl.Trim(), currentUrl.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var duration = DateTime.Now - startTime;
                _logger.LogDebug("[{SessionId}] No URL change detected in {Duration}ms: '{Url}'", 
                               sessionId, duration.TotalMilliseconds, currentUrl);
                return false;
            }
            
            // URLs are different - determine if it's a meaningful change
            bool isMeaningfulChange = false;
            string changeReason = "unknown";
            
            try
            {
                // Parse URLs for detailed comparison
                if (Uri.TryCreate(safeInitialUrl, UriKind.Absolute, out Uri? initialUri) && 
                    Uri.TryCreate(currentUrl, UriKind.Absolute, out Uri? currentUri))
                {
                    // Check different components
                    bool sameHost = string.Equals(initialUri.Host, currentUri.Host, StringComparison.OrdinalIgnoreCase);
                    bool samePath = string.Equals(initialUri.AbsolutePath, currentUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                    bool sameQuery = string.Equals(initialUri.Query, currentUri.Query, StringComparison.OrdinalIgnoreCase);
                    bool sameFragment = string.Equals(initialUri.Fragment, currentUri.Fragment, StringComparison.OrdinalIgnoreCase);
                    
                    if (!sameHost)
                    {
                        isMeaningfulChange = true;
                        changeReason = $"host change: '{initialUri.Host}' → '{currentUri.Host}'";
                    }
                    else if (!samePath)
                    {
                        isMeaningfulChange = true;
                        changeReason = $"path change: '{initialUri.AbsolutePath}' → '{currentUri.AbsolutePath}'";
                    }
                    else if (!sameQuery)
                    {
                        // Query parameter changes can be meaningful (e.g., login success tokens)
                        // but also can be just tracking parameters
                        isMeaningfulChange = true;
                        changeReason = $"query change: '{initialUri.Query}' → '{currentUri.Query}'";
                    }
                    else if (!sameFragment)
                    {
                        // Fragment changes are usually less significant but still indicate navigation
                        isMeaningfulChange = true;
                        changeReason = $"fragment change: '{initialUri.Fragment}' → '{currentUri.Fragment}'";
                    }
                }
                else
                {
                    // Fallback: if URI parsing fails, any string difference is considered meaningful
                    isMeaningfulChange = true;
                    changeReason = "URI parsing failed, using string comparison";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[{SessionId}] Error parsing URLs, falling back to string comparison", sessionId);
                isMeaningfulChange = true;
                changeReason = "URL parsing error, assuming meaningful change";
            }
            
            var totalDuration = DateTime.Now - startTime;
            
            if (isMeaningfulChange)
            {
                _logger.LogInformation("[{SessionId}] ✅ URL CHANGE DETECTED in {Duration}ms ({Reason}): '{InitialUrl}' → '{CurrentUrl}'", 
                                     sessionId, totalDuration.TotalMilliseconds, changeReason, safeInitialUrl, currentUrl);
            }
            else
            {
                _logger.LogDebug("[{SessionId}] URL changed but not meaningfully in {Duration}ms: '{InitialUrl}' → '{CurrentUrl}'", 
                               sessionId, totalDuration.TotalMilliseconds, safeInitialUrl, currentUrl);
            }
            
            return isMeaningfulChange;
        }
        catch (WebDriverException ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogWarning(ex, "[{SessionId}] WebDriver error during URL detection after {Duration}ms: {ErrorMessage}", 
                             sessionId, duration.TotalMilliseconds, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "[{SessionId}] Unexpected error during URL detection after {Duration}ms: {ErrorMessage}", 
                           sessionId, duration.TotalMilliseconds, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Fast page layout change detection method for login success verification.
    /// Checks if login form elements have disappeared, indicating successful navigation away from login page.
    /// </summary>
    /// <param name="driver">The WebDriver instance to check elements from.</param>
    /// <param name="loginFormElements">Array of By selectors for login form elements (username, password, submit button).</param>
    /// <param name="sessionId">Session ID for logging purposes (optional).</param>
    /// <returns>True if login form elements are no longer visible (indicating successful login), false otherwise.</returns>
    private bool DetectLayoutChange(IWebDriver driver, By[] loginFormElements, string sessionId = "")
    {
        var startTime = DateTime.Now;
        
        try
        {
            // Handle edge cases
            if (loginFormElements == null || loginFormElements.Length == 0)
            {
                _logger.LogDebug("[{SessionId}] No login form elements provided for layout detection", sessionId);
                return false; // Can't detect layout change without elements to check
            }
            
            if (driver == null)
            {
                _logger.LogDebug("[{SessionId}] WebDriver is null for layout detection", sessionId);
                return false;
            }
            
            _logger.LogDebug("[{SessionId}] Layout detection - checking {ElementCount} login form elements", 
                           sessionId, loginFormElements.Length);
            
            int visibleElementsCount = 0;
            int totalElementsChecked = 0;
            var elementResults = new List<string>();
            
            // Check each login form element
            foreach (var elementSelector in loginFormElements)
            {
                try
                {
                    totalElementsChecked++;
                    
                    // Use FindElements to avoid exceptions and get count
                    var elements = driver.FindElements(elementSelector);
                    
                    if (elements.Count > 0)
                    {
                        // Check if any of the found elements are actually visible
                        bool hasVisibleElement = false;
                        foreach (var element in elements)
                        {
                            try
                            {
                                if (element.Displayed && element.Enabled)
                                {
                                    hasVisibleElement = true;
                                    break;
                                }
                            }
                            catch (StaleElementReferenceException)
                            {
                                // Element became stale, consider it as not visible
                                continue;
                            }
                            catch (WebDriverException)
                            {
                                // Other WebDriver issues, skip this element
                                continue;
                            }
                        }
                        
                        if (hasVisibleElement)
                        {
                            visibleElementsCount++;
                            elementResults.Add($"{elementSelector}: VISIBLE ({elements.Count} found)");
                        }
                        else
                        {
                            elementResults.Add($"{elementSelector}: HIDDEN ({elements.Count} found but not displayed)");
                        }
                    }
                    else
                    {
                        elementResults.Add($"{elementSelector}: NOT FOUND");
                    }
                }
                catch (WebDriverException ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] WebDriver error checking element {ElementSelector}: {ErrorMessage}", 
                                   sessionId, elementSelector, ex.Message);
                    elementResults.Add($"{elementSelector}: ERROR ({ex.Message})");
                    // Continue checking other elements
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] Unexpected error checking element {ElementSelector}: {ErrorMessage}", 
                                   sessionId, elementSelector, ex.Message);
                    elementResults.Add($"{elementSelector}: UNEXPECTED ERROR ({ex.Message})");
                    // Continue checking other elements
                }
            }
            
            var totalDuration = DateTime.Now - startTime;
            
            // Determine if layout has changed (login form disappeared)
            bool layoutChanged = visibleElementsCount == 0 && totalElementsChecked > 0;
            
            if (layoutChanged)
            {
                _logger.LogInformation("[{SessionId}] ✅ LAYOUT CHANGE DETECTED in {Duration}ms: All {ElementCount} login form elements are gone", 
                                     sessionId, totalDuration.TotalMilliseconds, totalElementsChecked);
                _logger.LogDebug("[{SessionId}] Element check results: {ElementResults}", 
                               sessionId, string.Join("; ", elementResults));
            }
            else
            {
                _logger.LogDebug("[{SessionId}] No layout change detected in {Duration}ms: {VisibleCount}/{TotalCount} login form elements still visible", 
                               sessionId, totalDuration.TotalMilliseconds, visibleElementsCount, totalElementsChecked);
                _logger.LogDebug("[{SessionId}] Element check results: {ElementResults}", 
                               sessionId, string.Join("; ", elementResults));
            }
            
            return layoutChanged;
        }
        catch (WebDriverException ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogWarning(ex, "[{SessionId}] WebDriver error during layout detection after {Duration}ms: {ErrorMessage}", 
                             sessionId, duration.TotalMilliseconds, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "[{SessionId}] Unexpected error during layout detection after {Duration}ms: {ErrorMessage}", 
                           sessionId, duration.TotalMilliseconds, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Fast login verification method that orchestrates all three detection methods.
    /// Runs title change, URL change, and layout change detection in parallel for maximum speed.
    /// Returns true if ANY detection method indicates successful login.
    /// </summary>
    /// <param name="driver">The WebDriver instance to check.</param>
    /// <param name="initialUrl">The initial page URL before login attempt.</param>
    /// <param name="initialTitle">The initial page title before login attempt.</param>
    /// <param name="loginFormElements">Array of By selectors for login form elements to check for disappearance.</param>
    /// <param name="sessionId">Session ID for logging purposes (optional).</param>
    /// <returns>True if any detection method indicates successful login, false otherwise.</returns>
    public async Task<bool> FastVerifyLoginSuccess(IWebDriver driver, string initialUrl, string initialTitle, By[] loginFormElements, string sessionId = "")
    {
        var startTime = DateTime.Now;
        
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N")[..8];
        }
        
        _logger.LogInformation("[{SessionId}] === FAST LOGIN VERIFICATION STARTED ===", sessionId);
        _logger.LogDebug("[{SessionId}] Parameters - InitialUrl: '{InitialUrl}', InitialTitle: '{InitialTitle}', FormElements: {ElementCount}", 
                        sessionId, initialUrl, initialTitle, loginFormElements?.Length ?? 0);
        
        try
        {
            // Validate input parameters
            if (driver == null)
            {
                _logger.LogError("[{SessionId}] WebDriver is null - cannot perform verification", sessionId);
                return false;
            }
            
            // Use fast timeout (1 second max, configurable)
            var fastTimeout = TimeSpan.FromSeconds(Math.Min(1.0, _timeoutConfig.InternalTimeout.TotalSeconds));
            using var cts = new CancellationTokenSource(fastTimeout);
            
            _logger.LogDebug("[{SessionId}] Starting parallel execution of 3 detection methods with {Timeout}ms timeout", 
                           sessionId, fastTimeout.TotalMilliseconds);
            
            // Start all three detection methods in parallel for maximum speed
            var titleTask = Task.Run(() => 
            {
                try
                {
                    return DetectTitleChange(driver, initialTitle, sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] Title detection method failed: {ErrorMessage}", sessionId, ex.Message);
                    return false;
                }
            }, cts.Token);
            
            var urlTask = Task.Run(() => 
            {
                try
                {
                    return DetectUrlChange(driver, initialUrl, sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] URL detection method failed: {ErrorMessage}", sessionId, ex.Message);
                    return false;
                }
            }, cts.Token);
            
            var layoutTask = Task.Run(() => 
            {
                try
                {
                    return DetectLayoutChange(driver, loginFormElements ?? Array.Empty<By>(), sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[{SessionId}] Layout detection method failed: {ErrorMessage}", sessionId, ex.Message);
                    return false;
                }
            }, cts.Token);
            
            // PERFORMANCE FIX: Check for early success instead of waiting for all tasks
            // Poll task results every 50ms and return immediately when ANY task succeeds
            var pollInterval = TimeSpan.FromMilliseconds(50);
            bool overallSuccess = false;
            bool titleSuccess = false, urlSuccess = false, layoutSuccess = false;
            
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    // Check if any task has completed successfully with a positive result
                    titleSuccess = titleTask.IsCompletedSuccessfully && titleTask.Result;
                    urlSuccess = urlTask.IsCompletedSuccessfully && urlTask.Result;
                    layoutSuccess = layoutTask.IsCompletedSuccessfully && layoutTask.Result;
                    
                    // Early exit: If ANY method indicates success, we're done!
                    if (titleSuccess || urlSuccess || layoutSuccess)
                    {
                        overallSuccess = true;
                        _logger.LogDebug("[{SessionId}] Early success detected - cancelling remaining tasks", sessionId);
                        cts.Cancel(); // Cancel remaining tasks
                        break;
                    }
                    
                    // Check if all tasks completed (even with negative results)
                    if (titleTask.IsCompleted && urlTask.IsCompleted && layoutTask.IsCompleted)
                    {
                        _logger.LogDebug("[{SessionId}] All tasks completed - no success detected", sessionId);
                        break;
                    }
                    
                    // Wait a bit before checking again
                    await Task.Delay(pollInterval, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                _logger.LogWarning("[{SessionId}] Fast verification timed out after {Timeout}ms", sessionId, fastTimeout.TotalMilliseconds);
                
                // Still collect results from any completed tasks
                titleSuccess = titleTask.IsCompletedSuccessfully && titleTask.Result;
                urlSuccess = urlTask.IsCompletedSuccessfully && urlTask.Result;
                layoutSuccess = layoutTask.IsCompletedSuccessfully && layoutTask.Result;
                overallSuccess = titleSuccess || urlSuccess || layoutSuccess;
            }
            
            var totalDuration = DateTime.Now - startTime;
            
            // Log detailed results
            _logger.LogInformation("[{SessionId}] 📊 FAST VERIFICATION RESULTS in {Duration}ms:", sessionId, totalDuration.TotalMilliseconds);
            _logger.LogInformation("[{SessionId}]   • Title change: {TitleResult} (task: {TitleStatus})", 
                                 sessionId, titleSuccess ? "✅ SUCCESS" : "❌ NO CHANGE", 
                                 titleTask.IsCompletedSuccessfully ? "completed" : titleTask.Status.ToString().ToLower());
            _logger.LogInformation("[{SessionId}]   • URL change: {UrlResult} (task: {UrlStatus})", 
                                 sessionId, urlSuccess ? "✅ SUCCESS" : "❌ NO CHANGE", 
                                 urlTask.IsCompletedSuccessfully ? "completed" : urlTask.Status.ToString().ToLower());
            _logger.LogInformation("[{SessionId}]   • Layout change: {LayoutResult} (task: {LayoutStatus})", 
                                 sessionId, layoutSuccess ? "✅ SUCCESS" : "❌ NO CHANGE", 
                                 layoutTask.IsCompletedSuccessfully ? "completed" : layoutTask.Status.ToString().ToLower());
            
            var resultSymbol = overallSuccess ? "✅" : "❌";
            _logger.LogInformation("[{SessionId}] {Symbol} OVERALL RESULT: {Result} (any method success = login success)", 
                                 sessionId, resultSymbol, overallSuccess ? "LOGIN SUCCESS" : "LOGIN FAILURE");
            
            // Performance validation
            if (totalDuration.TotalMilliseconds > 1000)
            {
                _logger.LogWarning("[{SessionId}] ⚠️ Performance warning: Fast verification took {Duration}ms (target: <1000ms)", 
                                 sessionId, totalDuration.TotalMilliseconds);
            }
            else if (totalDuration.TotalMilliseconds < 100)
            {
                _logger.LogInformation("[{SessionId}] ⚡ Excellent performance: Fast verification completed in {Duration}ms", 
                                     sessionId, totalDuration.TotalMilliseconds);
            }
            
            _logger.LogInformation("[{SessionId}] === FAST LOGIN VERIFICATION ENDED: {Result} ===", 
                                 sessionId, overallSuccess ? "SUCCESS" : "FAILURE");
            
            return overallSuccess;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "[{SessionId}] 💥 UNEXPECTED ERROR in fast verification after {Duration}ms: {ErrorMessage}", 
                           sessionId, duration.TotalMilliseconds, ex.Message);
            _logger.LogInformation("[{SessionId}] === FAST LOGIN VERIFICATION ENDED: ERROR ===", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Captures a screenshot of the current browser state.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture from.</param>
    /// <param name="prefix">A prefix for the screenshot filename.</param>
    /// <returns>The path to the saved screenshot, or null if the capture failed.</returns>
    public string CaptureScreenshot(IWebDriver driver, string prefix)
    {
        try
        {
            if (driver is ITakesScreenshot screenshotDriver)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
                string filename = $"{prefix}_{timestamp}.png";
                string filePath = Path.Combine("screenshots", filename);
                
                // Ensure directory exists
                Directory.CreateDirectory("screenshots");
                
                Screenshot screenshot = screenshotDriver.GetScreenshot();
                screenshot.SaveAsFile(filePath);
                
                _logger.LogInformation("Screenshot captured: {FilePath}", filePath);
                return filePath;
            }
            else
            {
                _logger.LogWarning("WebDriver does not support screenshot capture");
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot with prefix: {Prefix}", prefix);
            return string.Empty;
        }
    }
}

/// <summary>
/// Configuration for login verification behavior
/// </summary>
public class LoginVerificationConfig
{
    /// <summary>
    /// Maximum time to spend on login verification in seconds
    /// </summary>
    public int MaxVerificationTimeSeconds { get; set; } = 10;

    /// <summary>
    /// Initial delay before starting verification checks in milliseconds
    /// </summary>
    public int InitialDelayMs { get; set; } = 500;

    /// <summary>
    /// Whether to enable detailed timing logs
    /// </summary>
    public bool EnableTimingLogs { get; set; } = true;

    /// <summary>
    /// Whether to capture screenshots on verification failures
    /// </summary>
    public bool CaptureScreenshotsOnFailure { get; set; } = true;
}
