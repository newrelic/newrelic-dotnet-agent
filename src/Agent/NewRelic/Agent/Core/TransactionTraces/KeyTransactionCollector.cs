// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;

namespace NewRelic.Agent.Core.TransactionTraces
{
    public class KeyTransactionCollector : ITransactionCollector, IDisposable
    {
        private volatile ConcurrentDictionary<double, TransactionTraceWireModelComponents> _keyTransactions =
            new ConcurrentDictionary<double, TransactionTraceWireModelComponents>();

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

            // If there aren't any lower scores than what we currently encountered, then add this one to the collection
            if (!_keyTransactions.Any(x => x.Key < score))
            {
                _keyTransactions[score] = transactionTraceWireModelComponents;
            }
        }

        public IEnumerable<TransactionTraceWireModelComponents> GetCollectedSamples()
        {
            var harvestedKeyTransactions = Interlocked.Exchange(ref _keyTransactions,
                new ConcurrentDictionary<double, TransactionTraceWireModelComponents>());

            if (harvestedKeyTransactions.Count == 0)
            {
                return Enumerable.Empty<TransactionTraceWireModelComponents>();
            }

            var worstScoredTransaction = harvestedKeyTransactions.Aggregate((x, y) => x.Key < y.Key ? x : y).Value;
            return new TransactionTraceWireModelComponents[] { worstScoredTransaction };
        }

        public void Dispose()
        {
            ConfigurationSubscription.Dispose();
        }
    }
}
