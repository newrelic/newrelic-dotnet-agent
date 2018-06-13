using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.CustomInstrumentation
{
	public class MultithreadedTrackingWrapper : IWrapper
	{
		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			var canWrap = "MultithreadedTrackingWrapper".Equals(instrumentedMethodInfo.RequestedWrapperName);

			if (canWrap && instrumentedMethodInfo.IsAsync)
			{
				return new CanWrapResponse(false, "This instrumentation is not intended to be used with async-await. Use the OtherTransactionWrapperAsync instead.");
			}

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var typeName = instrumentedMethodCall.MethodCall.Method.Type;
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;

			var name = $"{typeName}/{methodName}";
			
			transaction = instrumentedMethodCall.StartWebTransaction ?
				agentWrapperApi.CreateWebTransaction(WebTransactionType.Custom, name, false) :
				agentWrapperApi.CreateOtherTransaction("Custom", name, mustBeRootTransaction: false);

			transaction.AttachToAsync();

			var segment = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
				? transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
				: transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, name);

			var hasMetricName = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName);
			if (hasMetricName)
			{
				var priority = instrumentedMethodCall.RequestedTransactionNamePriority ?? 1;
				transaction.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, priority);
			}
			
			return Delegates.GetDelegateFor(
				onFailure: transaction.NoticeError,
				onComplete: OnComplete);
			
			void OnComplete()
			{
				segment.End();
				transaction.End();
			}
		}
	}
}
