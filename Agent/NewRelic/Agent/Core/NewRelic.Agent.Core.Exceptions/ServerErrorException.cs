using System.Net;

namespace NewRelic.Agent.Core.Exceptions
{

	/// <summary>
	/// This exception is thrown when a 5xx response is pushed from the Agent back to the RPM.
	/// </summary>
	public class ServerErrorException : HttpException
	{
		public ServerErrorException(string message, HttpStatusCode statusCode) : base(statusCode, message)
		{
		}
	}
}
