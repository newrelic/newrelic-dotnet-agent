using System;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class UriTransactionName : ITransactionName
    {
        public readonly String Uri;

        public UriTransactionName(String uri)
        {
            Uri = uri;
        }

        public Boolean IsWeb { get { return true; } }
    }
}
