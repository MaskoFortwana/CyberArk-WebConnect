using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ChromeConnect.Core;
using ChromeConnect.Models;
using ChromeConnect.Services;

namespace ChromeConnect.Tests.Integration
{
    /// <summary>
    /// Comprehensive test framework validating all fixes across the six test sites
    /// Tests the implementations from subtasks 32.1-32.7 for login field detection and domain handling
    /// </summary>
    [TestClass]
    public class ComprehensiveTestSiteValidationTests
    {
        private static IWebDriver? _driver;
        private static LoginDetector? _loginDetector;
        private static CredentialManager? _credentialManager;
        private static ILogger<LoginDetector>? _detectorLogger;
        private static ILogger<CredentialManager>? _credentialLogger;
        private static DynamicFormDetectionService? _dynamicFormService;
        private static EnhancedDomainHandlingService? _domainHandlingService;
        private static DynamicTimingService? _timingService;
        private TestSiteFramework? _testSiteFramework;

        private const string TestUsername = "testuser@example.com";
        private const string TestPassword = "TestPassword123";
        private const string TestDomain = "TestDomain";

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            // Initialize loggers
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _detectorLogger = loggerFactory.CreateLogger<LoginDetector>();
            _credentialLogger = loggerFactory.CreateLogger<CredentialManager>();

            // Initialize Chrome driver with options
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArgument("--headless");
            chromeOptions.AddArgument("--no-sandbox");
            chromeOptions.AddArgument("--disable-dev-shm-usage");
            chromeOptions.AddArgument("--disable-gpu");
            chromeOptions.AddArgument("--window-size=1920,1080");

            _driver = new ChromeDriver(chromeOptions);

            // Initialize core services
            _loginDetector = new LoginDetector(_detectorLogger);
            _credentialManager = new CredentialManager(_credentialLogger);
            
            // Initialize enhanced services from subtasks 32.5-32.7
            var dynamicFormLogger = loggerFactory.CreateLogger<DynamicFormDetectionService>();
            _dynamicFormService = new DynamicFormDetectionService(dynamicFormLogger, _loginDetector, _credentialManager);
            
            var domainHandlingLogger = loggerFactory.CreateLogger<EnhancedDomainHandlingService>();
            _domainHandlingService = new EnhancedDomainHandlingService(domainHandlingLogger);
            
            var timingLogger = loggerFactory.CreateLogger<DynamicTimingService>();
            _timingService = new DynamicTimingService(timingLogger, _driver);
        }

        [TestInitialize]
        public void TestSetup()
        {
            _testSiteFramework = new TestSiteFramework(_driver!, _loginDetector!, _credentialManager!);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }

        /// <summary>
        /// Test login.htm - Basic working site (baseline)
        /// Validates that basic functionality continues to work
        /// </summary>
        [TestMethod]
        [TestCategory("Baseline")]
        public async Task TestSite_Login_Basic_ShouldWork()
        {
            var result = await _testSiteFramework!.RunTestSiteScenario(new TestSiteScenario
            {
                SiteName = "login.htm",
                Description = "Basic working login site (baseline)",
                ExpectedResult = TestResult.Success,
                TestCredentials = new TestCredentials { Username = TestUsername, Password = TestPassword },
                ValidationChecks = new[]
                {
                    ValidationCheck.FormDetection,
                    ValidationCheck.CredentialEntry,
                    ValidationCheck.Submission
                }
            });

            Assert.IsTrue(result.Success, $"login.htm test failed: {result.ErrorMessage}");
            Assert.IsTrue(result.PerformanceMetrics.DetectionTimeMs < 2000, "Detection should be fast for basic site");
        }

        /// <summary>
        /// Test login2.htm - Username dropdown performance optimization (Subtask 32.4)
        /// Validates the performance improvements for dropdown handling
        /// </summary>
        [TestMethod]
        [TestCategory("Performance")]
        public async Task TestSite_Login2_UsernameDropdownPerformance_ShouldBeOptimized()
        {
            var result = await _testSiteFramework!.RunTestSiteScenario(new TestSiteScenario
            {
                SiteName = "login2.htm",
                Description = "Username dropdown with performance optimization",
                ExpectedResult = TestResult.Success,
                TestCredentials = new TestCredentials { Username = TestUsername, Password = TestPassword },
                ValidationChecks = new[]
                {
                    ValidationCheck.FormDetection,
                    ValidationCheck.UsernameDropdownHandling,
                    ValidationCheck.PerformanceOptimization,
                    ValidationCheck.CredentialEntry,
                    ValidationCheck.Submission
                },
                PerformanceExpectations = new PerformanceExpectations
                {
                    MaxDetectionTimeMs = 1500,
                    MaxUsernameEntryTimeMs = 500,
                    MaxTotalTimeMs = 3000
                }
            });

            Assert.IsTrue(result.Success, $"login2.htm test failed: {result.ErrorMessage}");
            Assert.IsTrue(result.PerformanceMetrics.UsernameEntryTimeMs < 500, 
                $"Username dropdown should be optimized. Actual: {result.PerformanceMetrics.UsernameEntryTimeMs}ms");
        }

        /// <summary>
        /// Test login3.htm - Domain dropdown selection fix (Subtask 32.1)
        /// Validates that domain dropdowns are properly selected
        /// </summary>
        [TestMethod]
        [TestCategory("DomainHandling")]
        public async Task TestSite_Login3_DomainDropdownSelection_ShouldWork()
        {
            var result = await _testSiteFramework!.RunTestSiteScenario(new TestSiteScenario
            {
                SiteName = "login3.htm",
                Description = "Domain dropdown selection functionality",
                ExpectedResult = TestResult.Success,
                TestCredentials = new TestCredentials 
                { 
                    Username = TestUsername, 
                    Password = TestPassword, 
                    Domain = TestDomain 
                },
                ValidationChecks = new[]
                {
                    ValidationCheck.FormDetection,
                    ValidationCheck.DomainFieldDetection,
                    ValidationCheck.DomainDropdownSelection,
                    ValidationCheck.MultipleSelectionStrategies,
                    ValidationCheck.CredentialEntry,
                    ValidationCheck.Submission
                }
            });

            Assert.IsTrue(result.Success, $"login3.htm test failed: {result.ErrorMessage}");
            Assert.IsTrue(result.DomainFieldHandled, "Domain field should be properly handled");
            Assert.IsTrue(result.DomainSelectionSuccessful, "Domain dropdown selection should succeed");
        }

        /// <summary>
        /// Test login4.htm - Progressive field detection (Subtask 32.2)
        /// Validates that fields appearing progressively are handled correctly
        /// </summary>
        [TestMethod]
        [TestCategory("ProgressiveForms")]
        public async Task TestSite_Login4_ProgressiveFieldDetection_ShouldWork()
        {
            var result = await _testSiteFramework!.RunTestSiteScenario(new TestSiteScenario
            {
                SiteName = "login4.htm",
                Description = "Progressive field appearance (username → password → button)",
                ExpectedResult = TestResult.Success,
                TestCredentials = new TestCredentials { Username = TestUsername, Password = TestPassword },
                ValidationChecks = new[]
                {
                    ValidationCheck.FormDetection,
                    ValidationCheck.ProgressiveFieldDetection,
                    ValidationCheck.DOMChangeMonitoring,
                    ValidationCheck.CharacterByCharacterTyping,
                    ValidationCheck.CredentialEntry,
                    ValidationCheck.Submission
                },
                RequiresProgressiveDetection = true
            });

            Assert.IsTrue(result.Success, $"login4.htm test failed: {result.ErrorMessage}");
            Assert.IsTrue(result.ProgressiveFieldsDetected, "Progressive fields should be detected");
            Assert.IsTrue(result.PerformanceMetrics.DetectionTimeMs < 5000, "Progressive detection should complete in reasonable time");
        }

        /// <summary>
        /// Test login5.htm - Post-password domain handling (Subtask 32.3)
        /// Validates that domain fields appearing after password entry are handled
        /// </summary>
        [TestMethod]
        [TestCategory("PostPasswordDomain")]
        public async Task TestSite_Login5_PostPasswordDomainHandling_ShouldWork()
        {
            var result = await _testSiteFramework!.RunTestSiteScenario(new TestSiteScenario
            {
                SiteName = "login5.htm",
                Description = "Domain field appearing after password entry",
                ExpectedResult = TestResult.Success,
                TestCredentials = new TestCredentials 
                { 
                    Username = TestUsername, 
                    Password = TestPassword, 
                    Domain = TestDomain 
                },
                ValidationChecks = new[]
                {
                    ValidationCheck.FormDetection,
                    ValidationCheck.ProgressiveFieldDetection,
                    ValidationCheck.PostPasswordDomainDetection,
                    ValidationCheck.DomainFieldHandling,
                    ValidationCheck.CredentialEntry,
                    ValidationCheck.Submission
                },
                RequiresProgressiveDetection = true,
                RequiresPostPasswordDomainHandling = true
            });

            Assert.IsTrue(result.Success, $"login5.htm test failed: {result.ErrorMessage}");
            Assert.IsTrue(result.PostPasswordDomainHandled, "Post-password domain field should be handled");
            Assert.IsTrue(result.DomainFieldHandled, "Domain field should be properly filled");
        }

        /// <summary>
        /// Test login6.htm - Basic working site (regression check)
        /// Ensures fixes don't break working functionality
        /// </summary>
        [TestMethod]
        [TestCategory("Regression")]
        public async Task TestSite_Login6_RegressionCheck_ShouldWork()
        {
            var result = await _testSiteFramework!.RunTestSiteScenario(new TestSiteScenario
            {
                SiteName = "login6.htm",
                Description = "Regression test for basic functionality",
                ExpectedResult = TestResult.Success,
                TestCredentials = new TestCredentials { Username = TestUsername, Password = TestPassword },
                ValidationChecks = new[]
                {
                    ValidationCheck.FormDetection,
                    ValidationCheck.CredentialEntry,
                    ValidationCheck.Submission,
                    ValidationCheck.NoRegression
                }
            });

            Assert.IsTrue(result.Success, $"login6.htm test failed: {result.ErrorMessage}");
            Assert.IsTrue(result.PerformanceMetrics.DetectionTimeMs < 2000, "Performance should not regress");
        }

        /// <summary>
        /// Comprehensive test running all sites in sequence
        /// Validates overall system stability and performance
        /// </summary>
        [TestMethod]
        [TestCategory("Comprehensive")]
        public async Task TestAllSites_Comprehensive_ShouldAllWork()
        {
            var allResults = new List<TestSiteResult>();
            var testSites = new[]
            {
                "login.htm", "login2.htm", "login3.htm", 
                "login4.htm", "login5.htm", "login6.htm"
            };

            foreach (var site in testSites)
            {
                var credentials = site.Contains("3") || site.Contains("5") 
                    ? new TestCredentials { Username = TestUsername, Password = TestPassword, Domain = TestDomain }
                    : new TestCredentials { Username = TestUsername, Password = TestPassword };

                var scenario = new TestSiteScenario
                {
                    SiteName = site,
                    Description = $"Comprehensive test for {site}",
                    ExpectedResult = TestResult.Success,
                    TestCredentials = credentials,
                    ValidationChecks = GetValidationChecksForSite(site),
                    RequiresProgressiveDetection = site.Contains("4") || site.Contains("5"),
                    RequiresPostPasswordDomainHandling = site.Contains("5")
                };

                var result = await _testSiteFramework!.RunTestSiteScenario(scenario);
                allResults.Add(result);
            }

            // Validate all results
            var failures = allResults.Where(r => !r.Success).ToList();
            Assert.AreEqual(0, failures.Count, 
                $"Some sites failed: {string.Join(", ", failures.Select(f => $"{f.SiteName}: {f.ErrorMessage}"))}");

            // Performance validation
            var totalTime = allResults.Sum(r => r.PerformanceMetrics.TotalTimeMs);
            Assert.IsTrue(totalTime < 20000, $"Total test time should be under 20 seconds. Actual: {totalTime}ms");
        }

        /// <summary>
        /// Edge case testing across all sites
        /// Tests boundary conditions and error scenarios
        /// </summary>
        [TestMethod]
        [TestCategory("EdgeCases")]
        public async Task TestAllSites_EdgeCases_ShouldHandleGracefully()
        {
            var edgeCaseScenarios = new[]
            {
                // Empty credentials
                new TestSiteScenario
                {
                    SiteName = "login.htm",
                    Description = "Empty credentials test",
                    ExpectedResult = TestResult.Failure,
                    TestCredentials = new TestCredentials { Username = "", Password = "" },
                    ValidationChecks = new[] { ValidationCheck.FormDetection, ValidationCheck.ErrorHandling }
                },
                
                // Very long credentials
                new TestSiteScenario
                {
                    SiteName = "login2.htm",
                    Description = "Long credentials test",
                    ExpectedResult = TestResult.Success,
                    TestCredentials = new TestCredentials 
                    { 
                        Username = new string('a', 100) + "@example.com", 
                        Password = new string('b', 100) 
                    },
                    ValidationChecks = new[] { ValidationCheck.FormDetection, ValidationCheck.CredentialEntry }
                },
                
                // Special characters in domain
                new TestSiteScenario
                {
                    SiteName = "login3.htm",
                    Description = "Special characters in domain",
                    ExpectedResult = TestResult.Success,
                    TestCredentials = new TestCredentials 
                    { 
                        Username = TestUsername, 
                        Password = TestPassword, 
                        Domain = "Test-Domain_123" 
                    },
                    ValidationChecks = new[] { ValidationCheck.FormDetection, ValidationCheck.DomainFieldHandling }
                }
            };

            foreach (var scenario in edgeCaseScenarios)
            {
                var result = await _testSiteFramework!.RunTestSiteScenario(scenario);
                
                if (scenario.ExpectedResult == TestResult.Success)
                {
                    Assert.IsTrue(result.Success, $"Edge case failed for {scenario.SiteName}: {result.ErrorMessage}");
                }
                else
                {
                    // For failure cases, we expect the system to handle gracefully (not crash)
                    Assert.IsTrue(result.HandledGracefully, $"System should handle edge case gracefully for {scenario.SiteName}");
                }
            }
        }

        private ValidationCheck[] GetValidationChecksForSite(string siteName)
        {
            var baseChecks = new List<ValidationCheck>
            {
                ValidationCheck.FormDetection,
                ValidationCheck.CredentialEntry,
                ValidationCheck.Submission
            };

            return siteName switch
            {
                "login2.htm" => baseChecks.Concat(new[] { ValidationCheck.UsernameDropdownHandling, ValidationCheck.PerformanceOptimization }).ToArray(),
                "login3.htm" => baseChecks.Concat(new[] { ValidationCheck.DomainFieldDetection, ValidationCheck.DomainDropdownSelection }).ToArray(),
                "login4.htm" => baseChecks.Concat(new[] { ValidationCheck.ProgressiveFieldDetection, ValidationCheck.DOMChangeMonitoring }).ToArray(),
                "login5.htm" => baseChecks.Concat(new[] { ValidationCheck.ProgressiveFieldDetection, ValidationCheck.PostPasswordDomainDetection }).ToArray(),
                _ => baseChecks.ToArray()
            };
        }
    }
} 