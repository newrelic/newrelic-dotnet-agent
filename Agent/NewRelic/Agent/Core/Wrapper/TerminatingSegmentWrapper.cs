using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
	public class TerminatingSegmentWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			return new CanWrapResponse("TerminatingSegmentWrapper".Equals(methodInfo.RequestedWrapperName));
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				agentWrapperApi.CurrentTransactionWrapperApi.Detach();
				return Delegates.NoOp;
			}

			var segment = agentWrapperApi.CurrentTransactionWrapperApi.StartTerminatingSegment(instrumentedMethodCall.MethodCall);
			return Delegates.GetDelegateFor(segment);
		}
	}
}
