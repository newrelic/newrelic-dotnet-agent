using System.Net;

namespace NewRelic.Agent.Core.Exceptions
{
	public class SerializationException : HttpException
	{
		public SerializationException(string message)
			: base(HttpStatusCode.UnsupportedMediaType, message)
		{
		}
	}
}
