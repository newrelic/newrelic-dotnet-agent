using System;

namespace NewRelic.Agent.Core.Exceptions
{
	public class RuntimeException : RPMException
	{
		public RuntimeException(String message)
			: base(message)
		{
		}

		public RuntimeException(String message, Exception ex)
			: base(message, ex)
		{
		}
	}
}
