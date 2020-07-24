using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class UriTransactionName : ITransactionName
    {
        [NotNull]
        public readonly String Uri;

        public UriTransactionName([NotNull] String uri)
        {
            Uri = uri;
        }

        public Boolean IsWeb { get { return true; } }
    }
}
