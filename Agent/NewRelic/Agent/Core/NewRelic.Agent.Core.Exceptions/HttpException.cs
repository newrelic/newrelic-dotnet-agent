using System;
using System.Net;

namespace NewRelic.Agent.Core.Exceptions
{
	/// <summary>
	/// Thrown when the connection to the collector(RPM) reports an HTTP transport error.
	/// </summary>
	public class HttpException : Exception
	{
		public HttpStatusCode StatusCode { get; }

		public HttpException(HttpStatusCode statusCode, string message)
			: base(message)
		{
			StatusCode = statusCode;
		}
	}
}
