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
        String GetData(Int32 value);

        [OperationContract]
        String IgnoredTransaction(String input);

        [OperationContract]
        void ThrowException();
    }
}
