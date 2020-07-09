using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.DataTransport
{
	public interface IConnectionHandler
	{
		void Connect();
		void Disconnect();
		T SendDataRequest<T>([NotNull] String method, [NotNull] params Object[] data);
	}
}