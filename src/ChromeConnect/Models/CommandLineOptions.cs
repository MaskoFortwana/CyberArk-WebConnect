using System;
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

    /// <summary>
    /// Gets or sets a value indicating whether to run the browser in incognito mode.
    /// Uses --incognito Chrome flag when set to true.
    /// </summary>
    [Option("INCOGNITO", Required = true, 
        HelpText = "Adds --incognito when yes (valid values: yes, no)")]
    public bool Incognito { get; set; } = false;

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
    public void SetIncognito(string value) => Incognito = YesNoParser(value);

    /// <summary>
    /// Gets or sets a value indicating whether to run the browser in kiosk mode.
    /// Uses --kiosk Chrome flag when set to true.
    /// </summary>
    [Option("KIOSK", Required = true, 
        HelpText = "Adds --kiosk when yes (valid values: yes, no)")]
    public bool Kiosk { get; set; } = false;

    /// <summary>
    /// Sets the Kiosk property based on a string value.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    public void SetKiosk(string value) => Kiosk = YesNoParser(value);

    /// <summary>
    /// Gets or sets a value indicating whether to ignore certificate errors.
    /// Uses --ignore-certificate-errors Chrome flag when set to true.
    /// </summary>
    [Option("CERT", Required = true, 
        HelpText = "Adds --ignore-certificate-errors when ignore (valid values: ignore, enforce)")]
    public bool IgnoreCertErrors { get; set; } = false;

    /// <summary>
    /// Sets the IgnoreCertErrors property based on a string value.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    public void SetCert(string value) => IgnoreCertErrors = value?.ToLower() == "ignore";

    /// <summary>
    /// Gets or sets a value indicating whether to display version information.
    /// </summary>
    [Option("version", HelpText = "Display version information")]
    public bool ShowVersion { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable debug logging.
    /// </summary>
    [Option("debug", HelpText = "Enable debug logging")]
    public bool Debug { get; set; } = false;
} 