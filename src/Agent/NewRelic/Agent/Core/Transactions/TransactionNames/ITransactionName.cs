namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public interface ITransactionName
    {
        bool IsWeb { get; }
    }
}
