using System;

namespace NewRelic.Agent.Core.Configuration
{
	public class SecurityPoliciesValidationException : Exception
	{
		public SecurityPoliciesValidationException()
		{
		}

		public SecurityPoliciesValidationException(string message) : base(message)
		{
		}

		public SecurityPoliciesValidationException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}