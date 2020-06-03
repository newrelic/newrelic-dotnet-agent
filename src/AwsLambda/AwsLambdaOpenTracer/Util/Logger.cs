using Amazon.Lambda.Core;
using System;

namespace NewRelic.OpenTracing.AmazonLambda.Util
{
    internal interface ILogger
    {
        void Log(string message, bool rawLogging = true, string level = "INFO");
    }

    internal class Logger : ILogger
    {
        public void Log(string message, bool rawLogging = true, string level = "INFO")
        {
            if (rawLogging)
            {
                LambdaLogger.Log(message + Environment.NewLine);
            }
            else
            {
                LambdaLogger.Log($"{Timestamp} NewRelic {level}: {message}" + Environment.NewLine);
            }
        }

        private string Timestamp
        {
            get
            {
                // 2019-02-12 16:06:25,422 NewRelic INFO: 
                return DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH-mm-ss,fff");
            }
        }
    }
}
