# WebConnect Usage Examples

This guide provides detailed, real-world examples of how to use WebConnect in various scenarios. Each example includes complete command-line syntax, explanations, and expected outcomes.

## 📋 Table of Contents

- [Quick Start Examples](#quick-start-examples)
- [Corporate and Enterprise Scenarios](#corporate-and-enterprise-scenarios)
- [Development and Testing Scenarios](#development-and-testing-scenarios)
- [Automation and Scripting Scenarios](#automation-and-scripting-scenarios)
- [Integration Examples](#integration-examples)
- [Advanced Configuration Examples](#advanced-configuration-examples)
- [Troubleshooting and Debugging Examples](#troubleshooting-and-debugging-examples)
- [Best Practices and Tips](#best-practices-and-tips)

---

## 🚀 Quick Start Examples

### Example 1: Basic Corporate Portal Login

**Scenario**: Standard corporate portal with username/password authentication.

**Command**:
```powershell
WebConnect.exe --USR john.doe --PSW SecurePass123! --URL https://portal.company.com/login --DOM CORPORATE --INCOGNITO yes --KIOSK no --CERT ignore
```

**Explanation**:
- `--USR john.doe`: Username for the corporate account
- `--PSW SecurePass123!`: Password (automatically masked in logs)
- `--URL https://portal.company.com/login`: Direct URL to the login page
- `--DOM CORPORATE`: Domain identifier for the organization
- `--INCOGNITO yes`: Use incognito mode for privacy
- `--KIOSK no`: Use normal windowed mode
- `--CERT ignore`: Ignore certificate validation issues

**Expected Outcome**:
- Chrome opens in incognito mode
- Navigates to the login page
- Fills in credentials automatically
- Submits the form
- Leaves browser open for continued use
- Returns exit code 0 on success

**Common Use Cases**:
- Daily work routine automation
- Quick access to corporate systems
- Secure credential handling

---

## 🏢 Corporate and Enterprise Scenarios

### Example 2: Enterprise SSO with Advanced Options

**Scenario**: Single Sign-On portal in a corporate environment with security policies.

**Command**:
```powershell
WebConnect.exe --USR employee.id@company.com --PSW EnterprisePass2024 --URL https://sso.company.com/auth/login --DOM NORTH_AMERICA --INCOGNITO yes --KIOSK no --CERT enforce --debug
```

**PowerShell Script Example**:
```powershell
# Enterprise-SSO-Login.ps1
param(
    [Parameter(Mandatory)]
    [string]$EmployeeId,
    
    [Parameter(Mandatory)]
    [SecureString]$Password,
    
    [string]$Region = "NORTH_AMERICA"
)

# Convert SecureString to plain text for WebConnect
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
$PlainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)

Write-Host "Initiating SSO login for $EmployeeId in region $Region..." -ForegroundColor Yellow

$result = & WebConnect.exe --USR $EmployeeId --PSW $PlainPassword --URL "https://sso.company.com/auth/login" --DOM $Region --INCOGNITO yes --KIOSK no --CERT enforce --debug

switch ($LASTEXITCODE) {
    0 { 
        Write-Host "✅ SSO login successful!" -ForegroundColor Green
        Write-Host "Browser session is now available for enterprise applications." -ForegroundColor Green
    }
    1 { 
        Write-Host "❌ Login failed - Check credentials or network connectivity" -ForegroundColor Red
        Write-Host "Review screenshots in ./screenshots/ for details" -ForegroundColor Yellow
    }
    2 { 
        Write-Host "⚠️ Application error - Check logs for details" -ForegroundColor Yellow
        Get-Content -Path ".\logs\webconnect-$(Get-Date -Format 'yyyyMMdd').log" -Tail 10
    }
}

# Clear password from memory
$PlainPassword = $null
[System.GC]::Collect()
```

**Expected Outcome**:
- Handles complex SSO authentication flow
- Manages domain/region selection
- Provides detailed logging for troubleshooting
- Maintains security with certificate enforcement

### Example 3: Automated Morning Routine

**Scenario**: Batch script that logs into multiple corporate systems at the start of the workday.

**Batch Script (morning-routine.bat)**:
```batch
@echo off
echo Starting morning login routine...

set USERNAME=john.doe
set DOMAIN=CORPORATE

echo.
echo [1/3] Logging into Corporate Portal...
WebConnect.exe --USR %USERNAME% --PSW %1 --URL https://portal.company.com --DOM %DOMAIN% --INCOGNITO yes --KIOSK no --CERT ignore
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Corporate Portal login failed
    goto :error
)

echo.
echo [2/3] Logging into HR System...
WebConnect.exe --USR %USERNAME% --PSW %1 --URL https://hr.company.com/login --DOM %DOMAIN% --INCOGNITO no --KIOSK no --CERT ignore
if %ERRORLEVEL% NEQ 0 (
    echo ❌ HR System login failed
    goto :error
)

echo.
echo [3/3] Logging into Project Management...
WebConnect.exe --USR %USERNAME% --PSW %1 --URL https://pm.company.com/auth --DOM %DOMAIN% --INCOGNITO no --KIOSK no --CERT ignore
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Project Management login failed
    goto :error
)

echo.
echo ✅ All systems logged in successfully!
echo Your workday is ready to begin.
goto :end

:error
echo.
echo ⚠️ Login routine incomplete. Check individual system logins.
echo Review screenshots in .\screenshots\ for details.

:end
pause
```

**Usage**: `morning-routine.bat "YourPassword"`

**Expected Outcome**:
- Sequential login to three different systems
- Error handling with specific failure reporting
- Multiple browser tabs/windows for different systems
- Time-saving automation for daily routine

---

## 🧪 Development and Testing Scenarios

### Example 4: Automated Testing Environment Setup

**Scenario**: Setting up test environment logins for QA automation.

**PowerShell Test Script**:
```powershell
# QA-Environment-Setup.ps1
param(
    [string]$Environment = "staging",
    [switch]$Parallel
)

# Environment configurations
$environments = @{
    "dev" = @{
        url = "https://dev.portal.com/login"
        user = "qa.tester"
        password = "DevPass123"
        domain = "DEV"
    }
    "staging" = @{
        url = "https://staging.portal.com/login"
        user = "stage.tester"
        password = "StagePass123"
        domain = "STAGING"
    }
    "uat" = @{
        url = "https://uat.portal.com/login"
        user = "uat.tester"
        password = "UATPass123"
        domain = "UAT"
    }
}

function Start-EnvironmentLogin {
    param($env, $config)
    
    Write-Host "Starting login for $env environment..." -ForegroundColor Cyan
    
    $startTime = Get-Date
    $process = Start-Process -FilePath "WebConnect.exe" -ArgumentList @(
        "--USR", $config.user,
        "--PSW", $config.password,
        "--URL", $config.url,
        "--DOM", $config.domain,
        "--INCOGNITO", "yes",
        "--KIOSK", "no",
        "--CERT", "ignore",
        "--debug"
    ) -Wait -PassThru -NoNewWindow
    
    $duration = (Get-Date) - $startTime
    
    $result = @{
        Environment = $env
        ExitCode = $process.ExitCode
        Duration = $duration
        Success = ($process.ExitCode -eq 0)
    }
    
    if ($result.Success) {
        Write-Host "✅ $env login successful (Duration: $($duration.TotalSeconds)s)" -ForegroundColor Green
    } else {
        Write-Host "❌ $env login failed (Exit Code: $($process.ExitCode))" -ForegroundColor Red
    }
    
    return $result
}

# Main execution
if ($Parallel) {
    # Parallel execution for faster setup
    $jobs = @()
    foreach ($env in $environments.Keys) {
        if ($Environment -eq "all" -or $Environment -eq $env) {
            $jobs += Start-Job -ScriptBlock ${function:Start-EnvironmentLogin} -ArgumentList $env, $environments[$env]
        }
    }
    
    $results = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job
} else {
    # Sequential execution
    $results = @()
    foreach ($env in $environments.Keys) {
        if ($Environment -eq "all" -or $Environment -eq $env) {
            $results += Start-EnvironmentLogin -env $env -config $environments[$env]
        }
    }
}

# Summary report
Write-Host "`n=== Login Summary ===" -ForegroundColor Yellow
$successful = ($results | Where-Object Success).Count
$total = $results.Count
Write-Host "Successful: $successful/$total environments" -ForegroundColor Green

$results | Format-Table Environment, Success, Duration, ExitCode -AutoSize
```

**Usage Examples**:
```powershell
# Login to staging environment only
.\QA-Environment-Setup.ps1 -Environment staging

# Login to all environments sequentially  
.\QA-Environment-Setup.ps1 -Environment all

# Login to all environments in parallel (faster)
.\QA-Environment-Setup.ps1 -Environment all -Parallel
```

**Expected Outcome**:
- Automated setup of multiple test environments
- Parallel or sequential execution options
- Detailed timing and success metrics
- Comprehensive error reporting

### Example 5: Continuous Integration Integration

**Scenario**: Using WebConnect in CI/CD pipelines for automated testing.

**Azure DevOps Pipeline YAML**:
```yaml
# azure-pipelines.yml
trigger:
- main
- develop

pool:
  vmImage: 'windows-latest'

variables:
  testEnvironment: 'staging'

stages:
- stage: Setup
  displayName: 'Environment Setup'
  jobs:
  - job: LoginSetup
    displayName: 'Setup Test Environment Logins'
    steps:
    - task: PowerShell@2
      displayName: 'Download WebConnect'
      inputs:
        targetType: 'inline'
        script: |
          # Download latest WebConnect release
          $release = Invoke-RestMethod -Uri "https://api.github.com/repos/MaskoFortwana/webconnect/releases/latest"
          $downloadUrl = $release.assets | Where-Object { $_.name -like "*win-x64.zip" } | Select-Object -First 1
          Invoke-WebRequest -Uri $downloadUrl.browser_download_url -OutFile "WebConnect.zip"
          Expand-Archive -Path "WebConnect.zip" -DestinationPath "." -Force

    - task: PowerShell@2
      displayName: 'Setup Test Environment'
      inputs:
        targetType: 'inline'
        script: |
          # Setup environment login
          $result = & .\WebConnect.exe --USR "$(testUsername)" --PSW "$(testPassword)" --URL "$(testUrl)" --DOM "$(testDomain)" --INCOGNITO yes --KIOSK no --CERT ignore --debug
          
          if ($LASTEXITCODE -eq 0) {
            Write-Host "##[section]✅ Environment setup successful"
            Write-Host "##vso[task.setvariable variable=loginSuccess]true"
          } else {
            Write-Host "##[error]❌ Environment setup failed with exit code $LASTEXITCODE"
            Write-Host "##vso[task.setvariable variable=loginSuccess]false"
            
            # Upload screenshots as artifacts for debugging
            if (Test-Path "screenshots") {
              Write-Host "##vso[artifact.upload containerfolder=screenshots;artifactname=login-screenshots]screenshots"
            }
            exit 1
          }
        env:
          testUsername: $(TEST_USERNAME)
          testPassword: $(TEST_PASSWORD)
          testUrl: $(TEST_URL)
          testDomain: $(TEST_DOMAIN)

- stage: Test
  displayName: 'Run Tests'
  dependsOn: Setup
  condition: succeeded()
  jobs:
  - job: RunTests
    displayName: 'Execute Test Suite'
    steps:
    - task: PowerShell@2
      displayName: 'Run Automated Tests'
      inputs:
        targetType: 'inline'
        script: |
          Write-Host "Running tests with pre-authenticated session..."
          # Your test execution code here
```

**GitHub Actions Workflow**:
```yaml
# .github/workflows/automated-testing.yml
name: Automated Testing with WebConnect

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]
  schedule:
    - cron: '0 6 * * 1-5'  # Weekdays at 6 AM

jobs:
  setup-and-test:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup WebConnect
      run: |
        # Download and extract WebConnect
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/MaskoFortwana/webconnect/releases/latest"
        $asset = $release.assets | Where-Object { $_.name -like "*win-x64.zip" }
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile "WebConnect.zip"
        Expand-Archive -Path "WebConnect.zip" -DestinationPath "tools" -Force
      
    - name: Authenticate Test Environment
      run: |
        $result = & .\tools\WebConnect.exe --USR "${{ secrets.TEST_USERNAME }}" --PSW "${{ secrets.TEST_PASSWORD }}" --URL "${{ vars.TEST_URL }}" --DOM "${{ vars.TEST_DOMAIN }}" --INCOGNITO yes --KIOSK no --CERT ignore
        
        if ($LASTEXITCODE -ne 0) {
          echo "::error::Authentication failed with exit code $LASTEXITCODE"
          exit 1
        }
        echo "::notice::Test environment authentication successful"
    
    - name: Upload Screenshots on Failure
      if: failure()
      uses: actions/upload-artifact@v3
      with:
        name: login-failure-screenshots
        path: screenshots/
        retention-days: 7
```

**Expected Outcome**:
- Seamless integration with CI/CD pipelines
- Automated environment setup for testing
- Artifact collection for debugging failures
- Reliable authentication for test execution

---

## 🤖 Automation and Scripting Scenarios

### Example 6: Data Extraction and Reporting

**Scenario**: Automated login followed by data extraction for reporting purposes.

**PowerShell Data Extraction Script**:
```powershell
# Data-Extraction-Automation.ps1
param(
    [Parameter(Mandatory)]
    [string]$ReportType,
    
    [string]$OutputPath = ".\reports",
    [switch]$ScheduledRun
)

# Configuration
$config = @{
    url = "https://reporting.company.com/login"
    username = "report.user"
    domain = "REPORTS"
    outputPath = $OutputPath
}

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path ".\logs\data-extraction.log" -Value $logMessage
}

function Start-AuthenticatedSession {
    param($config)
    
    Write-Log "Starting authentication process..."
    
    # Retrieve password from secure storage (example using Windows Credential Manager)
    try {
        $credential = Get-StoredCredential -Target "WebConnect-Reports"
        $password = $credential.GetNetworkCredential().Password
    } catch {
        Write-Log "Failed to retrieve stored credentials" "ERROR"
        $password = Read-Host -Prompt "Enter password for $($config.username)" -AsSecureString
        $password = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
        )
    }
    
    $authProcess = Start-Process -FilePath "WebConnect.exe" -ArgumentList @(
        "--USR", $config.username,
        "--PSW", $password,
        "--URL", $config.url,
        "--DOM", $config.domain,
        "--INCOGNITO", "no",  # Keep session for data extraction
        "--KIOSK", "no",
        "--CERT", "ignore"
    ) -Wait -PassThru -NoNewWindow
    
    # Clear password from memory
    $password = $null
    [System.GC]::Collect()
    
    if ($authProcess.ExitCode -eq 0) {
        Write-Log "Authentication successful"
        return $true
    } else {
        Write-Log "Authentication failed with exit code $($authProcess.ExitCode)" "ERROR"
        return $false
    }
}

function Start-DataExtraction {
    param($reportType, $outputPath)
    
    Write-Log "Starting data extraction for report type: $reportType"
    
    # Ensure output directory exists
    if (-not (Test-Path $outputPath)) {
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
        Write-Log "Created output directory: $outputPath"
    }
    
    # Here you would add your specific data extraction logic
    # This could involve additional Selenium automation, API calls, etc.
    # For this example, we'll simulate the process
    
    $reportFileName = "$reportType-$(Get-Date -Format 'yyyyMMdd-HHmmss').csv"
    $reportPath = Join-Path $outputPath $reportFileName
    
    Write-Log "Extracting data..."
    Start-Sleep 5  # Simulate extraction time
    
    # Simulate creating a report file
    "Timestamp,ReportType,Status,RecordCount" | Out-File -FilePath $reportPath
    "$(Get-Date),$reportType,Success,1250" | Add-Content -FilePath $reportPath
    
    Write-Log "Data extraction completed. Report saved to: $reportPath"
    return $reportPath
}

function Send-CompletionNotification {
    param($reportPath, $success)
    
    if ($success) {
        $subject = "Data Extraction Completed Successfully"
        $body = "Report generated: $reportPath`nTimestamp: $(Get-Date)"
    } else {
        $subject = "Data Extraction Failed"
        $body = "Data extraction process failed. Check logs for details.`nTimestamp: $(Get-Date)"
    }
    
    # Email notification (customize for your environment)
    try {
        # Send-MailMessage or other notification method
        Write-Log "Notification sent: $subject"
    } catch {
        Write-Log "Failed to send notification: $($_.Exception.Message)" "WARN"
    }
}

# Main execution
try {
    Write-Log "Starting automated data extraction process"
    Write-Log "Report Type: $ReportType"
    Write-Log "Output Path: $($config.outputPath)"
    
    # Step 1: Authenticate
    $authSuccess = Start-AuthenticatedSession -config $config
    
    if (-not $authSuccess) {
        throw "Authentication failed"
    }
    
    # Step 2: Extract data
    $reportPath = Start-DataExtraction -reportType $ReportType -outputPath $config.outputPath
    
    # Step 3: Cleanup and notification
    Write-Log "Process completed successfully"
    Send-CompletionNotification -reportPath $reportPath -success $true
    
} catch {
    Write-Log "Process failed: $($_.Exception.Message)" "ERROR"
    Send-CompletionNotification -reportPath $null -success $false
    exit 1
}
```

**Scheduled Task Setup**:
```powershell
# Create-Scheduled-Task.ps1
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-File C:\Scripts\Data-Extraction-Automation.ps1 -ReportType 'Daily' -ScheduledRun"
$trigger = New-ScheduledTaskTrigger -Daily -At 6:00AM
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId "DOMAIN\ServiceAccount" -LogonType Password

Register-ScheduledTask -TaskName "WebConnect-DataExtraction" -Action $action -Trigger $trigger -Settings $settings -Principal $principal
```

**Expected Outcome**:
- Automated login and data extraction
- Secure credential management
- Comprehensive logging and monitoring
- Error handling with notifications
- Scheduled execution capability

---

## 🔧 Integration Examples

### Example 7: Integration with Monitoring Systems

**Scenario**: Integrating WebConnect with monitoring and alerting systems.

**PowerShell Monitoring Integration**:
```powershell
# Monitoring-Integration.ps1
param(
    [string[]]$Systems = @("Portal", "HR", "Finance"),
    [string]$InfluxDBUrl = "http://localhost:8086",
    [string]$Database = "webconnect_metrics"
)

# InfluxDB integration functions
function Send-MetricToInfluxDB {
    param(
        [string]$Measurement,
        [hashtable]$Tags,
        [hashtable]$Fields,
        [string]$Timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    )
    
    # Build InfluxDB line protocol
    $tagString = ($Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ","
    $fieldString = ($Fields.GetEnumerator() | ForEach-Object { 
        if ($_.Value -is [string]) {
            "$($_.Key)=`"$($_.Value)`""
        } else {
            "$($_.Key)=$($_.Value)"
        }
    }) -join ","
    
    $lineProtocol = "$Measurement,$tagString $fieldString $Timestamp"
    
    try {
        Invoke-RestMethod -Uri "$InfluxDBUrl/write?db=$Database" -Method POST -Body $lineProtocol
        Write-Host "✅ Metric sent to InfluxDB: $Measurement" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to send metric to InfluxDB: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Test-SystemLogin {
    param(
        [string]$SystemName,
        [hashtable]$Config
    )
    
    $startTime = Get-Date
    
    Write-Host "Testing login for $SystemName..." -ForegroundColor Cyan
    
    $process = Start-Process -FilePath "WebConnect.exe" -ArgumentList @(
        "--USR", $Config.username,
        "--PSW", $Config.password,
        "--URL", $Config.url,
        "--DOM", $Config.domain,
        "--INCOGNITO", "yes",
        "--KIOSK", "no",
        "--CERT", "ignore"
    ) -Wait -PassThru -NoNewWindow
    
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    $success = ($process.ExitCode -eq 0)
    
    # Send metrics to InfluxDB
    Send-MetricToInfluxDB -Measurement "login_attempt" -Tags @{
        system = $SystemName
        environment = "production"
    } -Fields @{
        success = if ($success) { 1 } else { 0 }
        duration = $duration
        exit_code = $process.ExitCode
    }
    
    # Send to Prometheus (pushgateway)
    $prometheusMetrics = @"
# HELP webconnect_login_duration_seconds Time taken for login attempt
# TYPE webconnect_login_duration_seconds gauge
webconnect_login_duration_seconds{system="$SystemName",environment="production"} $duration

# HELP webconnect_login_success Login attempt success indicator
# TYPE webconnect_login_success gauge
webconnect_login_success{system="$SystemName",environment="production"} $(if ($success) { 1 } else { 0 })
"@
    
    try {
        Invoke-RestMethod -Uri "http://pushgateway:9091/metrics/job/webconnect/instance/$env:COMPUTERNAME" -Method POST -Body $prometheusMetrics
        Write-Host "✅ Metrics sent to Prometheus" -ForegroundColor Green
    } catch {
        Write-Host "⚠️ Failed to send metrics to Prometheus: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    return @{
        System = $SystemName
        Success = $success
        Duration = $duration
        ExitCode = $process.ExitCode
        Timestamp = $endTime
    }
}

# System configurations
$systemConfigs = @{
    "Portal" = @{
        url = "https://portal.company.com/login"
        username = "monitor.user"
        password = $env:PORTAL_PASSWORD
        domain = "CORPORATE"
    }
    "HR" = @{
        url = "https://hr.company.com/login"
        username = "monitor.user"
        password = $env:HR_PASSWORD
        domain = "CORPORATE"
    }
    "Finance" = @{
        url = "https://finance.company.com/login"
        username = "monitor.user"
        password = $env:FINANCE_PASSWORD
        domain = "CORPORATE"
    }
}

# Execute monitoring tests
$results = @()
foreach ($system in $Systems) {
    if ($systemConfigs.ContainsKey($system)) {
        $result = Test-SystemLogin -SystemName $system -Config $systemConfigs[$system]
        $results += $result
        
        # Slack notification on failure
        if (-not $result.Success) {
            $slackPayload = @{
                text = "🚨 WebConnect Login Failure"
                attachments = @(@{
                    color = "danger"
                    fields = @(
                        @{ title = "System"; value = $result.System; short = $true }
                        @{ title = "Exit Code"; value = $result.ExitCode; short = $true }
                        @{ title = "Duration"; value = "$([math]::Round($result.Duration, 2))s"; short = $true }
                        @{ title = "Timestamp"; value = $result.Timestamp.ToString(); short = $true }
                    )
                })
            } | ConvertTo-Json -Depth 3
            
            try {
                Invoke-RestMethod -Uri $env:SLACK_WEBHOOK_URL -Method POST -Body $slackPayload -ContentType "application/json"
            } catch {
                Write-Host "Failed to send Slack notification: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

# Summary
$successCount = ($results | Where-Object Success).Count
$totalCount = $results.Count
$successRate = if ($totalCount -gt 0) { [math]::Round(($successCount / $totalCount) * 100, 2) } else { 0 }

Write-Host "`n=== Monitoring Summary ===" -ForegroundColor Yellow
Write-Host "Success Rate: $successRate% ($successCount/$totalCount)" -ForegroundColor $(if ($successRate -ge 90) { "Green" } else { "Red" })

$results | Format-Table System, Success, Duration, ExitCode, Timestamp -AutoSize
```

**Expected Outcome**:
- Continuous monitoring of system availability
- Integration with InfluxDB and Prometheus
- Real-time alerting via Slack
- Comprehensive metrics collection

### Example 8: Selenium Test Suite Integration

**Scenario**: Using WebConnect to pre-authenticate sessions for Selenium test suites.

**C# Integration Example**:
```csharp
// WebConnectTestBase.cs
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class WebConnectTestBase
{
    protected IWebDriver Driver;
    protected string BaseUrl;
    protected bool IsAuthenticated;

    [TestInitialize]
    public virtual void Setup()
    {
        BaseUrl = Environment.GetEnvironmentVariable("TEST_BASE_URL") ?? "https://test.company.com";
        
        // Pre-authenticate using WebConnect
        AuthenticateWithWebConnect();
        
        // Initialize Selenium with the authenticated session
        InitializeSeleniumDriver();
    }

    private void AuthenticateWithWebConnect()
    {
        var username = Environment.GetEnvironmentVariable("TEST_USERNAME") ?? "test.user";
        var password = Environment.GetEnvironmentVariable("TEST_PASSWORD") ?? throw new InvalidOperationException("TEST_PASSWORD not set");
        var domain = Environment.GetEnvironmentVariable("TEST_DOMAIN") ?? "TEST";

        var webConnectPath = Path.Combine(TestContext.TestRunDirectory, "WebConnect.exe");
        
        var processInfo = new ProcessStartInfo
        {
            FileName = webConnectPath,
            Arguments = $"--USR {username} --PSW {password} --URL {BaseUrl}/login --DOM {domain} --INCOGNITO no --KIOSK no --CERT ignore",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
            process.WaitForExit(TimeSpan.FromMinutes(2));
            
            if (process.ExitCode == 0)
            {
                IsAuthenticated = true;
                Console.WriteLine("✅ Pre-authentication successful");
            }
            else
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"WebConnect authentication failed with exit code {process.ExitCode}: {error}");
            }
        }
    }

    private void InitializeSeleniumDriver()
    {
        var options = new ChromeOptions();
        
        // Use the same user data directory that WebConnect used
        var userDataDir = Path.Combine(Path.GetTempPath(), "WebConnect_UserData");
        options.AddArgument($"--user-data-dir={userDataDir}");
        
        // Additional options for test stability
        options.AddArgument("--disable-extensions");
        options.AddArgument("--disable-plugins");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        
        Driver = new ChromeDriver(options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        Driver.Manage().Window.Maximize();
    }

    [TestCleanup]
    public virtual void Cleanup()
    {
        Driver?.Quit();
        Driver?.Dispose();
    }

    public TestContext TestContext { get; set; }
}

// Example test class using the base
[TestClass]
public class DashboardTests : WebConnectTestBase
{
    [TestMethod]
    public void VerifyDashboardLoadsAfterAuthentication()
    {
        // Navigate to dashboard (should already be authenticated)
        Driver.Navigate().GoToUrl($"{BaseUrl}/dashboard");
        
        // Verify we're on the dashboard and not redirected to login
        Assert.IsTrue(IsAuthenticated, "Should be pre-authenticated");
        Assert.IsTrue(Driver.Url.Contains("/dashboard"), "Should be on dashboard page");
        
        // Verify dashboard elements are present
        var welcomeMessage = Driver.FindElement(By.CssSelector("[data-testid='welcome-message']"));
        Assert.IsNotNull(welcomeMessage, "Welcome message should be present");
        
        var navigationMenu = Driver.FindElement(By.CssSelector("[data-testid='nav-menu']"));
        Assert.IsNotNull(navigationMenu, "Navigation menu should be present");
    }

    [TestMethod]
    public void VerifyUserProfileAccess()
    {
        Driver.Navigate().GoToUrl($"{BaseUrl}/profile");
        
        // Should have access without additional authentication
        var profileHeader = Driver.FindElement(By.TagName("h1"));
        Assert.AreEqual("User Profile", profileHeader.Text);
        
        // Verify profile data is loaded
        var userNameField = Driver.FindElement(By.Id("username"));
        Assert.IsFalse(string.IsNullOrEmpty(userNameField.GetAttribute("value")));
    }
}
```

**PowerShell Test Runner**:
```powershell
# Run-Selenium-Tests.ps1
param(
    [string]$TestCategory = "Smoke",
    [string]$Environment = "staging"
)

# Setup environment variables based on target environment
switch ($Environment) {
    "staging" {
        $env:TEST_BASE_URL = "https://staging.company.com"
        $env:TEST_USERNAME = "staging.tester"
        $env:TEST_DOMAIN = "STAGING"
    }
    "production" {
        $env:TEST_BASE_URL = "https://company.com"
        $env:TEST_USERNAME = "prod.tester"
        $env:TEST_DOMAIN = "PROD"
    }
}

# Retrieve password from secure store
$env:TEST_PASSWORD = (Get-StoredCredential -Target "SeleniumTests-$Environment").GetNetworkCredential().Password

Write-Host "Running Selenium tests with WebConnect pre-authentication..." -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Test Category: $TestCategory" -ForegroundColor Yellow

# Copy WebConnect to test output directory
$testDir = ".\bin\Debug\net8.0"
Copy-Item "WebConnect.exe" -Destination $testDir -Force

# Run tests with MSTest
$testResults = & dotnet test --filter "TestCategory=$TestCategory" --logger "trx;LogFileName=TestResults.trx" --logger "console;verbosity=detailed"

# Process results
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ All tests passed!" -ForegroundColor Green
} else {
    Write-Host "❌ Some tests failed. Check TestResults.trx for details." -ForegroundColor Red
    
    # Extract and display failed tests
    $trxFile = Get-ChildItem -Path "TestResults" -Filter "*.trx" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($trxFile) {
        [xml]$testResultsXml = Get-Content $trxFile.FullName
        $failedTests = $testResultsXml.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq "Failed" }
        
        Write-Host "`nFailed Tests:" -ForegroundColor Red
        foreach ($test in $failedTests) {
            Write-Host "  - $($test.testName): $($test.Output.ErrorInfo.Message)" -ForegroundColor Red
        }
    }
}

# Cleanup
Remove-Item $env:TEST_PASSWORD -ErrorAction SilentlyContinue
```

**Expected Outcome**:
- Seamless integration with existing Selenium test suites
- Pre-authenticated sessions eliminate login steps in tests
- Faster test execution with reliable authentication
- Comprehensive test reporting and failure analysis

---

## ⚙️ Advanced Configuration Examples

### Example 9: Custom Configuration for Different Environments

**Environment-Specific Configuration Files**:

**appsettings.Development.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "WebConnect": {
    "DefaultTimeout": 60,
    "MaxRetryAttempts": 5,
    "ScreenshotOnError": true,
    "BrowserOptions": {
      "PageLoadTimeout": 60,
      "ImplicitWaitTimeout": 15,
      "AdditionalOptions": [
        "--disable-web-security",
        "--allow-running-insecure-content"
      ]
    }
  }
}
```

**appsettings.Production.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Error"
    }
  },
  "WebConnect": {
    "DefaultTimeout": 30,
    "MaxRetryAttempts": 3,
    "ScreenshotOnError": false,
    "BrowserOptions": {
      "PageLoadTimeout": 30,
      "ImplicitWaitTimeout": 10,
      "AdditionalOptions": [
        "--disable-extensions",
        "--disable-plugins"
      ]
    }
  }
}
```

### Example 10: Performance Optimization Configuration

**High-Performance Configuration**:
```json
{
  "WebConnect": {
    "DefaultTimeout": 15,
    "MaxRetryAttempts": 2,
    "ScreenshotOnError": false,
    "BrowserOptions": {
      "PageLoadTimeout": 15,
      "ImplicitWaitTimeout": 5,
      "AdditionalOptions": [
        "--memory-pressure-off",
        "--disable-background-timer-throttling",
        "--disable-features=VizDisplayCompositor",
        "--disable-ipc-flooding-protection"
      ]
    },
    "LoginDetection": {
      "MaxDetectionAttempts": 2,
      "DetectionTimeout": 5,
      "RetryDelay": 500
    },
    "Authentication": {
      "TypingDelay": 25,
      "SubmissionTimeout": 10,
      "VerificationTimeout": 5
    }
  }
}
```

---

## 🛠️ Troubleshooting and Debugging Examples

### Example 11: Comprehensive Debugging Setup

**Debug Configuration Script**:
```powershell
# Debug-WebConnect.ps1
param(
    [string]$Url,
    [string]$Username,
    [string]$Password,
    [string]$Domain = "DEFAULT",
    [switch]$Verbose,
    [switch]$CaptureNetwork
)

function Enable-DebugLogging {
    # Set debug environment variables
    $env:WEBCONNECT_LOG_LEVEL = "Debug"
    $env:WEBCONNECT_SCREENSHOT_DIR = ".\debug-screenshots"
    
    # Ensure debug directories exist
    @("debug-screenshots", "debug-logs", "network-logs") | ForEach-Object {
        if (-not (Test-Path $_)) {
            New-Item -ItemType Directory -Path $_ -Force | Out-Null
        }
    }
}

function Start-NetworkCapture {
    if ($CaptureNetwork) {
        Write-Host "Starting network capture..." -ForegroundColor Cyan
        
        # Start WebConnect with network logging
        $webConnectArgs = @(
            "--enable-logging",
            "--log-level=0",
            "--enable-net-log",
            "--net-log-capture-mode=Everything",
            "--log-file=.\network-logs\webconnect-debug.log"
        )
        
        return $webConnectArgs
    }
    return @()
}

function Test-Prerequisites {
    Write-Host "=== WebConnect Debug Prerequisites ===" -ForegroundColor Yellow
    
    # Check WebConnect installation
    $webConnectPaths = @(
        "${env:ProgramFiles}\Google\Chrome\Application\webconnect.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\webconnect.exe",
        "${env:LOCALAPPDATA}\Google\Chrome\Application\webconnect.exe"
    )
    
    $webConnectFound = $false
    foreach ($path in $webConnectPaths) {
        if (Test-Path $path) {
            $version = (Get-Item $path).VersionInfo.ProductVersion
            Write-Host "✅ WebConnect found: $path (Version: $version)" -ForegroundColor Green
            $webConnectFound = $true
            break
        }
    }
    
    if (-not $webConnectFound) {
        Write-Host "❌ WebConnect not found in standard locations" -ForegroundColor Red
        return $false
    }
    
    # Check WebConnect
    if (Test-Path "WebConnect.exe") {
        Write-Host "✅ WebConnect.exe found" -ForegroundColor Green
    } else {
        Write-Host "❌ WebConnect.exe not found in current directory" -ForegroundColor Red
        return $false
    }
    
    # Check network connectivity
    try {
        $response = Invoke-WebRequest -Uri $Url -Method Head -TimeoutSec 10 -UseBasicParsing
        Write-Host "✅ Target URL accessible (Status: $($response.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "⚠️ Target URL test failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    return $true
}

function Start-DebugSession {
    param($url, $username, $password, $domain, $networkArgs)
    
    Write-Host "`n=== Starting Debug Session ===" -ForegroundColor Yellow
    Write-Host "URL: $url" -ForegroundColor Cyan
    Write-Host "Username: $username" -ForegroundColor Cyan
    Write-Host "Domain: $domain" -ForegroundColor Cyan
    Write-Host "Timestamp: $(Get-Date)" -ForegroundColor Cyan
    
    $arguments = @(
        "--USR", $username,
        "--PSW", $password,
        "--URL", $url,
        "--DOM", $domain,
        "--INCOGNITO", "no",  # Don't use incognito for debugging
        "--KIOSK", "no",
        "--CERT", "ignore",
        "--debug"
    )
    
    if ($Verbose) {
        Write-Host "`nExecuting command:" -ForegroundColor Yellow
        Write-Host "WebConnect.exe $($arguments -join ' ')" -ForegroundColor White
    }
    
    $startTime = Get-Date
    $process = Start-Process -FilePath "WebConnect.exe" -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    Write-Host "`n=== Debug Session Results ===" -ForegroundColor Yellow
    Write-Host "Exit Code: $($process.ExitCode)" -ForegroundColor $(if ($process.ExitCode -eq 0) { "Green" } else { "Red" })
    Write-Host "Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Cyan
    Write-Host "Peak Memory: $([math]::Round($process.PeakWorkingSet64/1MB, 2)) MB" -ForegroundColor Cyan
    
    return $process.ExitCode
}

function Analyze-DebugResults {
    param($exitCode)
    
    Write-Host "`n=== Debug Analysis ===" -ForegroundColor Yellow
    
    # Analyze log files
    $logFiles = Get-ChildItem -Path ".\logs" -Filter "*.log" | Sort-Object LastWriteTime -Descending
    if ($logFiles) {
        $latestLog = $logFiles[0]
        Write-Host "Latest log file: $($latestLog.Name)" -ForegroundColor Cyan
        
        $errors = Select-String -Path $latestLog.FullName -Pattern "ERROR|Exception|Failed"
        if ($errors) {
            Write-Host "Errors found in log:" -ForegroundColor Red
            $errors | ForEach-Object { Write-Host "  $($_.Line)" -ForegroundColor Red }
        }
    }
    
    # Analyze screenshots
    $screenshots = Get-ChildItem -Path ".\debug-screenshots" -Filter "*.png" | Sort-Object LastWriteTime -Descending
    if ($screenshots) {
        Write-Host "Screenshots captured: $($screenshots.Count)" -ForegroundColor Cyan
        Write-Host "Latest screenshot: $($screenshots[0].Name)" -ForegroundColor Cyan
        
        # Open latest screenshot for review
        if ($screenshots.Count -gt 0) {
            Start-Process $screenshots[0].FullName
        }
    }
    
    # Provide recommendations based on exit code
    switch ($exitCode) {
        0 { Write-Host "✅ Success: Login completed successfully" -ForegroundColor Green }
        1 { 
            Write-Host "❌ Login Failed: Check credentials and screenshots" -ForegroundColor Red
            Write-Host "Recommendations:" -ForegroundColor Yellow
            Write-Host "  - Verify credentials are correct" -ForegroundColor Yellow
            Write-Host "  - Check if CAPTCHA is required" -ForegroundColor Yellow
            Write-Host "  - Review form detection in logs" -ForegroundColor Yellow
        }
        2 { 
            Write-Host "❌ Application Error: Check system configuration" -ForegroundColor Red
            Write-Host "Recommendations:" -ForegroundColor Yellow
            Write-Host "  - Verify WebConnect is properly installed" -ForegroundColor Yellow
            Write-Host "  - Check network connectivity" -ForegroundColor Yellow
            Write-Host "  - Review error logs for specific issues" -ForegroundColor Yellow
        }
        default { Write-Host "❌ Unexpected exit code: $exitCode" -ForegroundColor Red }
    }
}

# Main execution
try {
    Enable-DebugLogging
    
    if (-not (Test-Prerequisites)) {
        Write-Host "Prerequisites check failed. Cannot continue." -ForegroundColor Red
        exit 1
    }
    
    $networkArgs = Start-NetworkCapture
    $exitCode = Start-DebugSession -url $Url -username $Username -password $Password -domain $Domain -networkArgs $networkArgs
    
    Analyze-DebugResults -exitCode $exitCode
    
    Write-Host "`n=== Debug Session Complete ===" -ForegroundColor Green
    Write-Host "Review the generated logs and screenshots for detailed analysis." -ForegroundColor Cyan
    
} catch {
    Write-Host "Debug session failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    # Cleanup sensitive environment variables
    Remove-Item Env:WEBCONNECT_LOG_LEVEL -ErrorAction SilentlyContinue
}
```

**Usage**:
```powershell
.\Debug-WebConnect.ps1 -Url "https://portal.company.com/login" -Username "test.user" -Password "TestPass123" -Domain "TEST" -Verbose -CaptureNetwork
```

---

## 💡 Best Practices and Tips

### Security Best Practices

1. **Credential Management**:
   ```powershell
   # Use Windows Credential Manager
   cmdkey /add:WebConnect-Production /user:prod.user /pass:SecurePassword
   
   # Retrieve in scripts
   $credential = Get-StoredCredential -Target "WebConnect-Production"
   ```

2. **Environment Separation**:
   ```powershell
   # Use environment-specific configurations
   $env:ENVIRONMENT = "Production"
   WebConnect.exe --USR $prodUser --PSW $prodPass --URL $prodUrl --DOM $prodDomain --INCOGNITO yes --KIOSK no --CERT enforce
   ```

3. **Logging Security**:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "WebConnect.Services.LoginPerformer": "Warning"
       }
     }
   }
   ```

### Performance Optimization Tips

1. **Batch Operations**:
   ```powershell
   # Parallel execution for multiple systems
   $systems | ForEach-Object -Parallel {
       & WebConnect.exe --USR $_.user --PSW $_.pass --URL $_.url --DOM $_.domain --INCOGNITO yes --KIOSK no --CERT ignore
   }
   ```

2. **Resource Management**:
   ```powershell
   # Monitor and cleanup WebConnect processes
   Get-Process webconnect | Where-Object { $_.StartTime -lt (Get-Date).AddHours(-2) } | Stop-Process -Force
   ```

3. **Configuration Tuning**:
   ```json
   {
     "WebConnect": {
       "DefaultTimeout": 20,
       "Authentication": {
         "TypingDelay": 50
       }
     }
   }
   ```

### Maintenance and Monitoring

1. **Regular Health Checks**:
   ```powershell
   # Daily health check script
   $result = & WebConnect.exe --USR $monitorUser --PSW $monitorPass --URL $healthUrl --DOM $domain --INCOGNITO yes --KIOSK no --CERT ignore
   
   if ($LASTEXITCODE -ne 0) {
       Send-Alert -Message "WebConnect health check failed" -Severity "High"
   }
   ```

2. **Log Rotation**:
   ```powershell
   # Archive old logs
   Get-ChildItem .\logs\*.log | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Compress-Archive -DestinationPath "archived-logs-$(Get-Date -Format 'yyyyMM').zip"
   ```

3. **Version Management**:
   ```powershell
   # Check for updates
   $currentVersion = & WebConnect.exe --version
   $latestRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/MaskoFortwana/webconnect/releases/latest"
   
   if ($latestRelease.tag_name -ne $currentVersion) {
       Write-Host "New version available: $($latestRelease.tag_name)"
   }
   ```

---

## 📞 Support and Additional Resources

### Getting Help

- **Documentation**: [Full Documentation](../README.md)
- **Command Reference**: [Command-line Reference](command-line-reference.md)
- **Troubleshooting**: [Configuration and Troubleshooting Guide](configuration-troubleshooting.md)
- **GitHub Issues**: [Report Issues](https://github.com/MaskoFortwana/webconnect/issues)

### Community Examples

Visit our [GitHub Discussions](https://github.com/MaskoFortwana/webconnect/discussions) for community-contributed examples and use cases.

### Contributing Examples

If you have additional usage examples or improvements to existing ones, please submit a pull request or create an issue with the "documentation" label.

---

*This usage examples guide is regularly updated with new scenarios and best practices. Last updated: November 2024* 