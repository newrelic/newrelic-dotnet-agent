// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.TransactionTraces
{
    public class SyntheticsTransactionCollector : ITransactionCollector, IDisposable
    {
        private volatile ConcurrentBag<TransactionTraceWireModelComponents> _collectedSamples = new ConcurrentBag<TransactionTraceWireModelComponents>();

        private readonly ConfigurationSubscriber _configurationSubscription = new ConfigurationSubscriber();

        public void Collect(TransactionTraceWireModelComponents transactionTraceWireModelComponents)
        {
            if (!transactionTraceWireModelComponents.IsSynthetics)
                return;
            if (_collectedSamples.Count >= SyntheticsHeader.MaxTraceCount)
                return;

            _collectedSamples.Add(transactionTraceWireModelComponents);
        }

        public IEnumerable<TransactionTraceWireModelComponents> GetCollectedSamples()
        {
            var harvestedTransactions = Interlocked.Exchange(ref _collectedSamples,
                new ConcurrentBag<TransactionTraceWireModelComponents>());

            return harvestedTransactions;
        }

        public void Dispose()
        {
            _configurationSubscription.Dispose();
        }
    }
}
