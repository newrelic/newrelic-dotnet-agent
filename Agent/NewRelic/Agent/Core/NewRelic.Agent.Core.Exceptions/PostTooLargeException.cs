using System.Net;

namespace NewRelic.Agent.Core.Exceptions
{
	/// <summary>
	/// Thrown when the data posted from the Agent to the collector(RPM) is too large.
	/// </summary>
	public class PostTooLargeException : HttpException
	{
		public PostTooLargeException(string message) : base(HttpStatusCode.RequestEntityTooLarge, "Post too large")
		{
		}
	}
}
