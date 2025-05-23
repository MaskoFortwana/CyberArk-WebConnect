using System;

namespace ChromeConnect.Exceptions
{
    public class BrowserLaunchException : ChromeConnectException
    {
        public BrowserLaunchException()
        {
        }

        public BrowserLaunchException(string message)
            : base(message)
        {
        }

        public BrowserLaunchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
} 