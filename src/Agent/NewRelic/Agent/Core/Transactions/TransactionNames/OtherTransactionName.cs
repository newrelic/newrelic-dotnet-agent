using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
	public class OtherTransactionName : ITransactionName
	{
		[NotNull]
		public readonly String Category;
		[NotNull]
		public readonly String Name;

		public OtherTransactionName([NotNull] String category, [NotNull] String name)
		{
			Category = category;
			Name = name;
		}

		public Boolean IsWeb { get { return false; } }
	}
}