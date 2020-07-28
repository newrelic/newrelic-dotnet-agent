using System;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class CustomTransactionName : ITransactionName
    {
        public readonly String Name;

        public Boolean IsWeb { get; }

        public CustomTransactionName(String name, Boolean isWeb)
        {
            Name = name;
            IsWeb = isWeb;
        }

    }
}
