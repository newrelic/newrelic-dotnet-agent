using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface ITransactionMetricNameMaker
	{
		/// <summary>
		/// Builds a metric name from a ITransactionName.
		/// </summary>
		/// <param name="transactionName">The original transaction name.</param>
		TransactionMetricName GetTransactionMetricName([NotNull] ITransactionName transactionName);
	}

	public class TransactionMetricNameMaker : ITransactionMetricNameMaker
	{
		[NotNull]
		private readonly IMetricNameService _metricNameService;

		public TransactionMetricNameMaker([NotNull] IMetricNameService metricNameService)
		{
			_metricNameService = metricNameService;
		}

		public TransactionMetricName GetTransactionMetricName(ITransactionName transactionName)
		{
			var proposedTransactionMetricName = new TransactionMetricName(transactionName.IsWeb ? MetricNames.WebTransactionPrefix : MetricNames.OtherTransactionPrefix, transactionName.UnprefixedName);

			var vettedTransactionMetricName = _metricNameService.RenameTransaction(proposedTransactionMetricName);

			return vettedTransactionMetricName;
		}
	}

	/// <summary>
	/// Represents a transaction metric name, with the option to see both the prefixed version ("WebTransaction/Foo/Bar") and the unprefixed version ("Foo/Bar").
	/// </summary>
	public struct TransactionMetricName
	{
		/// <summary>
		/// The prefix name (e.g. "WebTransaction")
		/// </summary>
		[NotNull]
		public readonly String Prefix;

		/// <summary>
		/// The unprefixed name (e.g. "Foo/Bar")
		/// </summary>
		[NotNull]
		public readonly String UnPrefixedName;

		/// <summary>
		/// The prefixed name (e.g. "WebTranaction/Foo/Bar")
		/// </summary>
		[NotNull]
		public readonly String PrefixedName;

		/// <summary>
		/// True iff this metric name's prefix is equal to MetricNames.WebTranasctionPrefix.
		/// </summary>
		public readonly Boolean IsWebTransactionName;

		/// <summary>
		/// True iff a transaction with this name should be ignored.
		/// </summary>
		public readonly Boolean ShouldIgnore;

		/// <summary>
		/// Builds a new transaction metric name using a prefix and an unprefixed name. The Prefixed name will be $"{prefix}/{unprefixedName}".
		/// </summary>
		public TransactionMetricName([NotNull] String prefix, [NotNull] String unprefixedName, Boolean shouldIgnore = false)
		{
			Prefix = prefix;
			UnPrefixedName = unprefixedName;
			PrefixedName = $"{prefix}{MetricNames.PathSeparator}{unprefixedName}";
			IsWebTransactionName = prefix == MetricNames.WebTransactionPrefix;
			ShouldIgnore = shouldIgnore;
		}

		public override String ToString()
		{
			return PrefixedName;
		}
	}
}