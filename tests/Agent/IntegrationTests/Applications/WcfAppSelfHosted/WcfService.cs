using System;
using System.Threading;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted
{
    public class WcfService : IWcfService
    {
        public const string WcfServiceGetStringResponse = "Response string.";

        public String GetString()
        {
            NewRelic.Api.Agent.NewRelic.AddCustomParameter("custom key", "custom value");
            NewRelic.Api.Agent.NewRelic.AddCustomParameter("custom foo", "custom bar");

            return WcfServiceGetStringResponse;
        }

        public String ReturnString(String input)
        {
            return input;
        }

        public void ThrowException()
        {
            throw new Exception("ExceptionMessage");
        }

        public String IgnoredTransaction(String input)
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
            return input;
        }
    }
}
