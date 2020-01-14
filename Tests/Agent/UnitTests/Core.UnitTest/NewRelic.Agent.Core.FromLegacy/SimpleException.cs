using System;

namespace NewRelic.Agent.Core
{
	[Serializable]
	public class SimpleException : Exception
	{
		public SimpleException(string exceptionName)
			: base(exceptionName)
		{
		}
		public SimpleException(string exceptionName, Exception ex)
			: base(exceptionName, ex)
		{
		}
	}
}
