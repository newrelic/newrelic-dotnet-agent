using System;

namespace NewRelic.Agent.Core.Exceptions
{
	/// <summary>
	/// Thrown when the data posted from the Agent to the collector(RPM) is too large.
	/// </summary>
	public class PostTooLargeException : Exception
	{
		public PostTooLargeException(string message) : base("Post too large")
		{
		}
	}
}
