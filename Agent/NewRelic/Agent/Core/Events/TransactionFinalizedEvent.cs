using JetBrains.Annotations;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Events
{
	public class TransactionFinalizedEvent
	{
		[NotNull]
		public readonly IInternalTransaction Transaction;

		public TransactionFinalizedEvent([NotNull] IInternalTransaction transaction)
		{
			Transaction = transaction;
		}
	}
}
