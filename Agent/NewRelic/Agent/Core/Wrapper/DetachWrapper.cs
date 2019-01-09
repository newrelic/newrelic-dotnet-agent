using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
	public class DetachWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			return new CanWrapResponse("DetachWrapper".Equals(methodInfo.RequestedWrapperName));
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			agentWrapperApi.CurrentTransactionWrapperApi.Detach();
			return Delegates.NoOp;
		}
	}
}
