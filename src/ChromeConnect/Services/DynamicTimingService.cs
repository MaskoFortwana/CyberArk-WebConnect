using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Service for handling dynamic timing and element waiting strategies
    /// Provides intelligent waiting mechanisms for dynamic form elements
    /// </summary>
    public class DynamicTimingService
    {
        private readonly ILogger<DynamicTimingService> _logger;
        private readonly WebDriverWait _defaultWait;

        public DynamicTimingService(ILogger<DynamicTimingService> logger, IWebDriver driver)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Waits for elements to appear after a trigger action with intelligent timing
        /// </summary>
        public async Task<List<IWebElement>> WaitForElementsAfterActionAsync(
            IWebDriver driver, 
            Func<Task> triggerAction, 
            ElementWaitCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"Waiting for elements after action with criteria: {criteria.Description}");

            var foundElements = new List<IWebElement>();
            var startTime = DateTime.UtcNow;

            try
            {
                // Take snapshot of current elements before action
                var beforeElements = GetCurrentElements(driver, criteria.Selectors);
                _logger.LogDebug($"Elements before action: {beforeElements.Count}");

                // Execute the trigger action
                await triggerAction();

                // Use adaptive waiting strategy
                var waitStrategy = DetermineWaitStrategy(criteria);
                _logger.LogDebug($"Using wait strategy: {waitStrategy}");

                switch (waitStrategy)
                {
                    case TimingWaitStrategy.Progressive:
                        foundElements = await WaitProgressivelyAsync(driver, criteria, beforeElements, cancellationToken);
                        break;
                    
                    case TimingWaitStrategy.MutationObserver:
                        foundElements = await WaitWithMutationObserverAsync(driver, criteria, cancellationToken);
                        break;
                    
                    case TimingWaitStrategy.Polling:
                        foundElements = await WaitWithPollingAsync(driver, criteria, beforeElements, cancellationToken);
                        break;
                    
                    case TimingWaitStrategy.EventBased:
                        foundElements = await WaitForEventsAsync(driver, criteria, cancellationToken);
                        break;
                    
                    default:
                        foundElements = await WaitWithPollingAsync(driver, criteria, beforeElements, cancellationToken);
                        break;
                }

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation($"Element waiting completed in {duration.TotalMilliseconds}ms, found {foundElements.Count} elements");

                return foundElements;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Element waiting was cancelled");
                return foundElements;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during element waiting");
                return foundElements;
            }
        }

        /// <summary>
        /// Waits for form completion with intelligent detection of when rendering is finished
        /// </summary>
        public async Task<bool> WaitForFormCompletionAsync(
            IWebDriver driver,
            FormCompletionCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Waiting for form completion");

            var startTime = DateTime.UtcNow;
            var lastChangeTime = startTime;
            var stableCount = 0;
            var requiredStableChecks = 3;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var currentTime = DateTime.UtcNow;
                    
                    // Check if maximum wait time exceeded
                    if (currentTime - startTime > criteria.MaxWaitTime)
                    {
                        _logger.LogWarning($"Form completion wait timeout after {criteria.MaxWaitTime.TotalSeconds}s");
                        break;
                    }

                    // Check form completion criteria
                    var isComplete = await CheckFormCompletionAsync(driver, criteria);
                    
                    if (isComplete)
                    {
                        stableCount++;
                        _logger.LogDebug($"Form appears complete, stable check {stableCount}/{requiredStableChecks}");
                        
                        if (stableCount >= requiredStableChecks)
                        {
                            _logger.LogInformation($"Form completion confirmed after {(currentTime - startTime).TotalMilliseconds}ms");
                            return true;
                        }
                    }
                    else
                    {
                        if (stableCount > 0)
                        {
                            _logger.LogDebug("Form completion unstable, resetting counter");
                        }
                        stableCount = 0;
                        lastChangeTime = currentTime;
                    }

                    // Adaptive delay based on how long we've been waiting
                    var waitDuration = currentTime - startTime;
                    var delay = CalculateAdaptiveDelay(waitDuration, criteria);
                    await Task.Delay(delay, cancellationToken);
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Form completion wait was cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for form completion");
                return false;
            }
        }

        /// <summary>
        /// Waits for DOM stability after dynamic changes
        /// </summary>
        public async Task<bool> WaitForDOMStabilityAsync(
            IWebDriver driver,
            TimeSpan stabilityDuration = default,
            TimeSpan maxWaitTime = default,
            CancellationToken cancellationToken = default)
        {
            if (stabilityDuration == default) stabilityDuration = TimeSpan.FromMilliseconds(500);
            if (maxWaitTime == default) maxWaitTime = TimeSpan.FromSeconds(10);

            _logger.LogDebug($"Waiting for DOM stability for {stabilityDuration.TotalMilliseconds}ms");

            var jsExecutor = (IJavaScriptExecutor)driver;
            var startTime = DateTime.UtcNow;
            var lastMutationTime = startTime;

            try
            {
                // Set up mutation observer
                var observerScript = @"
                    window.domStabilityData = {
                        lastMutationTime: Date.now(),
                        observer: null
                    };
                    
                    if (window.domStabilityData.observer) {
                        window.domStabilityData.observer.disconnect();
                    }
                    
                    window.domStabilityData.observer = new MutationObserver(function(mutations) {
                        if (mutations.length > 0) {
                            window.domStabilityData.lastMutationTime = Date.now();
                        }
                    });
                    
                    window.domStabilityData.observer.observe(document.body, {
                        childList: true,
                        subtree: true,
                        attributes: true,
                        attributeOldValue: false,
                        characterData: false
                    });
                ";

                jsExecutor.ExecuteScript(observerScript);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var currentTime = DateTime.UtcNow;
                    
                    if (currentTime - startTime > maxWaitTime)
                    {
                        _logger.LogWarning($"DOM stability wait timeout after {maxWaitTime.TotalSeconds}s");
                        break;
                    }

                    // Get last mutation time from JavaScript
                    var lastMutationTimeMs = (long)jsExecutor.ExecuteScript("return window.domStabilityData ? window.domStabilityData.lastMutationTime : Date.now();");
                    var lastMutationTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(lastMutationTimeMs).UtcDateTime;

                    var timeSinceLastMutation = currentTime - lastMutationTimeUtc;
                    
                    if (timeSinceLastMutation >= stabilityDuration)
                    {
                        _logger.LogDebug($"DOM stable for {timeSinceLastMutation.TotalMilliseconds}ms");
                        
                        // Clean up observer
                        jsExecutor.ExecuteScript("if (window.domStabilityData && window.domStabilityData.observer) { window.domStabilityData.observer.disconnect(); }");
                        
                        return true;
                    }

                    await Task.Delay(100, cancellationToken);
                }

                // Clean up observer
                jsExecutor.ExecuteScript("if (window.domStabilityData && window.domStabilityData.observer) { window.domStabilityData.observer.disconnect(); }");
                
                return false;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("DOM stability wait was cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for DOM stability");
                return false;
            }
        }

        /// <summary>
        /// Intelligent retry mechanism with exponential backoff
        /// </summary>
        public async Task<T> ExecuteWithIntelligentRetryAsync<T>(
            Func<Task<T>> operation,
            RetryConfiguration config = null,
            CancellationToken cancellationToken = default)
        {
            config ??= new RetryConfiguration();
            
            var attempt = 0;
            var delay = config.InitialDelay;

            while (attempt < config.MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    attempt++;
                    _logger.LogDebug($"Executing operation attempt {attempt}/{config.MaxAttempts}");
                    
                    var result = await operation();
                    
                    if (attempt > 1)
                    {
                        _logger.LogInformation($"Operation succeeded on attempt {attempt}");
                    }
                    
                    return result;
                }
                catch (Exception ex) when (attempt < config.MaxAttempts && config.ShouldRetry(ex))
                {
                    _logger.LogWarning($"Operation failed on attempt {attempt}, retrying in {delay.TotalMilliseconds}ms: {ex.Message}");
                    
                    await Task.Delay(delay, cancellationToken);
                    
                    // Exponential backoff with jitter
                    delay = TimeSpan.FromMilliseconds(
                        Math.Min(delay.TotalMilliseconds * config.BackoffMultiplier + Random.Shared.Next(0, 100),
                                config.MaxDelay.TotalMilliseconds));
                }
            }

            throw new InvalidOperationException($"Operation failed after {config.MaxAttempts} attempts");
        }

        #region Private Methods

        private List<IWebElement> GetCurrentElements(IWebDriver driver, string[] selectors)
        {
            var elements = new List<IWebElement>();
            
            foreach (var selector in selectors)
            {
                try
                {
                    var found = driver.FindElements(By.CssSelector(selector))
                        .Where(e => e.Displayed && e.Enabled)
                        .ToList();
                    elements.AddRange(found);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error finding elements with selector '{selector}': {ex.Message}");
                }
            }
            
            return elements;
        }

        private TimingWaitStrategy DetermineWaitStrategy(ElementWaitCriteria criteria)
        {
            // Choose strategy based on criteria and context
            if (criteria.ExpectedElementCount > 5)
                return TimingWaitStrategy.MutationObserver;
            
            if (criteria.IsProgressiveForm)
                return TimingWaitStrategy.Progressive;
            
            if (criteria.HasEventTriggers)
                return TimingWaitStrategy.EventBased;
            
            return TimingWaitStrategy.Polling;
        }

        private async Task<List<IWebElement>> WaitProgressivelyAsync(
            IWebDriver driver, 
            ElementWaitCriteria criteria, 
            List<IWebElement> beforeElements,
            CancellationToken cancellationToken)
        {
            var foundElements = new List<IWebElement>();
            var checkIntervals = new[] { 100, 250, 500, 1000, 1500 }; // Progressive intervals
            
            foreach (var interval in checkIntervals)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                await Task.Delay(interval, cancellationToken);
                
                var currentElements = GetCurrentElements(driver, criteria.Selectors);
                var newElements = currentElements.Except(beforeElements).ToList();
                
                if (newElements.Any())
                {
                    foundElements.AddRange(newElements);
                    _logger.LogDebug($"Found {newElements.Count} new elements after {interval}ms");
                    
                    if (foundElements.Count >= criteria.ExpectedElementCount)
                    {
                        break;
                    }
                }
            }
            
            return foundElements;
        }

        private async Task<List<IWebElement>> WaitWithMutationObserverAsync(
            IWebDriver driver,
            ElementWaitCriteria criteria,
            CancellationToken cancellationToken)
        {
            var jsExecutor = (IJavaScriptExecutor)driver;
            var foundElements = new List<IWebElement>();
            
            // Set up mutation observer
            var observerScript = @"
                window.elementWaitData = {
                    newElements: [],
                    observer: null
                };
                
                window.elementWaitData.observer = new MutationObserver(function(mutations) {
                    mutations.forEach(function(mutation) {
                        if (mutation.type === 'childList') {
                            mutation.addedNodes.forEach(function(node) {
                                if (node.nodeType === 1) {
                                    window.elementWaitData.newElements.push({
                                        tagName: node.tagName,
                                        id: node.id || '',
                                        className: node.className || ''
                                    });
                                }
                            });
                        }
                    });
                });
                
                window.elementWaitData.observer.observe(document.body, {
                    childList: true,
                    subtree: true
                });
            ";
            
            jsExecutor.ExecuteScript(observerScript);
            
            try
            {
                var startTime = DateTime.UtcNow;
                
                while (!cancellationToken.IsCancellationRequested && 
                       DateTime.UtcNow - startTime < criteria.MaxWaitTime)
                {
                    await Task.Delay(200, cancellationToken);
                    
                    var currentElements = GetCurrentElements(driver, criteria.Selectors);
                    foundElements = currentElements.ToList();
                    
                    if (foundElements.Count >= criteria.ExpectedElementCount)
                    {
                        break;
                    }
                }
            }
            finally
            {
                // Clean up observer
                jsExecutor.ExecuteScript("if (window.elementWaitData && window.elementWaitData.observer) { window.elementWaitData.observer.disconnect(); }");
            }
            
            return foundElements;
        }

        private async Task<List<IWebElement>> WaitWithPollingAsync(
            IWebDriver driver,
            ElementWaitCriteria criteria,
            List<IWebElement> beforeElements,
            CancellationToken cancellationToken)
        {
            var foundElements = new List<IWebElement>();
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromMilliseconds(250);
            
            while (!cancellationToken.IsCancellationRequested && 
                   DateTime.UtcNow - startTime < criteria.MaxWaitTime)
            {
                var currentElements = GetCurrentElements(driver, criteria.Selectors);
                var newElements = currentElements.Except(beforeElements).ToList();
                
                if (newElements.Any())
                {
                    foundElements.AddRange(newElements);
                    
                    if (foundElements.Count >= criteria.ExpectedElementCount)
                    {
                        break;
                    }
                }
                
                await Task.Delay(pollInterval, cancellationToken);
            }
            
            return foundElements;
        }

        private async Task<List<IWebElement>> WaitForEventsAsync(
            IWebDriver driver,
            ElementWaitCriteria criteria,
            CancellationToken cancellationToken)
        {
            // Event-based waiting implementation
            // This would listen for specific DOM events that indicate element appearance
            var foundElements = new List<IWebElement>();
            var startTime = DateTime.UtcNow;
            
            while (!cancellationToken.IsCancellationRequested && 
                   DateTime.UtcNow - startTime < criteria.MaxWaitTime)
            {
                var currentElements = GetCurrentElements(driver, criteria.Selectors);
                foundElements = currentElements.ToList();
                
                if (foundElements.Count >= criteria.ExpectedElementCount)
                {
                    break;
                }
                
                await Task.Delay(100, cancellationToken);
            }
            
            return foundElements;
        }

        private async Task<bool> CheckFormCompletionAsync(IWebDriver driver, FormCompletionCriteria criteria)
        {
            try
            {
                // Check if all required elements are present
                foreach (var selector in criteria.RequiredElementSelectors)
                {
                    var elements = driver.FindElements(By.CssSelector(selector))
                        .Where(e => e.Displayed && e.Enabled)
                        .ToList();
                    
                    if (!elements.Any())
                    {
                        return false;
                    }
                }

                // Check if any blocking elements are gone
                foreach (var selector in criteria.BlockingElementSelectors)
                {
                    var elements = driver.FindElements(By.CssSelector(selector))
                        .Where(e => e.Displayed)
                        .ToList();
                    
                    if (elements.Any())
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error checking form completion: {ex.Message}");
                return false;
            }
        }

        private TimeSpan CalculateAdaptiveDelay(TimeSpan waitDuration, FormCompletionCriteria criteria)
        {
            // Start with shorter delays, increase over time
            if (waitDuration < TimeSpan.FromSeconds(2))
                return TimeSpan.FromMilliseconds(100);
            
            if (waitDuration < TimeSpan.FromSeconds(5))
                return TimeSpan.FromMilliseconds(250);
            
            return TimeSpan.FromMilliseconds(500);
        }

        #endregion
    }

    #region Supporting Classes

    public class ElementWaitCriteria
    {
        public string[] Selectors { get; set; } = Array.Empty<string>();
        public int ExpectedElementCount { get; set; } = 1;
        public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(10);
        public bool IsProgressiveForm { get; set; }
        public bool HasEventTriggers { get; set; }
        public string Description { get; set; } = "Element wait";
    }

    public class FormCompletionCriteria
    {
        public string[] RequiredElementSelectors { get; set; } = Array.Empty<string>();
        public string[] BlockingElementSelectors { get; set; } = Array.Empty<string>();
        public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(15);
    }

    public class RetryConfiguration
    {
        public int MaxAttempts { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);
        public double BackoffMultiplier { get; set; } = 2.0;
        public Func<Exception, bool> ShouldRetry { get; set; } = ex => 
            ex is WebDriverTimeoutException || 
            ex is NoSuchElementException || 
            ex is StaleElementReferenceException;
    }

    public enum TimingWaitStrategy
    {
        Progressive,
        MutationObserver,
        Polling,
        EventBased
    }

    #endregion
} 