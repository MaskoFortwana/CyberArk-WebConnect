using System;
using System.Collections.Generic;
using ChromeConnect.Models;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Fluent builder for creating multi-step login flows using a DSL-like syntax.
    /// </summary>
    public class LoginFlowBuilder
    {
        private readonly LoginFlow _flow;
        private LoginStep? _currentStep;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginFlowBuilder"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the flow.</param>
        /// <param name="name">The human-readable name for the flow.</param>
        public LoginFlowBuilder(string id, string name)
        {
            _flow = new LoginFlow
            {
                Id = id,
                Name = name
            };
        }

        /// <summary>
        /// Sets the description for the flow.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <returns>The builder instance for chaining.</returns>
        public LoginFlowBuilder WithDescription(string description)
        {
            _flow.Description = description;
            return this;
        }

        /// <summary>
        /// Sets the overall timeout for the flow.
        /// </summary>
        /// <param name="timeoutSeconds">The timeout in seconds.</param>
        /// <returns>The builder instance for chaining.</returns>
        public LoginFlowBuilder WithTimeout(int timeoutSeconds)
        {
            _flow.OverallTimeoutSeconds = timeoutSeconds;
            return this;
        }

        /// <summary>
        /// Configures the flow to continue on step failures.
        /// </summary>
        /// <param name="continueOnFailure">Whether to continue on failures.</param>
        /// <returns>The builder instance for chaining.</returns>
        public LoginFlowBuilder ContinueOnStepFailure(bool continueOnFailure = true)
        {
            _flow.ContinueOnStepFailure = continueOnFailure;
            return this;
        }

        /// <summary>
        /// Adds URL patterns that this flow applies to.
        /// </summary>
        /// <param name="patterns">The URL patterns (regex supported).</param>
        /// <returns>The builder instance for chaining.</returns>
        public LoginFlowBuilder ForUrls(params string[] patterns)
        {
            _flow.UrlPatterns.AddRange(patterns);
            return this;
        }

        /// <summary>
        /// Adds a configuration property.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The configuration value.</param>
        /// <returns>The builder instance for chaining.</returns>
        public LoginFlowBuilder WithConfiguration(string key, object value)
        {
            _flow.Configuration[key] = value;
            return this;
        }

        /// <summary>
        /// Starts defining a new step.
        /// </summary>
        /// <param name="id">The step identifier.</param>
        /// <param name="name">The step name.</param>
        /// <returns>A step builder for configuring the step.</returns>
        public LoginStepBuilder AddStep(string id, string name)
        {
            var step = new LoginStep
            {
                Id = id,
                Name = name
            };

            _flow.Steps.Add(step);
            _currentStep = step;
            return new LoginStepBuilder(this, step);
        }

        /// <summary>
        /// Starts defining a recovery step.
        /// </summary>
        /// <param name="id">The step identifier.</param>
        /// <param name="name">The step name.</param>
        /// <returns>A step builder for configuring the recovery step.</returns>
        public LoginStepBuilder AddRecoveryStep(string id, string name)
        {
            var step = new LoginStep
            {
                Id = id,
                Name = name
            };

            _flow.RecoverySteps.Add(step);
            return new LoginStepBuilder(this, step);
        }

        /// <summary>
        /// Builds the final login flow.
        /// </summary>
        /// <returns>The constructed login flow.</returns>
        public LoginFlow Build()
        {
            if (string.IsNullOrEmpty(_flow.Id))
                throw new InvalidOperationException("Flow ID is required");
            
            if (string.IsNullOrEmpty(_flow.Name))
                throw new InvalidOperationException("Flow name is required");

            if (_flow.Steps.Count == 0)
                throw new InvalidOperationException("At least one step is required");

            return _flow;
        }

        /// <summary>
        /// Creates a new flow builder.
        /// </summary>
        /// <param name="id">The flow identifier.</param>
        /// <param name="name">The flow name.</param>
        /// <returns>A new flow builder instance.</returns>
        public static LoginFlowBuilder Create(string id, string name)
        {
            return new LoginFlowBuilder(id, name);
        }
    }

    /// <summary>
    /// Fluent builder for configuring individual steps in a login flow.
    /// </summary>
    public class LoginStepBuilder
    {
        private readonly LoginFlowBuilder _flowBuilder;
        private readonly LoginStep _step;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginStepBuilder"/> class.
        /// </summary>
        /// <param name="flowBuilder">The parent flow builder.</param>
        /// <param name="step">The step being configured.</param>
        public LoginStepBuilder(LoginFlowBuilder flowBuilder, LoginStep step)
        {
            _flowBuilder = flowBuilder;
            _step = step;
        }

        /// <summary>
        /// Configures this step to enter credentials.
        /// </summary>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder EnterCredentials()
        {
            _step.StepType = LoginStepType.EnterCredentials;
            return this;
        }

        /// <summary>
        /// Configures this step to click an element.
        /// </summary>
        /// <param name="selector">The CSS selector for the element to click.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder ClickElement(string selector)
        {
            _step.StepType = LoginStepType.ClickElement;
            _step.ElementSelector = selector;
            return this;
        }

        /// <summary>
        /// Configures this step to wait for navigation.
        /// </summary>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder WaitForNavigation()
        {
            _step.StepType = LoginStepType.WaitForNavigation;
            return this;
        }

        /// <summary>
        /// Configures this step to wait for an element to appear.
        /// </summary>
        /// <param name="selector">The CSS selector for the element to wait for.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder WaitForElement(string selector)
        {
            _step.StepType = LoginStepType.WaitForElement;
            _step.ElementSelector = selector;
            return this;
        }

        /// <summary>
        /// Configures this step to validate elements.
        /// </summary>
        /// <param name="selectors">The CSS selectors to validate.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder ValidateElements(params string[] selectors)
        {
            _step.StepType = LoginStepType.ValidateElements;
            _step.ValidationSelectors.AddRange(selectors);
            return this;
        }

        /// <summary>
        /// Configures this step to execute JavaScript.
        /// </summary>
        /// <param name="script">The JavaScript code to execute.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder ExecuteScript(string script)
        {
            _step.StepType = LoginStepType.ExecuteScript;
            _step.ScriptCode = script;
            return this;
        }

        /// <summary>
        /// Configures this step to enter text into a field.
        /// </summary>
        /// <param name="selector">The CSS selector for the input field.</param>
        /// <param name="text">The text to enter.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder EnterText(string selector, string text)
        {
            _step.StepType = LoginStepType.EnterText;
            _step.ElementSelector = selector;
            _step.TextValue = text;
            return this;
        }

        /// <summary>
        /// Configures this step to select an option from a dropdown.
        /// </summary>
        /// <param name="selector">The CSS selector for the select element.</param>
        /// <param name="optionText">The text of the option to select.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder SelectOption(string selector, string optionText)
        {
            _step.StepType = LoginStepType.SelectOption;
            _step.ElementSelector = selector;
            _step.TextValue = optionText;
            return this;
        }

        /// <summary>
        /// Configures this step for custom validation.
        /// </summary>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder CustomValidation()
        {
            _step.StepType = LoginStepType.CustomValidation;
            return this;
        }

        /// <summary>
        /// Sets the timeout for this step.
        /// </summary>
        /// <param name="timeoutSeconds">The timeout in seconds.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder WithTimeout(int timeoutSeconds)
        {
            _step.TimeoutSeconds = timeoutSeconds;
            return this;
        }

        /// <summary>
        /// Configures retry behavior for this step.
        /// </summary>
        /// <param name="canRetry">Whether the step can be retried.</param>
        /// <param name="maxRetries">The maximum number of retry attempts.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder WithRetry(bool canRetry = true, int maxRetries = 3)
        {
            _step.CanRetry = canRetry;
            _step.MaxRetries = maxRetries;
            return this;
        }

        /// <summary>
        /// Adds validation selectors that must be present after this step.
        /// </summary>
        /// <param name="selectors">The CSS selectors to validate.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder RequireElements(params string[] selectors)
        {
            _step.ValidationSelectors.AddRange(selectors);
            return this;
        }

        /// <summary>
        /// Adds validation selectors that must NOT be present after this step.
        /// </summary>
        /// <param name="selectors">The CSS selectors that should not exist.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder ForbidElements(params string[] selectors)
        {
            _step.NegativeValidationSelectors.AddRange(selectors);
            return this;
        }

        /// <summary>
        /// Sets the expected URL pattern after this step.
        /// </summary>
        /// <param name="urlPattern">The expected URL pattern (regex supported).</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder ExpectUrl(string urlPattern)
        {
            _step.ExpectedUrlPattern = urlPattern;
            return this;
        }

        /// <summary>
        /// Sets a condition for executing this step.
        /// </summary>
        /// <param name="condition">The condition expression.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder OnCondition(string condition)
        {
            _step.Condition = condition;
            return this;
        }

        /// <summary>
        /// Adds a custom property to this step.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        /// <returns>The step builder for chaining.</returns>
        public LoginStepBuilder WithProperty(string key, object value)
        {
            _step.Properties[key] = value;
            return this;
        }

        /// <summary>
        /// Returns to the flow builder to add more steps.
        /// </summary>
        /// <returns>The parent flow builder.</returns>
        public LoginFlowBuilder And()
        {
            return _flowBuilder;
        }

        /// <summary>
        /// Completes the flow definition.
        /// </summary>
        /// <returns>The built login flow.</returns>
        public LoginFlow Build()
        {
            return _flowBuilder.Build();
        }
    }

    /// <summary>
    /// Static factory for creating common login flow patterns.
    /// </summary>
    public static class CommonFlows
    {
        /// <summary>
        /// Creates a simple single-page login flow.
        /// </summary>
        /// <param name="id">The flow identifier.</param>
        /// <param name="name">The flow name.</param>
        /// <param name="submitButtonSelector">The CSS selector for the submit button.</param>
        /// <returns>A configured login flow.</returns>
        public static LoginFlow SimpleLogin(string id, string name, string submitButtonSelector = "input[type='submit'], button[type='submit']")
        {
            return LoginFlowBuilder.Create(id, name)
                .WithDescription("Simple single-page login flow")
                .AddStep("enter-credentials", "Enter Login Credentials")
                    .EnterCredentials()
                    .WithTimeout(10)
                .And()
                .AddStep("click-submit", "Click Submit Button")
                    .ClickElement(submitButtonSelector)
                    .WithTimeout(5)
                    .RequireElements("body") // Basic validation
                .And()
                .AddStep("wait-navigation", "Wait for Login Navigation")
                    .WaitForNavigation()
                    .WithTimeout(15)
                .Build();
        }

        /// <summary>
        /// Creates a two-step login flow (e.g., username first, then password).
        /// </summary>
        /// <param name="id">The flow identifier.</param>
        /// <param name="name">The flow name.</param>
        /// <param name="usernameSelector">The CSS selector for the username field.</param>
        /// <param name="passwordSelector">The CSS selector for the password field.</param>
        /// <param name="nextButtonSelector">The CSS selector for the "Next" button after username.</param>
        /// <param name="submitButtonSelector">The CSS selector for the final submit button.</param>
        /// <returns>A configured login flow.</returns>
        public static LoginFlow TwoStepLogin(string id, string name, 
            string usernameSelector = "input[type='text'], input[type='email']",
            string passwordSelector = "input[type='password']",
            string nextButtonSelector = "button, input[type='submit']",
            string submitButtonSelector = "button, input[type='submit']")
        {
            return LoginFlowBuilder.Create(id, name)
                .WithDescription("Two-step login flow (username first, then password)")
                .AddStep("enter-username", "Enter Username")
                    .EnterText(usernameSelector, "{username}")
                    .WithTimeout(10)
                .And()
                .AddStep("click-next", "Click Next Button")
                    .ClickElement(nextButtonSelector)
                    .WithTimeout(5)
                .And()
                .AddStep("wait-password-page", "Wait for Password Page")
                    .WaitForElement(passwordSelector)
                    .WithTimeout(10)
                .And()
                .AddStep("enter-password", "Enter Password")
                    .EnterText(passwordSelector, "{password}")
                    .WithTimeout(10)
                .And()
                .AddStep("click-submit", "Click Submit Button")
                    .ClickElement(submitButtonSelector)
                    .WithTimeout(5)
                .And()
                .AddStep("wait-navigation", "Wait for Login Navigation")
                    .WaitForNavigation()
                    .WithTimeout(15)
                .Build();
        }

        /// <summary>
        /// Creates a login flow that handles JavaScript-heavy pages.
        /// </summary>
        /// <param name="id">The flow identifier.</param>
        /// <param name="name">The flow name.</param>
        /// <returns>A configured login flow.</returns>
        public static LoginFlow JavaScriptLogin(string id, string name)
        {
            return LoginFlowBuilder.Create(id, name)
                .WithDescription("Login flow for JavaScript-heavy pages")
                .AddStep("wait-page-load", "Wait for Page Load")
                    .ExecuteScript("return document.readyState === 'complete';")
                    .WithTimeout(15)
                .And()
                .AddStep("wait-login-form", "Wait for Login Form")
                    .WaitForElement("input[type='password']")
                    .WithTimeout(20)
                .And()
                .AddStep("enter-credentials", "Enter Login Credentials")
                    .EnterCredentials()
                    .WithTimeout(10)
                .And()
                .AddStep("trigger-submit", "Trigger Submit Event")
                    .ExecuteScript("document.querySelector('form').dispatchEvent(new Event('submit'));")
                    .WithTimeout(5)
                .And()
                .AddStep("wait-navigation", "Wait for Login Navigation")
                    .WaitForNavigation()
                    .WithTimeout(20)
                .Build();
        }
    }
} 