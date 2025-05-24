using System;

namespace ChromeConnect.Tests.Integration
{
    /// <summary>
    /// Represents test credentials for login scenarios
    /// </summary>
    public class TestCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Domain { get; set; }
    }

    /// <summary>
    /// Represents a test scenario for a specific test site
    /// </summary>
    public class TestSiteScenario
    {
        public string SiteName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TestResult ExpectedResult { get; set; }
        public TestCredentials TestCredentials { get; set; } = new();
        public ValidationCheck[] ValidationChecks { get; set; } = Array.Empty<ValidationCheck>();
        public bool RequiresProgressiveDetection { get; set; }
        public bool RequiresPostPasswordDomainHandling { get; set; }
        public PerformanceExpectations? PerformanceExpectations { get; set; }
    }

    /// <summary>
    /// Performance expectations for test scenarios
    /// </summary>
    public class PerformanceExpectations
    {
        public int MaxDetectionTimeMs { get; set; } = 3000;
        public int MaxUsernameEntryTimeMs { get; set; } = 1000;
        public int MaxPasswordEntryTimeMs { get; set; } = 1000;
        public int MaxDomainEntryTimeMs { get; set; } = 1000;
        public int MaxTotalTimeMs { get; set; } = 10000;
    }

    /// <summary>
    /// Performance metrics captured during test execution
    /// </summary>
    public class PerformanceMetrics
    {
        public int DetectionTimeMs { get; set; }
        public int UsernameEntryTimeMs { get; set; }
        public int PasswordEntryTimeMs { get; set; }
        public int DomainEntryTimeMs { get; set; }
        public int TotalTimeMs { get; set; }
        public int FormSubmissionTimeMs { get; set; }
        public int WaitTimeMs { get; set; }
    }

    /// <summary>
    /// Result of a test site execution
    /// </summary>
    public class TestSiteResult
    {
        public string SiteName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public PerformanceMetrics PerformanceMetrics { get; set; } = new();
        
        // Validation flags
        public bool FormDetected { get; set; }
        public bool DomainFieldHandled { get; set; }
        public bool DomainSelectionSuccessful { get; set; }
        public bool ProgressiveFieldsDetected { get; set; }
        public bool PostPasswordDomainHandled { get; set; }
        public bool HandledGracefully { get; set; }
        public bool UsernameDropdownOptimized { get; set; }
        
        // Execution details
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string[] ValidationsPassed { get; set; } = Array.Empty<string>();
        public string[] ValidationsFailed { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Expected test result
    /// </summary>
    public enum TestResult
    {
        Success,
        Failure,
        PartialSuccess
    }

    /// <summary>
    /// Validation checks to perform during testing
    /// </summary>
    public enum ValidationCheck
    {
        FormDetection,
        CredentialEntry,
        Submission,
        DomainFieldDetection,
        DomainDropdownSelection,
        DomainFieldHandling,
        UsernameDropdownHandling,
        PerformanceOptimization,
        ProgressiveFieldDetection,
        PostPasswordDomainDetection,
        DOMChangeMonitoring,
        CharacterByCharacterTyping,
        MultipleSelectionStrategies,
        ErrorHandling,
        NoRegression
    }

    /// <summary>
    /// Test site information for framework management
    /// </summary>
    public class TestSiteInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] KnownIssues { get; set; } = Array.Empty<string>();
        public string[] RequiredFixes { get; set; } = Array.Empty<string>();
    }
} 