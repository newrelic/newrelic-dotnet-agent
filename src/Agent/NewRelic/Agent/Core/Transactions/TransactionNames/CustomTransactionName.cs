using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class CustomTransactionName : ITransactionName
    {
        [NotNull]
        public readonly String Name;

        public Boolean IsWeb { get; }

        public CustomTransactionName([NotNull] String name, Boolean isWeb)
        {
            Name = name;
            IsWeb = isWeb;
        }

    }
}
