using System;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public interface ITransactionName
    {
        Boolean IsWeb { get; }
    }
}
