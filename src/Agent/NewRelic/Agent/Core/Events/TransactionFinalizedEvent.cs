using NewRelic.Agent.Core.Transactions;

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
