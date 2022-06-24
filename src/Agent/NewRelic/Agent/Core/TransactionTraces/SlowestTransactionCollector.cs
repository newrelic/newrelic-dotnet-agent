// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;

namespace NewRelic.Agent.Core.TransactionTraces
{
    public class SlowestTransactionCollector : ITransactionCollector, IDisposable
    {
        private volatile ConcurrentBag<TransactionTraceWireModelComponents> _slowTransactions = new ConcurrentBag<TransactionTraceWireModelComponents>();

        protected ConfigurationSubscriber ConfigurationSubscription = new ConfigurationSubscriber();

        public void Collect(TransactionTraceWireModelComponents transactionTraceWireModelComponents)
        {
            if (transactionTraceWireModelComponents.Duration <= ConfigurationSubscription.Configuration.TransactionTraceThreshold)
            {
                return;
            }

            // If this is the slowest transaction so far, save it!
            if (!_slowTransactions.Any(x => x.Duration > transactionTraceWireModelComponents.Duration))
            {
                _slowTransactions.Add(transactionTraceWireModelComponents);
            }
        }

        public IEnumerable<TransactionTraceWireModelComponents> GetCollectedSamples()
        {
            var harvestedSlowTransactions = Interlocked.Exchange(ref _slowTransactions,
                new ConcurrentBag<TransactionTraceWireModelComponents>());

            if (harvestedSlowTransactions.Count == 0)
            {
                return Enumerable.Empty<TransactionTraceWireModelComponents>();
            }

            var slowestTransaction = harvestedSlowTransactions.Aggregate((x, y) => x.Duration > y.Duration ? x : y);
            return new TransactionTraceWireModelComponents[] { slowestTransaction };
        }

        public void Dispose()
        {
            ConfigurationSubscription.Dispose();
        }
    }
}
