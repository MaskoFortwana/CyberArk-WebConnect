using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ChromeConnect.Tests.Integration
{
    /// <summary>
    /// Generates comprehensive test reports from test execution results
    /// </summary>
    public static class TestReportGenerator
    {
        /// <summary>
        /// Generate a comprehensive HTML report from test results
        /// </summary>
        public static string GenerateHtmlReport(IEnumerable<TestSiteResult> results, string title = "ChromeConnect Test Report")
        {
            var resultsList = results.ToList();
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine($"    <title>{title}</title>");
            html.AppendLine("    <style>");
            html.AppendLine(GetReportStyles());
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine($"    <h1>{title}</h1>");
            html.AppendLine($"    <p>Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");

            // Summary Section
            html.AppendLine("    <div class='summary'>");
            html.AppendLine("        <h2>Test Summary</h2>");
            GenerateTestSummary(html, resultsList);
            html.AppendLine("    </div>");

            // Performance Metrics Section
            html.AppendLine("    <div class='performance'>");
            html.AppendLine("        <h2>Performance Metrics</h2>");
            GeneratePerformanceSection(html, resultsList);
            html.AppendLine("    </div>");

            // Detailed Results Section
            html.AppendLine("    <div class='detailed-results'>");
            html.AppendLine("        <h2>Detailed Test Results</h2>");
            GenerateDetailedResults(html, resultsList);
            html.AppendLine("    </div>");

            // Validation Analysis Section
            html.AppendLine("    <div class='validation-analysis'>");
            html.AppendLine("        <h2>Validation Analysis</h2>");
            GenerateValidationAnalysis(html, resultsList);
            html.AppendLine("    </div>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        /// <summary>
        /// Generate a JSON report from test results
        /// </summary>
        public static string GenerateJsonReport(IEnumerable<TestSiteResult> results)
        {
            var report = new
            {
                GeneratedAt = DateTime.UtcNow,
                Summary = GenerateJsonSummary(results),
                Results = results.Select(r => new
                {
                    r.SiteName,
                    r.Success,
                    r.ErrorMessage,
                    ExecutionTime = (r.EndTime - r.StartTime).TotalMilliseconds,
                    r.PerformanceMetrics,
                    ValidationsPassed = r.ValidationsPassed,
                    ValidationsFailed = r.ValidationsFailed,
                    Flags = new
                    {
                        r.FormDetected,
                        r.DomainFieldHandled,
                        r.DomainSelectionSuccessful,
                        r.ProgressiveFieldsDetected,
                        r.PostPasswordDomainHandled,
                        r.UsernameDropdownOptimized,
                        r.HandledGracefully
                    }
                })
            };

            return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Generate a CSV report from test results
        /// </summary>
        public static string GenerateCsvReport(IEnumerable<TestSiteResult> results)
        {
            var csv = new StringBuilder();
            
            // Header
            csv.AppendLine("SiteName,Success,ErrorMessage,ExecutionTimeMs,DetectionTimeMs,UsernameEntryTimeMs,PasswordEntryTimeMs,DomainEntryTimeMs,FormSubmissionTimeMs,TotalTimeMs,ValidationsPassed,ValidationsFailed,FormDetected,DomainFieldHandled,ProgressiveFieldsDetected,PostPasswordDomainHandled,UsernameDropdownOptimized");

            // Data rows
            foreach (var result in results)
            {
                var executionTime = (result.EndTime - result.StartTime).TotalMilliseconds;
                var validationsPassed = string.Join(";", result.ValidationsPassed);
                var validationsFailed = string.Join(";", result.ValidationsFailed);
                var errorMessage = result.ErrorMessage?.Replace(",", ";") ?? "";

                csv.AppendLine($"{result.SiteName},{result.Success},\"{errorMessage}\",{executionTime},{result.PerformanceMetrics.DetectionTimeMs},{result.PerformanceMetrics.UsernameEntryTimeMs},{result.PerformanceMetrics.PasswordEntryTimeMs},{result.PerformanceMetrics.DomainEntryTimeMs},{result.PerformanceMetrics.FormSubmissionTimeMs},{result.PerformanceMetrics.TotalTimeMs},\"{validationsPassed}\",\"{validationsFailed}\",{result.FormDetected},{result.DomainFieldHandled},{result.ProgressiveFieldsDetected},{result.PostPasswordDomainHandled},{result.UsernameDropdownOptimized}");
            }

            return csv.ToString();
        }

        /// <summary>
        /// Save report to file
        /// </summary>
        public static void SaveReport(string content, string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
        }

        private static void GenerateTestSummary(StringBuilder html, List<TestSiteResult> results)
        {
            var totalTests = results.Count;
            var passedTests = results.Count(r => r.Success);
            var failedTests = totalTests - passedTests;
            var passRate = totalTests > 0 ? (passedTests * 100.0 / totalTests) : 0;

            html.AppendLine("        <table class='summary-table'>");
            html.AppendLine("            <tr><td>Total Tests:</td><td>" + totalTests + "</td></tr>");
            html.AppendLine("            <tr><td>Passed:</td><td class='success'>" + passedTests + "</td></tr>");
            html.AppendLine("            <tr><td>Failed:</td><td class='failure'>" + failedTests + "</td></tr>");
            html.AppendLine($"            <tr><td>Pass Rate:</td><td class='{(passRate >= 80 ? "success" : "failure")}'>{passRate:F1}%</td></tr>");
            
            if (results.Any())
            {
                var avgExecutionTime = results.Average(r => (r.EndTime - r.StartTime).TotalMilliseconds);
                var avgDetectionTime = results.Average(r => r.PerformanceMetrics.DetectionTimeMs);
                html.AppendLine($"            <tr><td>Avg Execution Time:</td><td>{avgExecutionTime:F0}ms</td></tr>");
                html.AppendLine($"            <tr><td>Avg Detection Time:</td><td>{avgDetectionTime:F0}ms</td></tr>");
            }
            
            html.AppendLine("        </table>");
        }

        private static void GeneratePerformanceSection(StringBuilder html, List<TestSiteResult> results)
        {
            if (!results.Any()) return;

            html.AppendLine("        <table class='performance-table'>");
            html.AppendLine("            <thead>");
            html.AppendLine("                <tr>");
            html.AppendLine("                    <th>Site</th>");
            html.AppendLine("                    <th>Detection (ms)</th>");
            html.AppendLine("                    <th>Username Entry (ms)</th>");
            html.AppendLine("                    <th>Password Entry (ms)</th>");
            html.AppendLine("                    <th>Domain Entry (ms)</th>");
            html.AppendLine("                    <th>Total (ms)</th>");
            html.AppendLine("                    <th>Status</th>");
            html.AppendLine("                </tr>");
            html.AppendLine("            </thead>");
            html.AppendLine("            <tbody>");

            foreach (var result in results.OrderBy(r => r.SiteName))
            {
                var statusClass = result.Success ? "success" : "failure";
                html.AppendLine("                <tr>");
                html.AppendLine($"                    <td>{result.SiteName}</td>");
                html.AppendLine($"                    <td>{result.PerformanceMetrics.DetectionTimeMs}</td>");
                html.AppendLine($"                    <td>{result.PerformanceMetrics.UsernameEntryTimeMs}</td>");
                html.AppendLine($"                    <td>{result.PerformanceMetrics.PasswordEntryTimeMs}</td>");
                html.AppendLine($"                    <td>{result.PerformanceMetrics.DomainEntryTimeMs}</td>");
                html.AppendLine($"                    <td>{result.PerformanceMetrics.TotalTimeMs}</td>");
                html.AppendLine($"                    <td class='{statusClass}'>{(result.Success ? "✓" : "✗")}</td>");
                html.AppendLine("                </tr>");
            }

            html.AppendLine("            </tbody>");
            html.AppendLine("        </table>");
        }

        private static void GenerateDetailedResults(StringBuilder html, List<TestSiteResult> results)
        {
            foreach (var result in results.OrderBy(r => r.SiteName))
            {
                var statusClass = result.Success ? "success" : "failure";
                html.AppendLine($"        <div class='test-result {statusClass}'>");
                html.AppendLine($"            <h3>{result.SiteName} {(result.Success ? "✓" : "✗")}</h3>");
                
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    html.AppendLine($"            <p class='error-message'><strong>Error:</strong> {result.ErrorMessage}</p>");
                }

                html.AppendLine("            <div class='test-details'>");
                html.AppendLine("                <div class='validations'>");
                html.AppendLine("                    <h4>Validations</h4>");
                
                if (result.ValidationsPassed.Any())
                {
                    html.AppendLine("                    <p><strong>Passed:</strong></p>");
                    html.AppendLine("                    <ul class='validations-passed'>");
                    foreach (var validation in result.ValidationsPassed)
                    {
                        html.AppendLine($"                        <li>✓ {validation}</li>");
                    }
                    html.AppendLine("                    </ul>");
                }

                if (result.ValidationsFailed.Any())
                {
                    html.AppendLine("                    <p><strong>Failed:</strong></p>");
                    html.AppendLine("                    <ul class='validations-failed'>");
                    foreach (var validation in result.ValidationsFailed)
                    {
                        html.AppendLine($"                        <li>✗ {validation}</li>");
                    }
                    html.AppendLine("                    </ul>");
                }

                html.AppendLine("                </div>");
                
                html.AppendLine("                <div class='flags'>");
                html.AppendLine("                    <h4>Feature Flags</h4>");
                html.AppendLine("                    <ul>");
                html.AppendLine($"                        <li>Form Detected: {(result.FormDetected ? "✓" : "✗")}</li>");
                html.AppendLine($"                        <li>Domain Field Handled: {(result.DomainFieldHandled ? "✓" : "✗")}</li>");
                html.AppendLine($"                        <li>Progressive Fields Detected: {(result.ProgressiveFieldsDetected ? "✓" : "✗")}</li>");
                html.AppendLine($"                        <li>Post-Password Domain Handled: {(result.PostPasswordDomainHandled ? "✓" : "✗")}</li>");
                html.AppendLine($"                        <li>Username Dropdown Optimized: {(result.UsernameDropdownOptimized ? "✓" : "✗")}</li>");
                html.AppendLine("                    </ul>");
                html.AppendLine("                </div>");
                html.AppendLine("            </div>");
                html.AppendLine("        </div>");
            }
        }

        private static void GenerateValidationAnalysis(StringBuilder html, List<TestSiteResult> results)
        {
            var allValidations = results.SelectMany(r => r.ValidationsPassed.Concat(r.ValidationsFailed)).Distinct().OrderBy(v => v);
            
            html.AppendLine("        <table class='validation-analysis-table'>");
            html.AppendLine("            <thead>");
            html.AppendLine("                <tr>");
            html.AppendLine("                    <th>Validation</th>");
            html.AppendLine("                    <th>Passed</th>");
            html.AppendLine("                    <th>Failed</th>");
            html.AppendLine("                    <th>Success Rate</th>");
            html.AppendLine("                </tr>");
            html.AppendLine("            </thead>");
            html.AppendLine("            <tbody>");

            foreach (var validation in allValidations)
            {
                var passed = results.Count(r => r.ValidationsPassed.Contains(validation));
                var failed = results.Count(r => r.ValidationsFailed.Contains(validation));
                var total = passed + failed;
                var successRate = total > 0 ? (passed * 100.0 / total) : 0;

                html.AppendLine("                <tr>");
                html.AppendLine($"                    <td>{validation}</td>");
                html.AppendLine($"                    <td class='success'>{passed}</td>");
                html.AppendLine($"                    <td class='failure'>{failed}</td>");
                html.AppendLine($"                    <td class='{(successRate >= 80 ? "success" : "failure")}'>{successRate:F1}%</td>");
                html.AppendLine("                </tr>");
            }

            html.AppendLine("            </tbody>");
            html.AppendLine("        </table>");
        }

        private static object GenerateJsonSummary(IEnumerable<TestSiteResult> results)
        {
            var resultsList = results.ToList();
            var totalTests = resultsList.Count;
            var passedTests = resultsList.Count(r => r.Success);
            
            return new
            {
                TotalTests = totalTests,
                PassedTests = passedTests,
                FailedTests = totalTests - passedTests,
                PassRate = totalTests > 0 ? (passedTests * 100.0 / totalTests) : 0,
                AverageExecutionTimeMs = resultsList.Any() ? resultsList.Average(r => (r.EndTime - r.StartTime).TotalMilliseconds) : 0,
                AverageDetectionTimeMs = resultsList.Any() ? resultsList.Average(r => r.PerformanceMetrics.DetectionTimeMs) : 0
            };
        }

        private static string GetReportStyles()
        {
            return @"
                body { font-family: Arial, sans-serif; margin: 20px; }
                h1 { color: #333; border-bottom: 2px solid #007acc; }
                h2 { color: #555; margin-top: 30px; }
                h3 { color: #666; }
                table { border-collapse: collapse; width: 100%; margin: 10px 0; }
                th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
                th { background-color: #f2f2f2; font-weight: bold; }
                .success { color: #28a745; font-weight: bold; }
                .failure { color: #dc3545; font-weight: bold; }
                .test-result { margin: 15px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; }
                .test-result.success { border-left: 4px solid #28a745; background-color: #f8fff9; }
                .test-result.failure { border-left: 4px solid #dc3545; background-color: #fff8f8; }
                .test-details { display: flex; flex-wrap: wrap; gap: 20px; margin-top: 10px; }
                .validations, .flags { flex: 1; min-width: 300px; }
                .validations-passed li { color: #28a745; }
                .validations-failed li { color: #dc3545; }
                .error-message { color: #dc3545; background-color: #fff5f5; padding: 10px; border-radius: 3px; }
                .summary { background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0; }
                .performance { margin: 20px 0; }
                .detailed-results { margin: 20px 0; }
                .validation-analysis { margin: 20px 0; }
                ul { margin: 5px 0; padding-left: 20px; }
            ";
        }
    }
} 