using System;
using System.Collections.Generic;
using CommandLine;
using System.ComponentModel.DataAnnotations;

namespace ChromeConnect.Models;

/// <summary>
/// Represents the command-line options for the ChromeConnect application.
/// This class maps directly to the command-line arguments used in the original WebConnect Python application.
/// </summary>
public class CommandLineOptions
{
    /// <summary>
    /// Gets or sets the username for the login form.
    /// </summary>
    [Option("USR", Required = true, HelpText = "Username for login form")]
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password for the login form. This value is masked in logs for security.
    /// </summary>
    [Option("PSW", Required = true, HelpText = "Password (masked in logs)")]
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target login page URL.
    /// </summary>
    [Option("URL", Required = true, HelpText = "Target login page")]
    [Required(ErrorMessage = "URL is required")]
    [Url(ErrorMessage = "URL must be a valid URL format")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the domain or tenant identifier.
    /// </summary>
    [Option("DOM", Required = true, HelpText = "Domain or tenant identifier")]
    [Required(ErrorMessage = "Domain is required")]
    public string Domain { get; set; } = string.Empty;

    // Private fields to store the actual boolean values
    private bool _incognito = false;
    private bool _kiosk = false;
    private bool _ignoreCertErrors = false;

    /// <summary>
    /// Gets or sets the incognito mode string value. Accepts "yes" or "no".
    /// </summary>
    [Option("INCOGNITO", Required = true, 
        HelpText = "Adds --incognito when yes (valid values: yes, no)")]
    public string IncognitoString 
    { 
        get => _incognito ? "yes" : "no";
        set => _incognito = YesNoParser(value);
    }

    /// <summary>
    /// Gets the boolean value for incognito mode.
    /// </summary>
    public bool Incognito => _incognito;

    /// <summary>
    /// Gets or sets the kiosk mode string value. Accepts "yes" or "no".
    /// </summary>
    [Option("KIOSK", Required = true, 
        HelpText = "Adds --kiosk when yes (valid values: yes, no)")]
    public string KioskString 
    { 
        get => _kiosk ? "yes" : "no";
        set => _kiosk = YesNoParser(value);
    }

    /// <summary>
    /// Gets the boolean value for kiosk mode.
    /// </summary>
    public bool Kiosk => _kiosk;

    /// <summary>
    /// Gets or sets the certificate handling string value. Accepts "ignore" or "enforce".
    /// </summary>
    [Option("CERT", Required = true, 
        HelpText = "Adds --ignore-certificate-errors when ignore (valid values: ignore, enforce)")]
    public string CertString 
    { 
        get => _ignoreCertErrors ? "ignore" : "enforce";
        set => _ignoreCertErrors = value?.ToLower() == "ignore";
    }

    /// <summary>
    /// Gets the boolean value for ignoring certificate errors.
    /// </summary>
    public bool IgnoreCertErrors => _ignoreCertErrors;

    /// <summary>
    /// Parses yes/no values from command-line arguments.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <returns>True if the value is "yes" (case-insensitive), otherwise false.</returns>
    public static bool YesNoParser(string value) => value?.ToLower() == "yes";

    /// <summary>
    /// Sets the Incognito property based on a string value.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    public void SetIncognito(string value) => _incognito = YesNoParser(value);

    /// <summary>
    /// Sets the Kiosk property based on a string value.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    public void SetKiosk(string value) => _kiosk = YesNoParser(value);

    /// <summary>
    /// Sets the IgnoreCertErrors property based on a string value.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    public void SetCert(string value) => _ignoreCertErrors = value?.ToLower() == "ignore";

    /// <summary>
    /// Gets or sets a value indicating whether to display version information.
    /// </summary>
    [Option("version", HelpText = "Display version information and deployment requirements")]
    public bool ShowVersion { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable debug logging.
    /// </summary>
    [Option("debug", HelpText = "Enable debug logging (logs written to Windows temp folder)")]
    public bool Debug { get; set; } = false;

    /// <summary>
    /// Gets comprehensive help text that includes deployment requirements and parameter descriptions.
    /// </summary>
    /// <returns>Formatted help text string</returns>
    public static string GetExtendedHelpText()
    {
        return @"
ChromeConnect - Single Executable Browser Automation Tool

DEPLOYMENT REQUIREMENTS:
  • ChromeDriver.exe must be placed in the same folder as ChromeConnect.exe
  • No additional configuration files required (embedded configuration)
  • Logs are automatically written to Windows temp folder
  • Compatible with Windows 10/11 (x64)

REQUIRED PARAMETERS:
  --USR <username>     Username for login form (supports domain\user format)
  --PSW <password>     Password for authentication (masked in logs)
  --URL <url>          Target login page URL (must include https://)
  --DOM <domain>       Domain or tenant identifier
  --INCOGNITO <yes|no> Use incognito/private browsing mode
  --KIOSK <yes|no>     Use kiosk mode (fullscreen, no UI)
  --CERT <ignore|enforce> Certificate validation handling

OPTIONAL PARAMETERS:
  --version            Display version and deployment information
  --debug              Enable debug logging (detailed output)

EXAMPLES:
  ChromeConnect.exe --USR john.doe --PSW mypass123 --URL https://login.company.com --DOM company --INCOGNITO no --KIOSK no --CERT enforce
  ChromeConnect.exe --USR admin@domain.com --PSW secret --URL https://portal.example.com --DOM example --INCOGNITO yes --KIOSK yes --CERT ignore --debug

LOG LOCATION:
  Logs are written to: %TEMP%\ChromeConnect\
  Screenshots (on error): %TEMP%\ChromeConnect\screenshots\

EXIT CODES:
  0 - Success
  1 - Login failure (invalid credentials, form not found)
  2 - System error (browser launch, network issues)
  3 - Verification uncertain (manual inspection required)
  4 - Configuration error (missing ChromeDriver, invalid parameters)

For more information, visit: https://github.com/your-repo/ChromeConnect
";
    }

    /// <summary>
    /// Validates the command-line options and returns any validation errors.
    /// </summary>
    /// <returns>List of validation error messages, empty if valid</returns>
    public List<string> ValidateOptions()
    {
        var errors = new List<string>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(Username))
            errors.Add("Username (--USR) is required and cannot be empty");

        if (string.IsNullOrWhiteSpace(Password))
            errors.Add("Password (--PSW) is required and cannot be empty");

        if (string.IsNullOrWhiteSpace(Url))
            errors.Add("URL (--URL) is required and cannot be empty");
        else if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            errors.Add("URL (--URL) must be a valid HTTP or HTTPS URL");

        if (string.IsNullOrWhiteSpace(Domain))
            errors.Add("Domain (--DOM) is required and cannot be empty");

        return errors;
    }
} 