using System;
using System.ServiceModel;

namespace NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService
{
	[ServiceContract]
	public interface IWcfService
	{
		[OperationContract(AsyncPattern = true)]
		IAsyncResult BeginServiceMethod(String value, String otherValue, AsyncCallback callback, Object asyncState);
		String EndServiceMethod(IAsyncResult result);

		[OperationContract]
		String ReturnInputIgnored(String input);
	}
}
