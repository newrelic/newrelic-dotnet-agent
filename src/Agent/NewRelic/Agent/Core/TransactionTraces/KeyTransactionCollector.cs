using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;

namespace NewRelic.Agent.Core.TransactionTraces
{
    public class KeyTransactionCollector : ITransactionCollector, IDisposable
    {
        private volatile TransactionTraceWireModelComponents _slowTransaction;
        private double _score = 0.0;

        protected ConfigurationSubscriber ConfigurationSubscription = new ConfigurationSubscriber();

        public void Collect(TransactionTraceWireModelComponents transactionTraceWireModelComponents)
        {
            var isKeyTransaction = ConfigurationSubscription.Configuration.WebTransactionsApdex.TryGetValue(transactionTraceWireModelComponents.TransactionMetricName.ToString(), out double apdexT);
            if (!isKeyTransaction)
                return;

            var apdexTime = TimeSpan.FromSeconds(apdexT);
            if (transactionTraceWireModelComponents.Duration <= apdexTime)
                return;

            // larger the score, the larger the diff
            var score = 100.0 * (transactionTraceWireModelComponents.Duration.TotalMilliseconds / apdexTime.TotalMilliseconds);
            if (_slowTransaction != null && _score > score)
                return;

            _slowTransaction = transactionTraceWireModelComponents;
            _score = score;
        }

        public IEnumerable<TransactionTraceWireModelComponents> GetCollectedSamples()
        {
            var slowTransaction = _slowTransaction;
            return slowTransaction == null ? Enumerable.Empty<TransactionTraceWireModelComponents>() :
                new TransactionTraceWireModelComponents[] { slowTransaction };
        }

        public void ClearCollectedSamples()
        {
            _slowTransaction = null;
        }

        public void Dispose()
        {
            ConfigurationSubscription.Dispose();
        }
    }
}
