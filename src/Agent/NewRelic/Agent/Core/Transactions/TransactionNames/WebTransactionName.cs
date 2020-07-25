using System;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class WebTransactionName : ITransactionName
    {
        public readonly String Category;
        public readonly String Name;

        public WebTransactionName(String category, String name)
        {
            Category = category;
            Name = name;
        }

        public Boolean IsWeb { get { return true; } }
    }
}
