using System;

namespace WebConnect.Exceptions
{
    public class BrowserLaunchException : WebConnectException
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
