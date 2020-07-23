using System;

namespace NewRelic.Agent.Core
{
	[Serializable]
	public class SimpleException : Exception
	{
		public SimpleException(String exceptionName)
			: base(exceptionName)
		{
		}
		public SimpleException(String exceptionName, Exception ex)
			: base(exceptionName, ex)
		{
		}
	}
}
