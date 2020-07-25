using System;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class OtherTransactionName : ITransactionName
    {
        public readonly String Category;
        public readonly String Name;

        public OtherTransactionName(String category, String name)
        {
            Category = category;
            Name = name;
        }

        public Boolean IsWeb { get { return false; } }
    }
}
