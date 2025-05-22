using System;
using CommandLine;

namespace ChromeConnect.Core;

public class CommandLineOptions
{
    [Option("USR", Required = true, HelpText = "Username for login form")]
    public string Username { get; set; } = "";

    [Option("PSW", Required = true, HelpText = "Password (masked in logs)")]
    public string Password { get; set; } = "";

    [Option("URL", Required = true, HelpText = "Target login page")]
    public string Url { get; set; } = "";

    [Option("DOM", Required = true, HelpText = "Domain or tenant identifier")]
    public string Domain { get; set; } = "";

    [Option("INCOGNITO", Required = true, 
        HelpText = "Adds --incognito when yes (valid values: yes, no)")]
    public bool Incognito { get; set; } = false;

    // Custom parser for yes/no values
    public static bool YesNoParser(string value) => value?.ToLower() == "yes";

    // Custom setter for INCOGNITO
    public void SetIncognito(string value) => Incognito = YesNoParser(value);

    [Option("KIOSK", Required = true, 
        HelpText = "Adds --kiosk when yes (valid values: yes, no)")]
    public bool Kiosk { get; set; } = false;

    // Custom setter for KIOSK
    public void SetKiosk(string value) => Kiosk = YesNoParser(value);

    [Option("CERT", Required = true, 
        HelpText = "Adds --ignore-certificate-errors when ignore (valid values: ignore, enforce)")]
    public bool IgnoreCertErrors { get; set; } = false;

    // Custom setter for CERT
    public void SetCert(string value) => IgnoreCertErrors = value?.ToLower() == "ignore";
}
