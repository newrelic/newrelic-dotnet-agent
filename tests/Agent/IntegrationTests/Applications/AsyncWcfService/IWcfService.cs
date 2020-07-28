using System;
using System.ServiceModel;

namespace NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService
{
    [ServiceContract]
    public interface IWcfService
    {
        [OperationContract(AsyncPattern = true)]
        IAsyncResult BeginServiceMethod(string value, string otherValue, AsyncCallback callback, object asyncState);
        string EndServiceMethod(IAsyncResult result);

        [OperationContract]
        string ReturnInputIgnored(string input);
    }
}
