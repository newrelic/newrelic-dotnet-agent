using System;

namespace NewRelic.Agent.Core.Exceptions
{

	/// <summary>
	/// This exception is thrown when the Agent is to restart, as for example
	/// when Agent settings on the RPM change.
	/// </summary>
	public class ForceRestartException : InstructionException
	{

		public ForceRestartException (String message) : base(message)
		{
		}
	}
}
