using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using WebConnect.Core;
using WebConnect.Models;
using WebConnect.Exceptions;

namespace WebConnect.Services
{
    /// <summary>
    /// Navigator service for handling multi-step login processes with state machine logic.
    /// </summary>
    public class MultiStepLoginNavigator
    {
        private readonly ILogger<MultiStepLoginNavigator> _logger;
        private readonly IScreenshotCapture _screenshotCapture;
        private readonly TimeoutManager _timeoutManager;
        private readonly MultiStepLoginConfiguration _configuration;

        /// <summary>
        /// Event fired when a step execution starts.
        /// </summary>
        public event EventHandler<StepExecutionEventArgs>? StepStarted;

        /// <summary>
        /// Event fired when a step execution completes.
        /// </summary>
        public event EventHandler<StepExecutionEventArgs>? StepCompleted;

        /// <summary>
        /// Event fired when the flow state changes.
        /// </summary>
        public event EventHandler<FlowExecutionEventArgs>? FlowStateChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiStepLoginNavigator"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="screenshotCapture">The screenshot capture service.</param>
        /// <param name="timeoutManager">The timeout manager.</param>
        /// <param name="configuration">The multi-step login configuration.</param>
        public MultiStepLoginNavigator(
            ILogger<MultiStepLoginNavigator> logger,
            IScreenshotCapture screenshotCapture,
            TimeoutManager timeoutManager,
            MultiStepLoginConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _screenshotCapture = screenshotCapture ?? throw new ArgumentNullException(nameof(screenshotCapture));
            _timeoutManager = timeoutManager ?? throw new ArgumentNullException(nameof(timeoutManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Executes a multi-step login flow.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="flow">The login flow to execute.</param>
        /// <param name="credentials">The credentials to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The execution result.</returns>
        public async Task<LoginFlowExecution> ExecuteFlowAsync(
            IWebDriver driver, 
            LoginFlow flow, 
            CommandLineOptions credentials,
            CancellationToken cancellationToken = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));

            var execution = new LoginFlowExecution
            {
                Flow = flow,
                State = LoginFlowState.InProgress,
                StartTime = DateTime.Now
            };

            _logger.LogInformation("Starting multi-step login flow: {FlowName} with {StepCount} steps", 
                flow.Name, flow.Steps.Count);

            try
            {
                OnFlowStateChanged(execution, "Flow execution started");

                // Validate the flow is applicable to current URL
                if (!IsFlowApplicable(driver, flow))
                {
                    throw new InvalidOperationException($"Flow '{flow.Name}' is not applicable to current URL: {driver.Url}");
                }

                // Execute each step in sequence
                for (int i = 0; i < flow.Steps.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    execution.CurrentStepIndex = i;
                    var step = flow.Steps[i];

                    _logger.LogInformation("Executing step {StepIndex}/{TotalSteps}: {StepName} ({StepType})", 
                        i + 1, flow.Steps.Count, step.Name, step.StepType);

                    // Check if step should be executed based on condition
                    if (!ShouldExecuteStep(driver, step, execution))
                    {
                        _logger.LogInformation("Skipping step {StepName} due to condition: {Condition}", 
                            step.Name, step.Condition);
                        continue;
                    }

                    var stepResult = await ExecuteStepWithRetryAsync(driver, step, credentials, execution, cancellationToken);
                    execution.CompletedSteps.Add(stepResult);

                    if (!stepResult.Success)
                    {
                        _logger.LogError("Step {StepName} failed: {ErrorMessage}", step.Name, stepResult.ErrorMessage);
                        
                        if (_configuration.StopOnFirstFailure && !flow.ContinueOnStepFailure)
                        {
                            execution.State = LoginFlowState.Failed;
                            execution.Errors.Add($"Step '{step.Name}' failed: {stepResult.ErrorMessage}");
                            break;
                        }
                    }

                    // Validate step completion
                    if (!await ValidateStepCompletionAsync(driver, step, cancellationToken))
                    {
                        _logger.LogWarning("Step validation failed for: {StepName}", step.Name);
                        if (_configuration.StopOnFirstFailure)
                        {
                            execution.State = LoginFlowState.Failed;
                            execution.Errors.Add($"Step validation failed for: {step.Name}");
                            break;
                        }
                    }
                }

                // Determine final state
                if (execution.State == LoginFlowState.InProgress)
                {
                    bool allStepsSuccessful = execution.CompletedSteps.All(s => s.Success);
                    execution.State = allStepsSuccessful ? LoginFlowState.Completed : LoginFlowState.Failed;
                }

                execution.EndTime = DateTime.Now;
                
                _logger.LogInformation("Multi-step login flow completed. State: {State}, Duration: {Duration}ms", 
                    execution.State, execution.ExecutionTime.TotalMilliseconds);

                OnFlowStateChanged(execution, $"Flow execution completed with state: {execution.State}");

                return execution;
            }
            catch (OperationCanceledException)
            {
                execution.State = LoginFlowState.Cancelled;
                execution.EndTime = DateTime.Now;
                _logger.LogWarning("Multi-step login flow was cancelled");
                OnFlowStateChanged(execution, "Flow execution was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                execution.State = LoginFlowState.Failed;
                execution.EndTime = DateTime.Now;
                execution.Errors.Add(ex.Message);
                
                _logger.LogError(ex, "Multi-step login flow failed with exception");
                OnFlowStateChanged(execution, $"Flow execution failed: {ex.Message}");

                // Attempt recovery if configured
                if (flow.RecoverySteps.Any())
                {
                    _logger.LogInformation("Attempting recovery with {RecoveryStepCount} recovery steps", flow.RecoverySteps.Count);
                    await AttemptRecoveryAsync(driver, flow, execution, credentials, cancellationToken);
                }

                throw new LoginFlowException($"Multi-step login flow '{flow.Name}' failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Executes a single step with retry logic.
        /// </summary>
        private async Task<StepExecutionResult> ExecuteStepWithRetryAsync(
            IWebDriver driver,
            LoginStep step,
            CommandLineOptions credentials,
            LoginFlowExecution execution,
            CancellationToken cancellationToken)
        {
            var result = new StepExecutionResult
            {
                Step = step,
                StartTime = DateTime.Now
            };

            OnStepStarted(execution, step);

            int maxRetries = step.CanRetry ? step.MaxRetries : 0;
            
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (attempt > 0)
                    {
                        _logger.LogInformation("Retrying step {StepName}, attempt {Attempt}/{MaxRetries}", 
                            step.Name, attempt, maxRetries);
                        
                        // Wait before retry
                        await Task.Delay(_configuration.RetryDelayMs, cancellationToken);
                    }

                    result.RetryAttempts = attempt;

                    // Execute the specific step type
                    bool stepSuccess = await ExecuteStepActionAsync(driver, step, credentials, cancellationToken);
                    
                    if (stepSuccess)
                    {
                        result.Success = true;
                        result.EndTime = DateTime.Now;
                        _logger.LogInformation("Step {StepName} completed successfully on attempt {Attempt}", 
                            step.Name, attempt + 1);
                        break;
                    }
                    else if (attempt >= maxRetries)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Step failed after {attempt + 1} attempts";
                        result.EndTime = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Step {StepName} failed on attempt {Attempt}: {ErrorMessage}", 
                        step.Name, attempt + 1, ex.Message);
                    
                    if (attempt >= maxRetries)
                    {
                        result.Success = false;
                        result.ErrorMessage = ex.Message;
                        result.EndTime = DateTime.Now;
                        
                        if (_configuration.CaptureScreenshotsOnFailure)
                        {
                            _screenshotCapture.CaptureScreenshot(driver, $"StepFailed_{step.Id}_{attempt}");
                        }
                    }
                }
            }

            OnStepCompleted(execution, step, result);
            return result;
        }

        /// <summary>
        /// Executes the action for a specific step type.
        /// </summary>
        private async Task<bool> ExecuteStepActionAsync(
            IWebDriver driver,
            LoginStep step,
            CommandLineOptions credentials,
            CancellationToken cancellationToken)
        {
            int timeoutMs = step.TimeoutSeconds * 1000;

            switch (step.StepType)
            {
                case LoginStepType.EnterCredentials:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await EnterCredentialsStepAsync(driver, step, credentials),
                        timeoutMs, $"Step_{step.Name}_EnterCredentials", cancellationToken);

                case LoginStepType.ClickElement:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await ClickElementStepAsync(driver, step),
                        timeoutMs, $"Step_{step.Name}_ClickElement", cancellationToken);

                case LoginStepType.WaitForNavigation:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await WaitForNavigationStepAsync(driver, step),
                        timeoutMs, $"Step_{step.Name}_WaitForNavigation", cancellationToken);

                case LoginStepType.WaitForElement:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await WaitForElementStepAsync(driver, step),
                        timeoutMs, $"Step_{step.Name}_WaitForElement", cancellationToken);

                case LoginStepType.ValidateElements:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await ValidateElementsStepAsync(driver, step),
                        timeoutMs, $"Step_{step.Name}_ValidateElements", cancellationToken);

                case LoginStepType.ExecuteScript:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await ExecuteScriptStepAsync(driver, step),
                        timeoutMs, $"Step_{step.Name}_ExecuteScript", cancellationToken);

                case LoginStepType.EnterText:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await EnterTextStepAsync(driver, step),
                        timeoutMs, $"Step_{step.Name}_EnterText", cancellationToken);

                case LoginStepType.SelectOption:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await SelectOptionStepAsync(driver, step),
                        timeoutMs, $"Step_{step.Name}_SelectOption", cancellationToken);

                case LoginStepType.CustomValidation:
                    return await _timeoutManager.ExecuteWithTimeoutAsync(async (tokenFromManager) =>
                        await CustomValidationStepAsync(driver, step),
                        timeoutMs, $"Step_{step.Name}_CustomValidation", cancellationToken);

                default:
                    _logger.LogWarning("Unsupported step type: {StepType}", step.StepType);
                    return false;
            }
        }

        /// <summary>
        /// Implements the EnterCredentials step action.
        /// </summary>
        private async Task<bool> EnterCredentialsStepAsync(IWebDriver driver, LoginStep step, CommandLineOptions credentials)
        {
            _logger.LogDebug("Executing EnterCredentials step: {StepName}", step.Name);

            try
            {
                // This is a simplified implementation - in practice, you might want to use the existing
                // CredentialManager service or adapt its logic here
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(step.TimeoutSeconds));

                // Find username field
                var usernameField = wait.Until(d => d.FindElement(By.CssSelector("input[type='text'], input[type='email'], input[name*='user'], input[id*='user']")));
                usernameField.Clear();
                usernameField.SendKeys(credentials.Username);

                // Find password field
                var passwordField = wait.Until(d => d.FindElement(By.CssSelector("input[type='password']")));
                passwordField.Clear();
                passwordField.SendKeys(credentials.Password);

                // Find domain field if needed and specified
                if (!string.IsNullOrEmpty(credentials.Domain))
                {
                    try
                    {
                        var domainField = driver.FindElement(By.CssSelector("input[name*='domain'], input[id*='domain']"));
                        domainField.Clear();
                        domainField.SendKeys(credentials.Domain);
                    }
                    catch (NoSuchElementException)
                    {
                        _logger.LogDebug("Domain field not found, skipping domain entry");
                    }
                }

                await Task.Delay(500); // Brief pause for UI to update
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enter credentials in step: {StepName}", step.Name);
                return false;
            }
        }

        /// <summary>
        /// Implements the ClickElement step action.
        /// </summary>
        private async Task<bool> ClickElementStepAsync(IWebDriver driver, LoginStep step)
        {
            _logger.LogDebug("Executing ClickElement step: {StepName}", step.Name);

            if (string.IsNullOrEmpty(step.ElementSelector))
            {
                _logger.LogError("ElementSelector is required for ClickElement step: {StepName}", step.Name);
                return false;
            }

            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(step.TimeoutSeconds));
                var element = wait.Until(d => d.FindElement(By.CssSelector(step.ElementSelector)));
                
                // Scroll element into view
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                await Task.Delay(200);

                element.Click();
                await Task.Delay(500); // Brief pause after click
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to click element '{ElementSelector}' in step: {StepName}", 
                    step.ElementSelector, step.Name);
                return false;
            }
        }

        /// <summary>
        /// Implements the WaitForNavigation step action.
        /// </summary>
        private async Task<bool> WaitForNavigationStepAsync(IWebDriver driver, LoginStep step)
        {
            _logger.LogDebug("Executing WaitForNavigation step: {StepName}", step.Name);

            try
            {
                string initialUrl = driver.Url;
                var timeout = DateTime.Now.AddSeconds(step.TimeoutSeconds);

                while (DateTime.Now < timeout)
                {
                    await Task.Delay(100);
                    
                    if (driver.Url != initialUrl)
                    {
                        _logger.LogDebug("Navigation detected from {InitialUrl} to {CurrentUrl}", initialUrl, driver.Url);
                        return true;
                    }
                }

                _logger.LogWarning("Navigation timeout - URL did not change from: {InitialUrl}", initialUrl);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to wait for navigation in step: {StepName}", step.Name);
                return false;
            }
        }

        /// <summary>
        /// Implements the WaitForElement step action.
        /// </summary>
        private async Task<bool> WaitForElementStepAsync(IWebDriver driver, LoginStep step)
        {
            _logger.LogDebug("Executing WaitForElement step: {StepName}", step.Name);

            if (string.IsNullOrEmpty(step.ElementSelector))
            {
                _logger.LogError("ElementSelector is required for WaitForElement step: {StepName}", step.Name);
                return false;
            }

            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(step.TimeoutSeconds));
                var element = wait.Until(d => d.FindElement(By.CssSelector(step.ElementSelector)));
                return element != null;
            }
            catch (WebDriverTimeoutException)
            {
                _logger.LogWarning("Element '{ElementSelector}' did not appear within timeout for step: {StepName}", 
                    step.ElementSelector, step.Name);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to wait for element '{ElementSelector}' in step: {StepName}", 
                    step.ElementSelector, step.Name);
                return false;
            }
        }

        /// <summary>
        /// Implements the ValidateElements step action.
        /// </summary>
        private async Task<bool> ValidateElementsStepAsync(IWebDriver driver, LoginStep step)
        {
            _logger.LogDebug("Executing ValidateElements step: {StepName}", step.Name);

            try
            {
                // Check positive validation selectors
                foreach (var selector in step.ValidationSelectors)
                {
                    try
                    {
                        driver.FindElement(By.CssSelector(selector));
                        _logger.LogDebug("Validation selector found: {Selector}", selector);
                    }
                    catch (NoSuchElementException)
                    {
                        _logger.LogWarning("Required validation selector not found: {Selector}", selector);
                        return false;
                    }
                }

                // Check negative validation selectors
                foreach (var selector in step.NegativeValidationSelectors)
                {
                    try
                    {
                        driver.FindElement(By.CssSelector(selector));
                        _logger.LogWarning("Negative validation selector found (should not exist): {Selector}", selector);
                        return false;
                    }
                    catch (NoSuchElementException)
                    {
                        _logger.LogDebug("Negative validation selector correctly not found: {Selector}", selector);
                    }
                }

                await Task.Delay(100); // Brief pause
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate elements in step: {StepName}", step.Name);
                return false;
            }
        }

        /// <summary>
        /// Implements the ExecuteScript step action.
        /// </summary>
        private async Task<bool> ExecuteScriptStepAsync(IWebDriver driver, LoginStep step)
        {
            _logger.LogDebug("Executing ExecuteScript step: {StepName}", step.Name);

            if (string.IsNullOrEmpty(step.ScriptCode))
            {
                _logger.LogError("ScriptCode is required for ExecuteScript step: {StepName}", step.Name);
                return false;
            }

            try
            {
                var jsExecutor = (IJavaScriptExecutor)driver;
                var result = jsExecutor.ExecuteScript(step.ScriptCode);
                
                _logger.LogDebug("JavaScript executed successfully, result: {Result}", result);
                await Task.Delay(200); // Brief pause after script execution
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute JavaScript in step: {StepName}", step.Name);
                return false;
            }
        }

        /// <summary>
        /// Implements the EnterText step action.
        /// </summary>
        private async Task<bool> EnterTextStepAsync(IWebDriver driver, LoginStep step)
        {
            _logger.LogDebug("Executing EnterText step: {StepName}", step.Name);

            if (string.IsNullOrEmpty(step.ElementSelector) || string.IsNullOrEmpty(step.TextValue))
            {
                _logger.LogError("ElementSelector and TextValue are required for EnterText step: {StepName}", step.Name);
                return false;
            }

            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(step.TimeoutSeconds));
                var element = wait.Until(d => d.FindElement(By.CssSelector(step.ElementSelector)));
                
                element.Clear();
                element.SendKeys(step.TextValue);
                
                await Task.Delay(200); // Brief pause after text entry
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enter text '{TextValue}' into element '{ElementSelector}' in step: {StepName}", 
                    step.TextValue, step.ElementSelector, step.Name);
                return false;
            }
        }

        /// <summary>
        /// Implements the SelectOption step action.
        /// </summary>
        private async Task<bool> SelectOptionStepAsync(IWebDriver driver, LoginStep step)
        {
            _logger.LogDebug("Executing SelectOption step: {StepName}", step.Name);

            if (string.IsNullOrEmpty(step.ElementSelector) || string.IsNullOrEmpty(step.TextValue))
            {
                _logger.LogError("ElementSelector and TextValue are required for SelectOption step: {StepName}", step.Name);
                return false;
            }

            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(step.TimeoutSeconds));
                var selectElement = wait.Until(d => d.FindElement(By.CssSelector(step.ElementSelector)));
                
                var select = new SelectElement(selectElement);
                select.SelectByText(step.TextValue);
                
                await Task.Delay(200); // Brief pause after selection
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to select option '{TextValue}' from element '{ElementSelector}' in step: {StepName}", 
                    step.TextValue, step.ElementSelector, step.Name);
                return false;
            }
        }

        /// <summary>
        /// Implements the CustomValidation step action.
        /// </summary>
        private async Task<bool> CustomValidationStepAsync(IWebDriver driver, LoginStep step)
        {
            _logger.LogDebug("Executing CustomValidation step: {StepName}", step.Name);

            // This is a placeholder for custom validation logic
            // In a real implementation, you might have a registry of custom validators
            // or use reflection to call validation methods
            
            _logger.LogWarning("CustomValidation step type is not fully implemented: {StepName}", step.Name);
            await Task.Delay(100);
            return true;
        }

        /// <summary>
        /// Validates step completion based on configured validation rules.
        /// </summary>
        private async Task<bool> ValidateStepCompletionAsync(IWebDriver driver, LoginStep step, CancellationToken cancellationToken)
        {
            // URL pattern validation
            if (!string.IsNullOrEmpty(step.ExpectedUrlPattern))
            {
                var regex = new Regex(step.ExpectedUrlPattern, RegexOptions.IgnoreCase);
                if (!regex.IsMatch(driver.Url))
                {
                    _logger.LogWarning("URL validation failed. Expected pattern: {Pattern}, Actual URL: {Url}", 
                        step.ExpectedUrlPattern, driver.Url);
                    return false;
                }
            }

            // Element validation
            if (step.ValidationSelectors.Any() || step.NegativeValidationSelectors.Any())
            {
                return await ValidateElementsStepAsync(driver, step);
            }

            return true;
        }

        /// <summary>
        /// Determines if a flow is applicable to the current URL.
        /// </summary>
        private bool IsFlowApplicable(IWebDriver driver, LoginFlow flow)
        {
            if (!flow.UrlPatterns.Any())
                return true; // No restrictions

            foreach (var pattern in flow.UrlPatterns)
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                if (regex.IsMatch(driver.Url))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if a step should be executed based on its condition.
        /// </summary>
        private bool ShouldExecuteStep(IWebDriver driver, LoginStep step, LoginFlowExecution execution)
        {
            if (string.IsNullOrEmpty(step.Condition))
                return true;

            // This is a simplified condition evaluator
            // In a real implementation, you might want a more sophisticated expression evaluator
            try
            {
                // Basic condition support - can be extended
                if (step.Condition.StartsWith("url:"))
                {
                    var pattern = step.Condition.Substring(4);
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    return regex.IsMatch(driver.Url);
                }
                else if (step.Condition.StartsWith("element:"))
                {
                    var selector = step.Condition.Substring(8);
                    try
                    {
                        driver.FindElement(By.CssSelector(selector));
                        return true;
                    }
                    catch (NoSuchElementException)
                    {
                        return false;
                    }
                }

                _logger.LogWarning("Unknown condition format: {Condition}", step.Condition);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating step condition: {Condition}", step.Condition);
                return true; // Default to executing the step
            }
        }

        /// <summary>
        /// Attempts to recover from flow failure using recovery steps.
        /// </summary>
        private async Task AttemptRecoveryAsync(
            IWebDriver driver,
            LoginFlow flow,
            LoginFlowExecution execution,
            CommandLineOptions credentials,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting recovery process with {RecoveryStepCount} recovery steps", flow.RecoverySteps.Count);
            
            execution.State = LoginFlowState.Retrying;
            OnFlowStateChanged(execution, "Starting recovery process");

            try
            {
                foreach (var recoveryStep in flow.RecoverySteps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    _logger.LogInformation("Executing recovery step: {StepName}", recoveryStep.Name);
                    var result = await ExecuteStepWithRetryAsync(driver, recoveryStep, credentials, execution, cancellationToken);
                    
                    if (!result.Success)
                    {
                        _logger.LogWarning("Recovery step failed: {StepName}", recoveryStep.Name);
                        // Continue with other recovery steps
                    }
                }

                _logger.LogInformation("Recovery process completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery process failed");
            }
        }

        /// <summary>
        /// Fires the StepStarted event.
        /// </summary>
        private void OnStepStarted(LoginFlowExecution execution, LoginStep step)
        {
            StepStarted?.Invoke(this, new StepExecutionEventArgs
            {
                Execution = execution,
                Step = step
            });
        }

        /// <summary>
        /// Fires the StepCompleted event.
        /// </summary>
        private void OnStepCompleted(LoginFlowExecution execution, LoginStep step, StepExecutionResult result)
        {
            StepCompleted?.Invoke(this, new StepExecutionEventArgs
            {
                Execution = execution,
                Step = step,
                Result = result
            });
        }

        /// <summary>
        /// Fires the FlowStateChanged event.
        /// </summary>
        private void OnFlowStateChanged(LoginFlowExecution execution, string message)
        {
            FlowStateChanged?.Invoke(this, new FlowExecutionEventArgs
            {
                Execution = execution,
                Message = message
            });
        }
    }

    /// <summary>
    /// Exception thrown when a login flow fails.
    /// </summary>
    public class LoginFlowException : WebConnectException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoginFlowException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public LoginFlowException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginFlowException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public LoginFlowException(string message, Exception innerException) : base(message, innerException) { }
    }
} 
