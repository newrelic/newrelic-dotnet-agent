using System;

namespace NewRelic.Agent.Core.Exceptions
{
	/// <summary>
	/// This exception is thrown when there has been a connection error between the collector(APM) and the Agent.
	/// </summary>
	public class ConnectionException : Exception
	{
		public ConnectionException(String message) : base(message)
		{
		}
	}
}
