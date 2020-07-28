namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class UriTransactionName : ITransactionName
    {
        public readonly string Uri;

        public UriTransactionName(string uri)
        {
            Uri = uri;
        }

        public bool IsWeb { get { return true; } }
    }
}
