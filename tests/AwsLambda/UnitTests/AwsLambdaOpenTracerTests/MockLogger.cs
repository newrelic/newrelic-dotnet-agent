using NewRelic.OpenTracing.AmazonLambda.Util;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    public class MockLogger : ILogger
    {
        public string LastLogMessage { get; set; }

        public void Log(string message, bool rawLogging = true, string level = "INFO")
        {
            LastLogMessage = message;
        }
    }
}
