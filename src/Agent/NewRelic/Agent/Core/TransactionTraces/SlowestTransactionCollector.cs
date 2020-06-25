/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using System.Linq;

namespace NewRelic.Agent.Core.TransactionTraces
{
    public class SlowestTransactionCollector : ITransactionCollector, IDisposable
    {
        private volatile TransactionTraceWireModelComponents _slowTransaction;

        protected ConfigurationSubscriber ConfigurationSubscription = new ConfigurationSubscriber();

        public void Collect(TransactionTraceWireModelComponents transactionTraceWireModelComponents)
        {
            if (transactionTraceWireModelComponents.Duration <= ConfigurationSubscription.Configuration.TransactionTraceThreshold)
                return;

            if (_slowTransaction != null && _slowTransaction.Duration > transactionTraceWireModelComponents.Duration)
                return;

            _slowTransaction = transactionTraceWireModelComponents;
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
