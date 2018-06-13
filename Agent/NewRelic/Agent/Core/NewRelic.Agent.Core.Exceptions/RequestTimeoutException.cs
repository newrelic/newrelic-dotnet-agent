using System.Net;

namespace NewRelic.Agent.Core.Exceptions
{
	public class RequestTimeoutException : HttpException
	{
		public RequestTimeoutException(string message)
			: base(HttpStatusCode.RequestTimeout, message)
		{
		}
	}
}
