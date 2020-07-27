using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted
{
    [ServiceContract]
    public interface IMyService
    {
        [OperationContract]
        string GetData(int value);

        [OperationContract]
        string IgnoredTransaction(string input);

        [OperationContract]
        void ThrowException();
    }
}
