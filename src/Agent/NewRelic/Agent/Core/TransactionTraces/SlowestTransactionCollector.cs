using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.TransactionTraces
{
    public class SlowestTransactionCollector : ITransactionCollector, IDisposable
    {
        private volatile TransactionTraceWireModelComponents _slowTransaction;

        [NotNull]
        protected ConfigurationSubscriber ConfigurationSubscription = new ConfigurationSubscriber();

        public void Collect(TransactionTraceWireModelComponents transactionTraceWireModelComponents)
        {
            if (transactionTraceWireModelComponents.Duration <= ConfigurationSubscription.Configuration.TransactionTraceThreshold)
                return;

            if (_slowTransaction != null && _slowTransaction.Duration > transactionTraceWireModelComponents.Duration)
                return;

            _slowTransaction = transactionTraceWireModelComponents;
        }

        public IEnumerable<TransactionTraceWireModelComponents> GetAndClearCollectedSamples()
        {
            var slowTransaction = _slowTransaction;
            _slowTransaction = null;
            return slowTransaction == null ? Enumerable.Empty<TransactionTraceWireModelComponents>() :
                new TransactionTraceWireModelComponents[] { slowTransaction };
        }

        public void Dispose()
        {
            ConfigurationSubscription.Dispose();
        }
    }
}
