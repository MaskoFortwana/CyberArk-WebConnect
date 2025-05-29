using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Text.Json;
using WebConnect.Core;
using WebConnect.Models;
using WebConnect.Exceptions;

namespace WebConnect.Services
{
    /// <summary>
    /// Service for handling JavaScript-heavy page interactions and dynamic content.
    /// </summary>
    public class JavaScriptInteractionManager
    {
        private readonly ILogger<JavaScriptInteractionManager> _logger;
        private readonly TimeoutManager _timeoutManager;
        private readonly JavaScriptInteractionConfiguration _configuration;
        private readonly List<JavaScriptError> _capturedErrors;

        /// <summary>
        /// Event fired when JavaScript is executed.
        /// </summary>
        public event EventHandler<JavaScriptExecutionEventArgs>? JavaScriptExecuted;

        /// <summary>
        /// Event fired when a JavaScript error is captured.
        /// </summary>
        public event EventHandler<JavaScriptErrorEventArgs>? JavaScriptErrorCaptured;

        /// <summary>
        /// Event fired when performance metrics are collected.
        /// </summary>
        public event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsCollected;

        /// <summary>
        /// Initializes a new instance of the <see cref="JavaScriptInteractionManager"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="timeoutManager">The timeout manager.</param>
        /// <param name="configuration">The JavaScript interaction configuration.</param>
        public JavaScriptInteractionManager(
            ILogger<JavaScriptInteractionManager> logger,
            TimeoutManager timeoutManager,
            JavaScriptInteractionConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeoutManager = timeoutManager ?? throw new ArgumentNullException(nameof(timeoutManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _capturedErrors = new List<JavaScriptError>();
        }

        /// <summary>
        /// Gets the list of captured JavaScript errors.
        /// </summary>
        public IReadOnlyList<JavaScriptError> CapturedErrors => _capturedErrors.AsReadOnly();

        /// <summary>
        /// Initializes JavaScript error capturing for the current page.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        public async Task InitializeErrorCapturingAsync(IWebDriver driver)
        {
            if (!_configuration.CaptureJavaScriptErrors)
                return;

            try
            {
                var script = @"
                window.webConnectJsErrors = window.webConnectJsErrors || [];
                
                // Capture JavaScript errors
                window.addEventListener('error', function(e) {
                    window.webConnectJsErrors.push({
                        message: e.message,
                        source: e.filename,
                        line: e.lineno,
                        column: e.colno,
                        stack: e.error ? e.error.stack : null,
                        timestamp: new Date().toISOString(),
                        level: 'error'
                    });
                });

                // Capture unhandled promise rejections
                window.addEventListener('unhandledrejection', function(e) {
                    window.webConnectJsErrors.push({
                        message: e.reason ? e.reason.toString() : 'Unhandled promise rejection',
                        source: null,
                        line: null,
                        column: null,
                        stack: e.reason && e.reason.stack ? e.reason.stack : null,
                        timestamp: new Date().toISOString(),
                        level: 'error'
                    });
                });

                return true;";

                var request = new JavaScriptExecutionRequest
                {
                    Script = script,
                    ReturnResult = true,
                    Description = "Initialize JavaScript error capturing"
                };

                await ExecuteJavaScriptAsync(driver, request);
                _logger.LogDebug("JavaScript error capturing initialized");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize JavaScript error capturing");
            }
        }

        /// <summary>
        /// Executes JavaScript code with error handling and timeout management.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="request">The execution request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The execution result.</returns>
        public async Task<JavaScriptOperationResult> ExecuteJavaScriptAsync(
            IWebDriver driver,
            JavaScriptExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var startTime = DateTime.Now;
            var timeoutSeconds = request.TimeoutSeconds ?? _configuration.DefaultExecutionTimeoutSeconds;

            _logger.LogDebug("Executing JavaScript: {Description}", request.Description ?? "No description");

            var result = new JavaScriptOperationResult();

            try
            {
                var executor = (IJavaScriptExecutor)driver;
                object? returnValue = null;

                var executionTask = Task.Run(() =>
                {
                    try
                    {
                        if (request.IsAsync)
                        {
                            return executor.ExecuteAsyncScript(request.Script, request.Arguments ?? Array.Empty<object>());
                        }
                        else
                        {
                            return executor.ExecuteScript(request.Script, request.Arguments ?? Array.Empty<object>());
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new JavaScriptInteractionException($"JavaScript execution failed: {ex.Message}", ex);
                    }
                }, cancellationToken);

                returnValue = await _timeoutManager.ExecuteWithTimeoutAsync(
                    async (tokenFromManager) => await executionTask,
                    timeoutSeconds * 1000,
                    "ExecuteJavaScript",
                    cancellationToken);

                result.Success = true;
                result.ExecutionResult = JavaScriptExecutionResult.Success;
                result.ReturnValue = returnValue;

                // Capture any JavaScript errors that occurred during execution
                if (_configuration.CaptureJavaScriptErrors)
                {
                    await CaptureJavaScriptErrorsAsync(driver, result);
                }
            }
            catch (TimeoutException)
            {
                result.Success = false;
                result.ExecutionResult = JavaScriptExecutionResult.Timeout;
                result.ErrorMessage = $"JavaScript execution timed out after {timeoutSeconds} seconds";
                _logger.LogWarning("JavaScript execution timed out: {Description}", request.Description);
            }
            catch (JavaScriptInteractionException ex)
            {
                result.Success = false;
                result.ExecutionResult = JavaScriptExecutionResult.Error;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "JavaScript execution failed: {Description}", request.Description);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ExecutionResult = JavaScriptExecutionResult.Error;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Unexpected error during JavaScript execution: {Description}", request.Description);
            }

            result.Duration = DateTime.Now - startTime;
            OnJavaScriptExecuted(request, result);

            return result;
        }

        /// <summary>
        /// Waits for a specific condition to be met using various strategies.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="condition">The wait condition.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The wait operation result.</returns>
        public async Task<JavaScriptOperationResult> WaitForConditionAsync(
            IWebDriver driver,
            WaitCondition condition,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (condition == null) throw new ArgumentNullException(nameof(condition));

            var startTime = DateTime.Now;
            var timeoutSeconds = condition.TimeoutSeconds ?? _configuration.DefaultWaitTimeoutSeconds;

            _logger.LogDebug("Waiting for condition: {Strategy} - {Description}", condition.Strategy, condition.Description);

            try
            {
                var waitScript = GetWaitScript(condition);
                
                var result = await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                {
                    return await PollConditionAsync(driver, waitScript, condition, tokenFromManager);
                }, timeoutSeconds * 1000, $"WaitForCondition_{condition.Strategy}", cancellationToken);

                var duration = DateTime.Now - startTime;

                if (result)
                {
                    _logger.LogDebug("Wait condition satisfied: {Strategy} in {Duration}ms", 
                        condition.Strategy, duration.TotalMilliseconds);
                    
                    return new JavaScriptOperationResult
                    {
                        Success = true,
                        ExecutionResult = JavaScriptExecutionResult.Success,
                        Duration = duration,
                        ReturnValue = true
                    };
                }
                else
                {
                    _logger.LogWarning("Wait condition failed: {Strategy} after {TimeoutSeconds}s", 
                        condition.Strategy, timeoutSeconds);
                    
                    return new JavaScriptOperationResult
                    {
                        Success = false,
                        ExecutionResult = JavaScriptExecutionResult.Timeout,
                        ErrorMessage = $"Wait condition '{condition.Strategy}' timed out after {timeoutSeconds} seconds",
                        Duration = duration
                    };
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Wait condition error: {Strategy}", condition.Strategy);
                
                return new JavaScriptOperationResult
                {
                    Success = false,
                    ExecutionResult = JavaScriptExecutionResult.Error,
                    ErrorMessage = ex.Message,
                    Duration = duration
                };
            }
        }

        /// <summary>
        /// Simulates a user event on an element.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="request">The event simulation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The simulation result.</returns>
        public async Task<JavaScriptOperationResult> SimulateEventAsync(
            IWebDriver driver,
            EventSimulationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var startTime = DateTime.Now;

            _logger.LogDebug("Simulating {EventType} event on element: {Selector}", 
                request.EventType, request.ElementSelector);

            try
            {
                // Add delay if specified
                if (request.DelayMs > 0)
                {
                    await Task.Delay(request.DelayMs, cancellationToken);
                }

                var script = GetEventSimulationScript(request);
                
                var executionRequest = new JavaScriptExecutionRequest
                {
                    Script = script,
                    Arguments = new object[] { request.ElementSelector },
                    ReturnResult = true,
                    Description = $"Simulate {request.EventType} event"
                };

                var result = await ExecuteJavaScriptAsync(driver, executionRequest, cancellationToken);

                if (request.WaitForPropagation)
                {
                    // Small delay to allow event propagation
                    await Task.Delay(100, cancellationToken);
                }

                result.Duration = DateTime.Now - startTime;
                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Event simulation failed: {EventType} on {Selector}", 
                    request.EventType, request.ElementSelector);
                
                return new JavaScriptOperationResult
                {
                    Success = false,
                    ExecutionResult = JavaScriptExecutionResult.Error,
                    ErrorMessage = ex.Message,
                    Duration = duration
                };
            }
        }

        /// <summary>
        /// Interacts with elements within a Shadow DOM.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="request">The Shadow DOM interaction request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The interaction result.</returns>
        public async Task<JavaScriptOperationResult> InteractWithShadowDomAsync(
            IWebDriver driver,
            ShadowDomRequest request,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!_configuration.EnableShadowDomSupport)
            {
                return new JavaScriptOperationResult
                {
                    Success = false,
                    ExecutionResult = JavaScriptExecutionResult.Error,
                    ErrorMessage = "Shadow DOM support is disabled in configuration"
                };
            }

            var startTime = DateTime.Now;

            _logger.LogDebug("Interacting with Shadow DOM element: {HostSelector} -> {ShadowSelector}", 
                request.HostElementSelector, request.ShadowElementSelector);

            try
            {
                var script = GetShadowDomInteractionScript(request);
                
                var executionRequest = new JavaScriptExecutionRequest
                {
                    Script = script,
                    Arguments = new object[] 
                    { 
                        request.HostElementSelector, 
                        request.ShadowElementSelector,
                        request.InteractionType,
                        request.Value ?? string.Empty,
                        request.MaxShadowDepth
                    },
                    ReturnResult = true,
                    Description = $"Shadow DOM {request.InteractionType} interaction"
                };

                var result = await ExecuteJavaScriptAsync(driver, executionRequest, cancellationToken);
                result.Duration = DateTime.Now - startTime;
                
                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Shadow DOM interaction failed: {HostSelector} -> {ShadowSelector}", 
                    request.HostElementSelector, request.ShadowElementSelector);
                
                return new JavaScriptOperationResult
                {
                    Success = false,
                    ExecutionResult = JavaScriptExecutionResult.Error,
                    ErrorMessage = ex.Message,
                    Duration = duration
                };
            }
        }

        /// <summary>
        /// Collects page performance metrics.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The performance metrics.</returns>
        public async Task<PagePerformanceMetrics> GetPerformanceMetricsAsync(
            IWebDriver driver,
            CancellationToken cancellationToken = default)
        {
            if (!_configuration.EnablePerformanceMonitoring)
            {
                return new PagePerformanceMetrics
                {
                    IsStable = true // Assume stable if monitoring is disabled
                };
            }

            try
            {
                var script = @"
                return {
                    domContentLoadedTime: window.performance.timing.domContentLoadedEventEnd - window.performance.timing.navigationStart,
                    loadCompleteTime: window.performance.timing.loadEventEnd - window.performance.timing.navigationStart,
                    activeNetworkRequests: window.performance.getEntriesByType('resource').filter(r => !r.responseEnd).length,
                    heapSize: window.performance.memory ? window.performance.memory.usedJSHeapSize : 0,
                    domNodeCount: document.querySelectorAll('*').length
                };";

                var request = new JavaScriptExecutionRequest
                {
                    Script = script,
                    ReturnResult = true,
                    Description = "Collect performance metrics"
                };

                var result = await ExecuteJavaScriptAsync(driver, request, cancellationToken);
                
                if (result.Success && result.ReturnValue is Dictionary<string, object> metrics)
                {
                    var performanceMetrics = new PagePerformanceMetrics
                    {
                        DomContentLoadedTime = Convert.ToInt64(metrics.GetValueOrDefault("domContentLoadedTime", 0)),
                        LoadCompleteTime = Convert.ToInt64(metrics.GetValueOrDefault("loadCompleteTime", 0)),
                        ActiveNetworkRequests = Convert.ToInt32(metrics.GetValueOrDefault("activeNetworkRequests", 0)),
                        JavaScriptHeapSize = Convert.ToInt64(metrics.GetValueOrDefault("heapSize", 0)),
                        DomNodeCount = Convert.ToInt32(metrics.GetValueOrDefault("domNodeCount", 0)),
                        IsStable = Convert.ToInt32(metrics.GetValueOrDefault("activeNetworkRequests", 0)) == 0
                    };

                    OnPerformanceMetricsCollected(performanceMetrics, "GetPerformanceMetrics");
                    return performanceMetrics;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect performance metrics");
            }

            return new PagePerformanceMetrics { IsStable = false };
        }

        /// <summary>
        /// Waits for the page to be in a stable state (no active network requests).
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="timeoutSeconds">Timeout in seconds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether the page became stable.</returns>
        public async Task<bool> WaitForPageStabilityAsync(
            IWebDriver driver,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var timeout = timeoutSeconds ?? _configuration.DefaultWaitTimeoutSeconds;
            var endTime = DateTime.Now.AddSeconds(timeout);

            _logger.LogDebug("Waiting for page stability with timeout: {TimeoutSeconds}s", timeout);

            while (DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
            {
                var metrics = await GetPerformanceMetricsAsync(driver, cancellationToken);
                
                if (metrics.IsStable && metrics.ActiveNetworkRequests == 0)
                {
                    // Wait a bit more to ensure stability
                    await Task.Delay(_configuration.NetworkIdleTimeoutMs, cancellationToken);
                    
                    // Check again
                    metrics = await GetPerformanceMetricsAsync(driver, cancellationToken);
                    if (metrics.IsStable && metrics.ActiveNetworkRequests == 0)
                    {
                        _logger.LogDebug("Page is stable with {DomNodes} DOM nodes", metrics.DomNodeCount);
                        return true;
                    }
                }

                await Task.Delay(_configuration.PollingIntervalMs, cancellationToken);
            }

            _logger.LogWarning("Page stability wait timed out after {TimeoutSeconds}s", timeout);
            return false;
        }

        /// <summary>
        /// Captures JavaScript errors from the page.
        /// </summary>
        private async Task CaptureJavaScriptErrorsAsync(IWebDriver driver, JavaScriptOperationResult result)
        {
            try
            {
                var script = @"
                var errors = window.webConnectJsErrors || [];
                window.webConnectJsErrors = [];
                return errors;";

                var request = new JavaScriptExecutionRequest
                {
                    Script = script,
                    ReturnResult = true,
                    Description = "Capture JavaScript errors"
                };

                var errorResult = await ExecuteJavaScriptAsync(driver, request);
                
                if (errorResult.Success && errorResult.ReturnValue is IEnumerable<object> errors)
                {
                    foreach (var errorObj in errors)
                    {
                        if (errorObj is Dictionary<string, object> errorDict)
                        {
                            var jsError = new JavaScriptError
                            {
                                Message = errorDict.GetValueOrDefault("message", "Unknown error").ToString() ?? string.Empty,
                                Source = errorDict.GetValueOrDefault("source")?.ToString(),
                                LineNumber = errorDict.ContainsKey("line") ? Convert.ToInt32(errorDict["line"]) : null,
                                ColumnNumber = errorDict.ContainsKey("column") ? Convert.ToInt32(errorDict["column"]) : null,
                                StackTrace = errorDict.GetValueOrDefault("stack")?.ToString(),
                                Level = errorDict.GetValueOrDefault("level", "error").ToString() ?? "error",
                                Timestamp = DateTime.TryParse(errorDict.GetValueOrDefault("timestamp")?.ToString() ?? string.Empty, out var timestamp) 
                                    ? timestamp : DateTime.Now
                            };

                            result.JavaScriptErrors.Add(jsError);
                            _capturedErrors.Add(jsError);
                            OnJavaScriptErrorCaptured(jsError, driver.Url);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture JavaScript errors");
            }
        }

        /// <summary>
        /// Gets the appropriate JavaScript for waiting based on the strategy.
        /// </summary>
        private string GetWaitScript(WaitCondition condition)
        {
            return condition.Strategy switch
            {
                WaitStrategy.DomReady => "return document.readyState === 'complete';",
                
                WaitStrategy.NetworkIdle => @"
                    var entries = window.performance.getEntriesByType('resource');
                    var activeRequests = entries.filter(function(entry) { return !entry.responseEnd; });
                    return activeRequests.length === 0;",
                
                WaitStrategy.ElementPresence => $"return document.querySelector('{condition.ElementSelector}') !== null;",
                
                WaitStrategy.ElementClickable => $@"
                    var element = document.querySelector('{condition.ElementSelector}');
                    return element && element.offsetParent !== null && !element.disabled;",
                
                WaitStrategy.AjaxComplete => @"
                    return (typeof jQuery !== 'undefined' && jQuery.active === 0) || 
                           (typeof $ !== 'undefined' && $.active === 0) ||
                           (typeof jQuery === 'undefined' && typeof $ === 'undefined');",
                
                WaitStrategy.CustomCondition => condition.CustomCondition ?? "return true;",
                
                WaitStrategy.PerformanceStable => @"
                    var entries = window.performance.getEntriesByType('resource');
                    var activeRequests = entries.filter(function(entry) { return !entry.responseEnd; });
                    return activeRequests.length === 0 && document.readyState === 'complete';",
                
                WaitStrategy.AngularReady => @"
                    return (typeof angular !== 'undefined' && 
                            angular.element(document).injector() && 
                            angular.element(document).injector().get('$http').pendingRequests.length === 0);",
                
                WaitStrategy.ReactReady => @"
                    return typeof React !== 'undefined' && 
                           document.querySelector('[data-reactroot]') !== null;",
                
                _ => "return true;"
            };
        }

        /// <summary>
        /// Polls a condition until it's met or timeout occurs.
        /// </summary>
        private async Task<bool> PollConditionAsync(
            IWebDriver driver,
            string script,
            WaitCondition condition,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new JavaScriptExecutionRequest
                    {
                        Script = script,
                        ReturnResult = true,
                        Description = $"Poll condition: {condition.Strategy}"
                    };

                    var result = await ExecuteJavaScriptAsync(driver, request, cancellationToken);
                    
                    if (result.Success && result.ReturnValue is bool boolResult && boolResult)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error polling condition: {Strategy}", condition.Strategy);
                    
                    if (condition.IsCritical)
                        throw;
                }

                await Task.Delay(_configuration.PollingIntervalMs, cancellationToken);
            }

            return false;
        }

        /// <summary>
        /// Gets the JavaScript for event simulation.
        /// </summary>
        private string GetEventSimulationScript(EventSimulationRequest request)
        {
            var baseScript = @"
            var element = document.querySelector(arguments[0]);
            if (!element) throw new Error('Element not found: ' + arguments[0]);
            
            ";

            return request.EventType switch
            {
                EventType.Click => baseScript + "element.click(); return true;",
                
                EventType.DoubleClick => baseScript + @"
                    var event = new MouseEvent('dblclick', { bubbles: true, cancelable: true });
                    element.dispatchEvent(event); return true;",
                
                EventType.Hover => baseScript + @"
                    var event = new MouseEvent('mouseover', { bubbles: true, cancelable: true });
                    element.dispatchEvent(event); return true;",
                
                EventType.Focus => baseScript + "element.focus(); return true;",
                
                EventType.Blur => baseScript + "element.blur(); return true;",
                
                EventType.Input => baseScript + @"
                    element.value = arguments[1] || '';
                    var event = new Event('input', { bubbles: true, cancelable: true });
                    element.dispatchEvent(event); return true;",
                
                EventType.Submit => baseScript + @"
                    var form = element.closest('form');
                    if (form) form.submit();
                    return true;",
                
                _ => baseScript + "return true;"
            };
        }

        /// <summary>
        /// Gets the JavaScript for Shadow DOM interaction.
        /// </summary>
        private string GetShadowDomInteractionScript(ShadowDomRequest request)
        {
            return @"
            function findElementThroughShadow(hostSelector, shadowSelector, maxDepth) {
                var host = document.querySelector(hostSelector);
                if (!host || !host.shadowRoot) return null;
                
                function searchShadowRoot(root, selector, depth) {
                    if (depth >= maxDepth) return null;
                    
                    var element = root.querySelector(selector);
                    if (element) return element;
                    
                    var shadowHosts = root.querySelectorAll('*');
                    for (var i = 0; i < shadowHosts.length; i++) {
                        if (shadowHosts[i].shadowRoot) {
                            element = searchShadowRoot(shadowHosts[i].shadowRoot, selector, depth + 1);
                            if (element) return element;
                        }
                    }
                    return null;
                }
                
                return searchShadowRoot(host.shadowRoot, selector, 0);
            }
            
            var element = findElementThroughShadow(arguments[0], arguments[1], arguments[4]);
            if (!element) throw new Error('Shadow DOM element not found');
            
            var interactionType = arguments[2];
            var value = arguments[3];
            
            switch (interactionType) {
                case 'click':
                    element.click();
                    break;
                case 'input':
                    element.value = value;
                    var event = new Event('input', { bubbles: true });
                    element.dispatchEvent(event);
                    break;
                case 'focus':
                    element.focus();
                    break;
                default:
                    element.click();
                    break;
            }
            
            return true;";
        }

        /// <summary>
        /// Fires the JavaScriptExecuted event.
        /// </summary>
        private void OnJavaScriptExecuted(JavaScriptExecutionRequest request, JavaScriptOperationResult result)
        {
            JavaScriptExecuted?.Invoke(this, new JavaScriptExecutionEventArgs
            {
                Request = request,
                Result = result
            });
        }

        /// <summary>
        /// Fires the JavaScriptErrorCaptured event.
        /// </summary>
        private void OnJavaScriptErrorCaptured(JavaScriptError error, string pageUrl)
        {
            JavaScriptErrorCaptured?.Invoke(this, new JavaScriptErrorEventArgs
            {
                Error = error,
                PageUrl = pageUrl
            });
        }

        /// <summary>
        /// Fires the PerformanceMetricsCollected event.
        /// </summary>
        private void OnPerformanceMetricsCollected(PagePerformanceMetrics metrics, string context)
        {
            PerformanceMetricsCollected?.Invoke(this, new PerformanceMetricsEventArgs
            {
                Metrics = metrics,
                Context = context
            });
        }
    }

    /// <summary>
    /// Exception thrown when JavaScript interaction operations fail.
    /// </summary>
    public class JavaScriptInteractionException : WebConnectException
    {
        public JavaScriptInteractionException() { }

        public JavaScriptInteractionException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="JavaScriptInteractionException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public JavaScriptInteractionException(string message, Exception innerException) : base(message, innerException) { }
    }
} 
