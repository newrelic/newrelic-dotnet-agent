using System.ServiceModel;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted
{
    [ServiceContract]
    public interface IWcfService
    {
        [OperationContract]
        string GetString();

        [OperationContract]
        string ReturnString(string input);

        [OperationContract]
        void ThrowException();

        [OperationContract]
        string IgnoredTransaction(string input);
    }
}
