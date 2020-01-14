using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Events
{
	public class TransactionFinalizedEvent
	{
		public readonly IInternalTransaction Transaction;

		public TransactionFinalizedEvent(IInternalTransaction transaction)
		{
			Transaction = transaction;
		}
	}
}
