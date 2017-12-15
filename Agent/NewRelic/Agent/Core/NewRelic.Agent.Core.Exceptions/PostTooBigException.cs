using System;

namespace NewRelic.Agent.Core.Exceptions
{

	/// <summary>
	/// This exception is thrown when too much data is pushed from the Agent back to the RPM.
	/// </summary>
	public class PostTooBigException : RPMException
	{

		public PostTooBigException (String message) : base(message)
		{
		}
	}
}
