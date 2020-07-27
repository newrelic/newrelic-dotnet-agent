namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class WebTransactionName : ITransactionName
    {
        public readonly string Category;
        public readonly string Name;

        public WebTransactionName(string category, string name)
        {
            Category = category;
            Name = name;
        }

        public bool IsWeb { get { return true; } }
    }
}
