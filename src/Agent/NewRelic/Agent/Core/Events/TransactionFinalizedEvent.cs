using JetBrains.Annotations;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Events
{
	public class TransactionFinalizedEvent
	{
		[NotNull]
		public readonly ITransaction Transaction;

		public TransactionFinalizedEvent([NotNull] ITransaction transaction)
		{
			Transaction = transaction;
		}
	}
}
