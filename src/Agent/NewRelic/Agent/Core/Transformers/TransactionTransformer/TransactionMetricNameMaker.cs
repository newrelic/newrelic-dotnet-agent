using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions.TransactionNames;

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
            var proposedTransactionMetricName = GetProposedTransactionMetricName(transactionName);
            var vettedTransactionMetricName = _metricNameService.RenameTransaction(proposedTransactionMetricName);
            return vettedTransactionMetricName;
        }

        private TransactionMetricName GetProposedTransactionMetricName([NotNull] ITransactionName transactionName)
        {
            if (transactionName is WebTransactionName)
                return GetTransactionMetricName(transactionName as WebTransactionName);
            if (transactionName is UriTransactionName)
                return GetTransactionMetricName(transactionName as UriTransactionName);
            if (transactionName is OtherTransactionName)
                return GetTransactionMetricName(transactionName as OtherTransactionName);
            if (transactionName is MessageBrokerTransactionName)
                return GetTransactionMetricName(transactionName as MessageBrokerTransactionName);
            if (transactionName is CustomTransactionName)
                return GetTransactionMetricName(transactionName as CustomTransactionName);

            throw new NotImplementedException("Unsupported ITransactionName type");
        }

        private TransactionMetricName GetTransactionMetricName([NotNull] WebTransactionName transactionName)
        {
            return MetricNames.WebTransaction(transactionName.Category, transactionName.Name);
        }

        private TransactionMetricName GetTransactionMetricName([NotNull] UriTransactionName transactionName)
        {
            var normalizedUri = _metricNameService.NormalizeUrl(transactionName.Uri);
            return MetricNames.UriTransaction(normalizedUri);
        }

        private TransactionMetricName GetTransactionMetricName([NotNull] OtherTransactionName transactionName)
        {
            return MetricNames.OtherTransaction(transactionName.Category, transactionName.Name);
        }

        private TransactionMetricName GetTransactionMetricName([NotNull] MessageBrokerTransactionName transactionName)
        {
            return MetricNames.MessageBrokerTransaction(transactionName.DestinationType, transactionName.BrokerVendorName, transactionName.Destination);
        }

        private TransactionMetricName GetTransactionMetricName([NotNull] CustomTransactionName transactionName)
        {
            return MetricNames.CustomTransaction(transactionName.Name, transactionName.IsWeb);
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
