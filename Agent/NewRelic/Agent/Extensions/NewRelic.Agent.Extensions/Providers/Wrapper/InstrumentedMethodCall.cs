namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public class InstrumentedMethodCall
	{
		public readonly MethodCall MethodCall;
		public readonly InstrumentedMethodInfo InstrumentedMethodInfo;

		public bool IsAsync => InstrumentedMethodInfo.IsAsync;
		public string RequestedMetricName => InstrumentedMethodInfo.RequestedMetricName;
		public TransactionNamePriority? RequestedTransactionNamePriority => InstrumentedMethodInfo.RequestedTransactionNamePriority;
		public bool StartWebTransaction => InstrumentedMethodInfo.StartWebTransaction;

		public InstrumentedMethodCall(MethodCall methodCall, InstrumentedMethodInfo instrumentedMethodInfo)
		{
			MethodCall = methodCall;
			InstrumentedMethodInfo = instrumentedMethodInfo;
		}
	}
}
