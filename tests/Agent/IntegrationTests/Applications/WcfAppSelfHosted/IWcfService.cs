using System;
using System.ServiceModel;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted
{
	[ServiceContract]
	public interface IWcfService
	{
		[OperationContract]
		String GetString();

		[OperationContract]
		String ReturnString(String input);

		[OperationContract]
		void ThrowException();

		[OperationContract]
		String IgnoredTransaction(String input);
	}
}
