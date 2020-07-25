using System;
using System.Net;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class WcfService : IWcfService
    {
        public IAsyncResult BeginServiceMethod(String value, String otherValue, AsyncCallback callback, Object asyncState)
        {
            if (callback == null)
            {
                throw new NullReferenceException("callback");
            }

            //RPM requires a minimum of TLS12
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            //This web call is used for asserting metrics
            new WebClient().DownloadString("https://www.google.com/");

            var task = new Task<String>(_ => value, asyncState);
            task.ContinueWith(x => callback(x));
            task.Start();

            return task;
        }

        public String EndServiceMethod(IAsyncResult asyncResult)
        {
            var result = asyncResult as Task<String>;
            if (result == null)
                throw new Exception("asyncResult was not a Task<String>");
            return result.Result;
        }

        public String ReturnInputIgnored(String input)
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
            return input;
        }
    }
}
