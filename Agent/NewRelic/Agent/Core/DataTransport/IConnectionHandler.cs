using System;

namespace NewRelic.Agent.Core.DataTransport
{
	public interface IConnectionHandler
	{
		void Connect();
		void Disconnect();
		T SendDataRequest<T>(string method, params object[] data);
	}
}