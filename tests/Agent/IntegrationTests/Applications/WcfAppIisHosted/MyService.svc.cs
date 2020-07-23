using System;
using System.Threading;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted
{
	public class MyService : IMyService
	{
		public String GetData(Int32 value)
		{
			return String.Format("You entered: {0}", value);
		}

		public String IgnoredTransaction(String input)
		{
			NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
			return input;
		}
		public void ThrowException()
		{
			throw new Exception("ExceptionMessage");
		}
	}
}
