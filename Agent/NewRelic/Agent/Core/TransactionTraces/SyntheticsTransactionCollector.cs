using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.TransactionTraces
{
	public class SyntheticsTransactionCollector : ITransactionCollector, IDisposable
	{
		[NotNull]
		private volatile ICollection<TransactionTraceWireModelComponents> _collectedSamples = new ConcurrentHashSet<TransactionTraceWireModelComponents>();

		[NotNull]
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
			var oldCollectedSamples = _collectedSamples;
			return oldCollectedSamples;
		}

		public void ClearCollectedSamples()
		{
			_collectedSamples = new ConcurrentHashSet<TransactionTraceWireModelComponents>();
		}

		public void Dispose()
		{
			_configurationSubscription.Dispose();
		}
	}
}
