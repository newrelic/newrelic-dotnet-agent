using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public enum TransactionNamePriority
	{
		Uri = 1,
		StatusCode = 2,
		Handler = 3,
		Route = 4,
		FrameworkLow = 5,
		FrameworkHigh = 6,
		CustomTransactionName = 8,
		UserTransactionName = Int32.MaxValue
	}
}
