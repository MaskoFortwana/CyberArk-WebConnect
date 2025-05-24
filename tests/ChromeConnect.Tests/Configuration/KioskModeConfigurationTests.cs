using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using ChromeConnect.Core;
using ChromeConnect.Services;
using ChromeConnect.Models;
using Moq;
using System.Threading.Tasks;
using OpenQA.Selenium;

namespace ChromeConnect.Tests.Configuration;

[TestClass]
public class KioskModeConfigurationTests
{
    private Mock<IWebDriver>? _mockDriver;
    private ILoggerFactory? _loggerFactory;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _mockDriver = new Mock<IWebDriver>();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _loggerFactory?.Dispose();
    }

    [TestMethod]
    public void KioskMode_SetToNo_ShouldBeFalse()
    {
        // Arrange
        var commandLineOptions = new CommandLineOptions
        {
            Url = "http://test.com",
            Username = "user",
            Password = "pass",
            Domain = "domain",
            KioskString = "no", // Explicitly "no"
            IncognitoString = "no",
            CertString = "ignore"
        };

        // Act
        var kioskMode = CommandLineOptions.YesNoParser(commandLineOptions.KioskString);

        // Assert
        Assert.IsFalse(kioskMode, "Kiosk mode should be false when set to 'no'");
    }

    [TestMethod]
    public void KioskMode_SetToYes_ShouldBeTrue()
    {
        // Arrange
        var commandLineOptions = new CommandLineOptions
        {
            Url = "http://test.com",
            Username = "user",
            Password = "pass",
            Domain = "domain",
            KioskString = "yes", // Explicitly "yes"
            IncognitoString = "no",
            CertString = "ignore"
        };

        // Act
        var kioskMode = CommandLineOptions.YesNoParser(commandLineOptions.KioskString);

        // Assert
        Assert.IsTrue(kioskMode, "Kiosk mode should be true when set to 'yes'");
    }

    [TestMethod]
    public void KioskMode_SetToInvalid_ShouldBeFalse()
    {
        // Arrange (YesNoParser defaults to false for invalid strings)
        var commandLineOptions = new CommandLineOptions
        {
            Url = "http://test.com",
            Username = "user",
            Password = "pass",
            Domain = "domain",
            KioskString = "maybe", // Invalid value, should default to false
            IncognitoString = "no",
            CertString = "ignore"
        };

        // Act
        var kioskMode = CommandLineOptions.YesNoParser(commandLineOptions.KioskString);

        // Assert
        Assert.IsFalse(kioskMode, "Kiosk mode should be false for invalid values (default behavior)");
    }

    [TestMethod]
    public void IncognitoMode_SetToYes_ShouldBeTrue()
    {
        // Arrange
        var commandLineOptions = new CommandLineOptions
        {
            Url = "http://test.com",
            Username = "user",
            Password = "pass",
            Domain = "domain",
            KioskString = "no",
            IncognitoString = "yes", // Explicitly "yes"
            CertString = "ignore"
        };

        // Act
        var incognitoMode = CommandLineOptions.YesNoParser(commandLineOptions.IncognitoString);

        // Assert
        Assert.IsTrue(incognitoMode, "Incognito mode should be true when set to 'yes'");
    }

    [TestMethod]
    public void IgnoreCertificates_SetToIgnore_ShouldBeTrue()
    {
        // Arrange
        var commandLineOptions = new CommandLineOptions
        {
            Url = "http://test.com",
            Username = "user",
            Password = "pass",
            Domain = "domain",
            KioskString = "no",
            IncognitoString = "no",
            CertString = "ignore" // Should result in true for ignoring certificates
        };

        // Act
        var ignoreCerts = commandLineOptions.CertString.ToLower() == "ignore";

        // Assert
        Assert.IsTrue(ignoreCerts, "Certificate ignoring should be true when set to 'ignore'");
    }
} 