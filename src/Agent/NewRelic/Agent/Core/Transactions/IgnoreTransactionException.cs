using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Transactions
{
	/// <summary>
	/// Thrown by the transaction name normalizer to indicate that a transaction should be ignored.
	/// </summary>
	public class IgnoreTransactionException : Exception
	{
		/// <summary>
		/// The name that was ignored
		/// </summary>
		[NotNull]
		public readonly String IgnoredTransactionName;

		public IgnoreTransactionException (String message, [NotNull] String ignoredTransactionName) : base(message)
		{
			IgnoredTransactionName = ignoredTransactionName;
		}
	}
}

