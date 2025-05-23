using OpenQA.Selenium;

namespace ChromeConnect.Services
{
    /// <summary>
    /// Interface for screenshot capture functionality.
    /// </summary>
    public interface IScreenshotCapture
    {
        /// <summary>
        /// Captures a screenshot of the current browser state.
        /// </summary>
        /// <param name="driver">The WebDriver instance to capture from.</param>
        /// <param name="prefix">A prefix for the screenshot filename.</param>
        /// <returns>The path to the saved screenshot, or null if the capture failed.</returns>
        string CaptureScreenshot(IWebDriver driver, string prefix);
    }
} 