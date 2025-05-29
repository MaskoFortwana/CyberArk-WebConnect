using WebConnect.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WebConnect.Tests.Configuration;

[TestClass]
public class CommandLineOptionsTests
{
    [TestMethod]
    public void YesNoParser_ShouldParseYesAsTrue()
    {
        // Act
        var result = CommandLineOptions.YesNoParser("yes");

        // Assert
        Assert.IsTrue(result, "YesNoParser should return true for 'yes'");
    }

    [TestMethod]
    public void YesNoParser_ShouldParseNoAsFalse()
    {
        // Act
        var result = CommandLineOptions.YesNoParser("no");

        // Assert
        Assert.IsFalse(result, "YesNoParser should return false for 'no'");
    }

    [TestMethod]
    public void YesNoParser_ShouldBeCaseInsensitive()
    {
        // Act & Assert
        Assert.IsTrue(CommandLineOptions.YesNoParser("YES"), "Should handle uppercase YES");
        Assert.IsTrue(CommandLineOptions.YesNoParser("Yes"), "Should handle mixed case Yes");
        Assert.IsTrue(CommandLineOptions.YesNoParser("yEs"), "Should handle mixed case yEs");
        
        Assert.IsFalse(CommandLineOptions.YesNoParser("NO"), "Should handle uppercase NO");
        Assert.IsFalse(CommandLineOptions.YesNoParser("No"), "Should handle mixed case No");
        Assert.IsFalse(CommandLineOptions.YesNoParser("nO"), "Should handle mixed case nO");
    }

    [TestMethod]
    public void YesNoParser_ShouldHandleNullAndEmpty()
    {
        // Act & Assert
        Assert.IsFalse(CommandLineOptions.YesNoParser(null), "Should return false for null");
        Assert.IsFalse(CommandLineOptions.YesNoParser(""), "Should return false for empty string");
        Assert.IsFalse(CommandLineOptions.YesNoParser("   "), "Should return false for whitespace");
    }

    [TestMethod]
    public void YesNoParser_ShouldHandleInvalidValues()
    {
        // Act & Assert
        Assert.IsFalse(CommandLineOptions.YesNoParser("true"), "Should return false for 'true'");
        Assert.IsFalse(CommandLineOptions.YesNoParser("false"), "Should return false for 'false'");
        Assert.IsFalse(CommandLineOptions.YesNoParser("1"), "Should return false for '1'");
        Assert.IsFalse(CommandLineOptions.YesNoParser("0"), "Should return false for '0'");
        Assert.IsFalse(CommandLineOptions.YesNoParser("invalid"), "Should return false for invalid input");
    }

    [TestMethod]
    public void KioskString_ShouldSetAndGetCorrectly()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Act & Assert - Test "no" value
        options.KioskString = "no";
        Assert.AreEqual("no", options.KioskString, "KioskString should return 'no'");
        Assert.IsFalse(options.Kiosk, "Kiosk boolean should be false when set to 'no'");

        // Act & Assert - Test "yes" value
        options.KioskString = "yes";
        Assert.AreEqual("yes", options.KioskString, "KioskString should return 'yes'");
        Assert.IsTrue(options.Kiosk, "Kiosk boolean should be true when set to 'yes'");
    }

    [TestMethod]
    public void IncognitoString_ShouldSetAndGetCorrectly()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Act & Assert - Test "no" value
        options.IncognitoString = "no";
        Assert.AreEqual("no", options.IncognitoString, "IncognitoString should return 'no'");
        Assert.IsFalse(options.Incognito, "Incognito boolean should be false when set to 'no'");

        // Act & Assert - Test "yes" value
        options.IncognitoString = "yes";
        Assert.AreEqual("yes", options.IncognitoString, "IncognitoString should return 'yes'");
        Assert.IsTrue(options.Incognito, "Incognito boolean should be true when set to 'yes'");
    }

    [TestMethod]
    public void CertString_ShouldSetAndGetCorrectly()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Act & Assert - Test "enforce" value
        options.CertString = "enforce";
        Assert.AreEqual("enforce", options.CertString, "CertString should return 'enforce'");
        Assert.IsFalse(options.IgnoreCertErrors, "IgnoreCertErrors should be false when set to 'enforce'");

        // Act & Assert - Test "ignore" value
        options.CertString = "ignore";
        Assert.AreEqual("ignore", options.CertString, "CertString should return 'ignore'");
        Assert.IsTrue(options.IgnoreCertErrors, "IgnoreCertErrors should be true when set to 'ignore'");
    }

    [TestMethod]
    public void CertString_ShouldBeCaseInsensitive()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Act & Assert - Test case variations
        options.CertString = "IGNORE";
        Assert.IsTrue(options.IgnoreCertErrors, "Should handle uppercase IGNORE");

        options.CertString = "Ignore";
        Assert.IsTrue(options.IgnoreCertErrors, "Should handle mixed case Ignore");

        options.CertString = "ENFORCE";
        Assert.IsFalse(options.IgnoreCertErrors, "Should handle uppercase ENFORCE");

        options.CertString = "Enforce";
        Assert.IsFalse(options.IgnoreCertErrors, "Should handle mixed case Enforce");
    }

    [TestMethod]
    public void SetKiosk_ShouldWorkCorrectly()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Act & Assert
        options.SetKiosk("no");
        Assert.IsFalse(options.Kiosk, "SetKiosk('no') should set Kiosk to false");
        Assert.AreEqual("no", options.KioskString, "KioskString should reflect the set value");

        options.SetKiosk("yes");
        Assert.IsTrue(options.Kiosk, "SetKiosk('yes') should set Kiosk to true");
        Assert.AreEqual("yes", options.KioskString, "KioskString should reflect the set value");
    }

    [TestMethod]
    public void SetIncognito_ShouldWorkCorrectly()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Act & Assert
        options.SetIncognito("no");
        Assert.IsFalse(options.Incognito, "SetIncognito('no') should set Incognito to false");
        Assert.AreEqual("no", options.IncognitoString, "IncognitoString should reflect the set value");

        options.SetIncognito("yes");
        Assert.IsTrue(options.Incognito, "SetIncognito('yes') should set Incognito to true");
        Assert.AreEqual("yes", options.IncognitoString, "IncognitoString should reflect the set value");
    }

    [TestMethod]
    public void SetCert_ShouldWorkCorrectly()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Act & Assert
        options.SetCert("enforce");
        Assert.IsFalse(options.IgnoreCertErrors, "SetCert('enforce') should set IgnoreCertErrors to false");
        Assert.AreEqual("enforce", options.CertString, "CertString should reflect the set value");

        options.SetCert("ignore");
        Assert.IsTrue(options.IgnoreCertErrors, "SetCert('ignore') should set IgnoreCertErrors to true");
        Assert.AreEqual("ignore", options.CertString, "CertString should reflect the set value");
    }

    [TestMethod]
    public void KioskModeRegression_ShouldNotActivateWhenSetToNo()
    {
        // Arrange - This test specifically validates the fix for the KIOSK mode issue
        var options = new CommandLineOptions();

        // Act - Simulate command line parameter --KIOSK no
        options.KioskString = "no";

        // Assert - The critical fix: "no" should NOT activate KIOSK mode
        Assert.IsFalse(options.Kiosk, 
            "CRITICAL: KIOSK mode should NOT be activated when explicitly set to 'no'. " +
            "This was the original bug where 'no' was treated as truthy.");
        
        Assert.AreEqual("no", options.KioskString, 
            "KioskString should correctly store and return 'no'");
    }

    [TestMethod]
    public void AllBooleanProperties_ShouldHaveConsistentBehavior()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Test all boolean properties follow the same pattern
        var testCases = new[]
        {
            new { SetNo = (Action)(() => options.KioskString = "no"), 
                  SetYes = (Action)(() => options.KioskString = "yes"), 
                  GetBool = (Func<bool>)(() => options.Kiosk),
                  GetString = (Func<string>)(() => options.KioskString),
                  Name = "Kiosk" },
            
            new { SetNo = (Action)(() => options.IncognitoString = "no"), 
                  SetYes = (Action)(() => options.IncognitoString = "yes"), 
                  GetBool = (Func<bool>)(() => options.Incognito),
                  GetString = (Func<string>)(() => options.IncognitoString),
                  Name = "Incognito" }
        };

        foreach (var testCase in testCases)
        {
            // Test "no" case
            testCase.SetNo();
            Assert.IsFalse(testCase.GetBool(), $"{testCase.Name} boolean should be false for 'no'");
            Assert.AreEqual("no", testCase.GetString(), $"{testCase.Name} string should be 'no'");

            // Test "yes" case
            testCase.SetYes();
            Assert.IsTrue(testCase.GetBool(), $"{testCase.Name} boolean should be true for 'yes'");
            Assert.AreEqual("yes", testCase.GetString(), $"{testCase.Name} string should be 'yes'");
        }
    }

    [TestMethod]
    public void CertProperty_ShouldFollowIgnoreEnforcePattern()
    {
        // Arrange
        var options = new CommandLineOptions();

        // Test "enforce" case
        options.CertString = "enforce";
        Assert.IsFalse(options.IgnoreCertErrors, "IgnoreCertErrors should be false for 'enforce'");
        Assert.AreEqual("enforce", options.CertString, "CertString should be 'enforce'");

        // Test "ignore" case
        options.CertString = "ignore";
        Assert.IsTrue(options.IgnoreCertErrors, "IgnoreCertErrors should be true for 'ignore'");
        Assert.AreEqual("ignore", options.CertString, "CertString should be 'ignore'");
    }

    [TestMethod]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new CommandLineOptions();

        // Assert - All boolean flags should default to false
        Assert.IsFalse(options.Kiosk, "Kiosk should default to false");
        Assert.IsFalse(options.Incognito, "Incognito should default to false");
        Assert.IsFalse(options.IgnoreCertErrors, "IgnoreCertErrors should default to false");

        // String representations should reflect the false state
        Assert.AreEqual("no", options.KioskString, "KioskString should default to 'no'");
        Assert.AreEqual("no", options.IncognitoString, "IncognitoString should default to 'no'");
        Assert.AreEqual("enforce", options.CertString, "CertString should default to 'enforce'");
    }
} 
