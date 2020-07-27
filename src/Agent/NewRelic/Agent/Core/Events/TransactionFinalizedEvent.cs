using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Events
{
    public class TransactionFinalizedEvent
    {
        public readonly ITransaction Transaction;

        public TransactionFinalizedEvent(ITransaction transaction)
        {
            Transaction = transaction;
        }
    }
}
