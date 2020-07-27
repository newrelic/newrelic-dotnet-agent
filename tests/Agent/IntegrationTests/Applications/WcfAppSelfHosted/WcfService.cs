using System;
using System.Threading;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted
{
    public class WcfService : IWcfService
    {
        public const string WcfServiceGetStringResponse = "Response string.";

        public string GetString()
        {
            NewRelic.Api.Agent.NewRelic.AddCustomParameter("custom key", "custom value");
            NewRelic.Api.Agent.NewRelic.AddCustomParameter("custom foo", "custom bar");

            return WcfServiceGetStringResponse;
        }

        public string ReturnString(string input)
        {
            return input;
        }

        public void ThrowException()
        {
            throw new Exception("ExceptionMessage");
        }

        public string IgnoredTransaction(string input)
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
            return input;
        }
    }
}
