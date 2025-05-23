using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ChromeConnect.Core;
using ChromeConnect.Models;
using ChromeConnect.Exceptions;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Service for handling popup windows and iFrames during login processes.
    /// </summary>
    public class PopupAndIFrameHandler
    {
        private readonly ILogger<PopupAndIFrameHandler> _logger;
        private readonly TimeoutManager _timeoutManager;
        private readonly PopupAndIFrameConfiguration _configuration;
        private readonly ContextTracker _contextTracker;

        /// <summary>
        /// Event fired when a new context (popup or iFrame) is detected.
        /// </summary>
        public event EventHandler<ContextDetectionEventArgs>? ContextDetected;

        /// <summary>
        /// Event fired when switching to a different context.
        /// </summary>
        public event EventHandler<ContextSwitchEventArgs>? ContextSwitched;

        /// <summary>
        /// Initializes a new instance of the <see cref="PopupAndIFrameHandler"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="timeoutManager">The timeout manager.</param>
        /// <param name="configuration">The popup and iFrame configuration.</param>
        public PopupAndIFrameHandler(
            ILogger<PopupAndIFrameHandler> logger,
            TimeoutManager timeoutManager,
            PopupAndIFrameConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeoutManager = timeoutManager ?? throw new ArgumentNullException(nameof(timeoutManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _contextTracker = new ContextTracker();
        }

        /// <summary>
        /// Gets the current context tracker state.
        /// </summary>
        public ContextTracker ContextTracker => _contextTracker;

        /// <summary>
        /// Initializes the context tracking with the main window.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        public void Initialize(IWebDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));

            var mainContext = new ContextInfo
            {
                Id = "main",
                Type = ContextType.MainWindow,
                WindowHandle = driver.CurrentWindowHandle,
                Title = driver.Title,
                Url = driver.Url,
                State = DetectionState.Detected
            };

            _contextTracker.MainWindowContext = mainContext;
            _contextTracker.CurrentContext = mainContext;
            _contextTracker.DetectedContexts.Add(mainContext);

            _logger.LogInformation("Initialized context tracking with main window: {WindowHandle}", driver.CurrentWindowHandle);
        }

        /// <summary>
        /// Detects and waits for a popup window to appear.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="request">The detection request parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The detection result.</returns>
        public async Task<ContextOperationResult> DetectPopupAsync(
            IWebDriver driver,
            ContextDetectionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var startTime = DateTime.Now;
            var timeoutSeconds = request.TimeoutSeconds ?? _configuration.PopupDetectionTimeoutSeconds;

            _logger.LogInformation("Starting popup detection with timeout: {TimeoutSeconds}s", timeoutSeconds);

            try
            {
                var initialHandles = driver.WindowHandles.ToHashSet();
                _logger.LogDebug("Initial window handles: {Handles}", string.Join(", ", initialHandles));

                var result = await _timeoutManager.ExecuteWithTimeoutAsync(async () =>
                {
                    return await PollForPopupAsync(driver, request, initialHandles, cancellationToken);
                }, timeoutSeconds * 1000, "DetectPopup", cancellationToken);

                var duration = DateTime.Now - startTime;

                if (result != null)
                {
                    _logger.LogInformation("Popup detected successfully: {PopupId} in {Duration}ms", 
                        result.Id, duration.TotalMilliseconds);

                    OnContextDetected(result, request);

                    if (request.AutoSwitch)
                    {
                        await SwitchToContextAsync(driver, result, cancellationToken);
                    }

                    return new ContextOperationResult
                    {
                        Success = true,
                        Context = result,
                        Duration = duration
                    };
                }
                else
                {
                    _logger.LogWarning("Popup detection timed out after {TimeoutSeconds}s", timeoutSeconds);
                    return new ContextOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"Popup detection timed out after {timeoutSeconds} seconds",
                        Duration = duration
                    };
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Popup detection failed");
                
                return new ContextOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Duration = duration
                };
            }
        }

        /// <summary>
        /// Detects and waits for an iFrame to appear.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="request">The detection request parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The detection result.</returns>
        public async Task<ContextOperationResult> DetectIFrameAsync(
            IWebDriver driver,
            ContextDetectionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var startTime = DateTime.Now;
            var timeoutSeconds = request.TimeoutSeconds ?? _configuration.IFrameDetectionTimeoutSeconds;

            _logger.LogInformation("Starting iFrame detection with selector: {Selector}, timeout: {TimeoutSeconds}s", 
                request.IFrameSelector, timeoutSeconds);

            try
            {
                var result = await _timeoutManager.ExecuteWithTimeoutAsync(async () =>
                {
                    return await PollForIFrameAsync(driver, request, cancellationToken);
                }, timeoutSeconds * 1000, "DetectIFrame", cancellationToken);

                var duration = DateTime.Now - startTime;

                if (result != null)
                {
                    _logger.LogInformation("iFrame detected successfully: {IFrameId} in {Duration}ms", 
                        result.Id, duration.TotalMilliseconds);

                    OnContextDetected(result, request);

                    if (request.AutoSwitch)
                    {
                        await SwitchToContextAsync(driver, result, cancellationToken);
                    }

                    return new ContextOperationResult
                    {
                        Success = true,
                        Context = result,
                        Duration = duration
                    };
                }
                else
                {
                    _logger.LogWarning("iFrame detection timed out after {TimeoutSeconds}s", timeoutSeconds);
                    return new ContextOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"iFrame detection timed out after {timeoutSeconds} seconds",
                        Duration = duration
                    };
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "iFrame detection failed");
                
                return new ContextOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Duration = duration
                };
            }
        }

        /// <summary>
        /// Switches to a specific context (popup or iFrame).
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="context">The context to switch to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The switch operation result.</returns>
        public async Task<ContextOperationResult> SwitchToContextAsync(
            IWebDriver driver,
            ContextInfo context,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var startTime = DateTime.Now;
            var currentContextId = _contextTracker.CurrentContext?.Id ?? "unknown";

            _logger.LogInformation("Switching from context {FromContext} to {ToContext} ({ContextType})", 
                currentContextId, context.Id, context.Type);

            try
            {
                bool success = false;
                string? errorMessage = null;

                switch (context.Type)
                {
                    case ContextType.MainWindow:
                        driver.SwitchTo().Window(context.WindowHandle!);
                        success = true;
                        break;

                    case ContextType.Popup:
                        driver.SwitchTo().Window(context.WindowHandle!);
                        success = true;
                        break;

                    case ContextType.IFrame:
                    case ContextType.NestedIFrame:
                        success = await SwitchToIFrameAsync(driver, context, cancellationToken);
                        if (!success)
                        {
                            errorMessage = $"Failed to switch to iFrame: {context.IFrameSelector}";
                        }
                        break;

                    default:
                        errorMessage = $"Unknown context type: {context.Type}";
                        break;
                }

                var duration = DateTime.Now - startTime;
                var switchRecord = new ContextSwitchRecord
                {
                    FromContextId = currentContextId,
                    ToContextId = context.Id,
                    Success = success,
                    ErrorMessage = errorMessage
                };

                _contextTracker.SwitchHistory.Add(switchRecord);

                if (success)
                {
                    _contextTracker.CurrentContext = context;
                    _logger.LogInformation("Successfully switched to context: {ContextId}", context.Id);
                }
                else
                {
                    _logger.LogError("Failed to switch to context: {ContextId}, Error: {Error}", context.Id, errorMessage);
                }

                OnContextSwitched(switchRecord);

                return new ContextOperationResult
                {
                    Success = success,
                    Context = context,
                    ErrorMessage = errorMessage,
                    Duration = duration
                };
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                var switchRecord = new ContextSwitchRecord
                {
                    FromContextId = currentContextId,
                    ToContextId = context.Id,
                    Success = false,
                    ErrorMessage = ex.Message
                };

                _contextTracker.SwitchHistory.Add(switchRecord);
                OnContextSwitched(switchRecord);

                _logger.LogError(ex, "Exception during context switch to: {ContextId}", context.Id);

                return new ContextOperationResult
                {
                    Success = false,
                    Context = context,
                    ErrorMessage = ex.Message,
                    Duration = duration
                };
            }
        }

        /// <summary>
        /// Switches back to the main window.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The switch operation result.</returns>
        public async Task<ContextOperationResult> SwitchToMainWindowAsync(
            IWebDriver driver,
            CancellationToken cancellationToken = default)
        {
            if (_contextTracker.MainWindowContext == null)
            {
                throw new InvalidOperationException("Main window context is not initialized. Call Initialize() first.");
            }

            return await SwitchToContextAsync(driver, _contextTracker.MainWindowContext, cancellationToken);
        }

        /// <summary>
        /// Closes a popup window.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="context">The popup context to close.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The close operation result.</returns>
        public async Task<ContextOperationResult> ClosePopupAsync(
            IWebDriver driver,
            ContextInfo context,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (context.Type != ContextType.Popup)
            {
                return new ContextOperationResult
                {
                    Success = false,
                    ErrorMessage = "Context is not a popup window",
                    Context = context
                };
            }

            var startTime = DateTime.Now;

            try
            {
                _logger.LogInformation("Closing popup: {PopupId} ({WindowHandle})", context.Id, context.WindowHandle);

                // Switch to the popup first
                driver.SwitchTo().Window(context.WindowHandle!);
                
                // Close the popup
                driver.Close();

                // Update context state
                context.State = DetectionState.Closed;

                // Switch back to main window if configured
                if (_configuration.AutoSwitchBackToMain)
                {
                    await SwitchToMainWindowAsync(driver, cancellationToken);
                }

                var duration = DateTime.Now - startTime;
                _logger.LogInformation("Popup closed successfully: {PopupId} in {Duration}ms", context.Id, duration.TotalMilliseconds);

                return new ContextOperationResult
                {
                    Success = true,
                    Context = context,
                    Duration = duration
                };
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Failed to close popup: {PopupId}", context.Id);

                return new ContextOperationResult
                {
                    Success = false,
                    Context = context,
                    ErrorMessage = ex.Message,
                    Duration = duration
                };
            }
        }

        /// <summary>
        /// Closes all abandoned popups (popups that are no longer referenced).
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The cleanup operation results.</returns>
        public async Task<List<ContextOperationResult>> CleanupAbandonedPopupsAsync(
            IWebDriver driver,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ContextOperationResult>();
            var currentHandles = driver.WindowHandles.ToHashSet();
            var mainWindowHandle = _contextTracker.MainWindowContext?.WindowHandle;

            _logger.LogInformation("Starting cleanup of abandoned popups. Current handles: {HandleCount}", currentHandles.Count);

            foreach (var handle in currentHandles)
            {
                if (handle == mainWindowHandle)
                    continue; // Skip main window

                // Check if this handle is tracked
                var trackedContext = _contextTracker.DetectedContexts
                    .FirstOrDefault(c => c.WindowHandle == handle && c.Type == ContextType.Popup);

                if (trackedContext == null)
                {
                    // This is an abandoned popup
                    _logger.LogWarning("Found abandoned popup with handle: {WindowHandle}", handle);
                    
                    try
                    {
                        driver.SwitchTo().Window(handle);
                        driver.Close();
                        
                        results.Add(new ContextOperationResult
                        {
                            Success = true,
                            Data = { ["WindowHandle"] = handle, ["Type"] = "AbandonedPopup" }
                        });
                        
                        _logger.LogInformation("Closed abandoned popup: {WindowHandle}", handle);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to close abandoned popup: {WindowHandle}", handle);
                        
                        results.Add(new ContextOperationResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                            Data = { ["WindowHandle"] = handle, ["Type"] = "AbandonedPopup" }
                        });
                    }
                }
            }

            // Switch back to main window
            if (_configuration.AutoSwitchBackToMain && mainWindowHandle != null)
            {
                await SwitchToMainWindowAsync(driver, cancellationToken);
            }

            _logger.LogInformation("Cleanup completed. Processed {PopupCount} abandoned popups", results.Count);
            return results;
        }

        /// <summary>
        /// Polls for popup window appearance.
        /// </summary>
        private async Task<ContextInfo?> PollForPopupAsync(
            IWebDriver driver,
            ContextDetectionRequest request,
            HashSet<string> initialHandles,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var currentHandles = driver.WindowHandles;
                var newHandles = currentHandles.Except(initialHandles).ToList();

                if (newHandles.Any())
                {
                    foreach (var handle in newHandles)
                    {
                        try
                        {
                            // Switch to the new window to get its properties
                            var originalHandle = driver.CurrentWindowHandle;
                            driver.SwitchTo().Window(handle);

                            var title = driver.Title;
                            var url = driver.Url;

                            // Check if this popup matches the criteria
                            if (IsPopupMatch(title, url, request))
                            {
                                var contextId = $"popup_{Guid.NewGuid():N}";
                                var context = new ContextInfo
                                {
                                    Id = contextId,
                                    Type = ContextType.Popup,
                                    WindowHandle = handle,
                                    Title = title,
                                    Url = url,
                                    State = DetectionState.Detected
                                };

                                _contextTracker.DetectedContexts.Add(context);

                                // Switch back to original handle
                                driver.SwitchTo().Window(originalHandle);

                                return context;
                            }

                            // Switch back to original handle
                            driver.SwitchTo().Window(originalHandle);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error checking popup window: {WindowHandle}", handle);
                        }
                    }
                }

                await Task.Delay(_configuration.DetectionPollingIntervalMs, cancellationToken);
            }

            return null;
        }

        /// <summary>
        /// Polls for iFrame appearance.
        /// </summary>
        private async Task<ContextInfo?> PollForIFrameAsync(
            IWebDriver driver,
            ContextDetectionRequest request,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var iFrames = driver.FindElements(By.TagName("iframe"));
                    
                    for (int i = 0; i < iFrames.Count; i++)
                    {
                        var iframe = iFrames[i];
                        
                        // Check if selector is specified and matches
                        if (!string.IsNullOrEmpty(request.IFrameSelector))
                        {
                            try
                            {
                                var specificFrame = driver.FindElement(By.CssSelector(request.IFrameSelector));
                                if (!iframe.Equals(specificFrame))
                                    continue;
                            }
                            catch (NoSuchElementException)
                            {
                                continue;
                            }
                        }

                        // Try to get iFrame properties
                        var src = iframe.GetAttribute("src");
                        var name = iframe.GetAttribute("name") ?? iframe.GetAttribute("id") ?? $"iframe_{i}";

                        if (IsIFrameMatch(name, src, request))
                        {
                            var contextId = $"iframe_{Guid.NewGuid():N}";
                            var context = new ContextInfo
                            {
                                Id = contextId,
                                Type = ContextType.IFrame,
                                IFrameSelector = request.IFrameSelector ?? $"iframe:nth-of-type({i + 1})",
                                IFrameIndex = i,
                                Title = name,
                                Url = src,
                                ParentContextId = request.ParentContextId,
                                State = DetectionState.Detected
                            };

                            _contextTracker.DetectedContexts.Add(context);
                            return context;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during iFrame detection polling");
                }

                await Task.Delay(_configuration.DetectionPollingIntervalMs, cancellationToken);
            }

            return null;
        }

        /// <summary>
        /// Switches to an iFrame context.
        /// </summary>
        private async Task<bool> SwitchToIFrameAsync(IWebDriver driver, ContextInfo context, CancellationToken cancellationToken)
        {
            try
            {
                // First, ensure we're in the right parent context
                if (!string.IsNullOrEmpty(context.ParentContextId))
                {
                    var parentContext = _contextTracker.DetectedContexts
                        .FirstOrDefault(c => c.Id == context.ParentContextId);
                    
                    if (parentContext != null)
                    {
                        await SwitchToContextAsync(driver, parentContext, cancellationToken);
                    }
                }

                // Switch to the iFrame
                if (!string.IsNullOrEmpty(context.IFrameSelector))
                {
                    var frameElement = driver.FindElement(By.CssSelector(context.IFrameSelector));
                    driver.SwitchTo().Frame(frameElement);
                }
                else if (context.IFrameIndex.HasValue)
                {
                    driver.SwitchTo().Frame(context.IFrameIndex.Value);
                }
                else
                {
                    _logger.LogError("iFrame context has no selector or index: {ContextId}", context.Id);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch to iFrame: {ContextId}", context.Id);
                return false;
            }
        }

        /// <summary>
        /// Checks if a popup matches the detection criteria.
        /// </summary>
        private bool IsPopupMatch(string title, string url, ContextDetectionRequest request)
        {
            if (!string.IsNullOrEmpty(request.ExpectedTitlePattern))
            {
                var titleRegex = new Regex(request.ExpectedTitlePattern, RegexOptions.IgnoreCase);
                if (!titleRegex.IsMatch(title ?? string.Empty))
                    return false;
            }

            if (!string.IsNullOrEmpty(request.ExpectedUrlPattern))
            {
                var urlRegex = new Regex(request.ExpectedUrlPattern, RegexOptions.IgnoreCase);
                if (!urlRegex.IsMatch(url ?? string.Empty))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if an iFrame matches the detection criteria.
        /// </summary>
        private bool IsIFrameMatch(string name, string? src, ContextDetectionRequest request)
        {
            if (!string.IsNullOrEmpty(request.ExpectedTitlePattern))
            {
                var titleRegex = new Regex(request.ExpectedTitlePattern, RegexOptions.IgnoreCase);
                if (!titleRegex.IsMatch(name ?? string.Empty))
                    return false;
            }

            if (!string.IsNullOrEmpty(request.ExpectedUrlPattern))
            {
                var urlRegex = new Regex(request.ExpectedUrlPattern, RegexOptions.IgnoreCase);
                if (!urlRegex.IsMatch(src ?? string.Empty))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Fires the ContextDetected event.
        /// </summary>
        private void OnContextDetected(ContextInfo context, ContextDetectionRequest request)
        {
            ContextDetected?.Invoke(this, new ContextDetectionEventArgs
            {
                Context = context,
                Request = request
            });
        }

        /// <summary>
        /// Fires the ContextSwitched event.
        /// </summary>
        private void OnContextSwitched(ContextSwitchRecord switchRecord)
        {
            ContextSwitched?.Invoke(this, new ContextSwitchEventArgs
            {
                SwitchRecord = switchRecord,
                Tracker = _contextTracker
            });
        }
    }

    /// <summary>
    /// Exception thrown when popup or iFrame operations fail.
    /// </summary>
    public class PopupAndIFrameException : ChromeConnectException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PopupAndIFrameException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public PopupAndIFrameException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PopupAndIFrameException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public PopupAndIFrameException(string message, Exception innerException) : base(message, innerException) { }
    }
} 