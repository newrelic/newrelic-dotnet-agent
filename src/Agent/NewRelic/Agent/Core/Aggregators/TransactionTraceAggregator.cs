// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.TransactionTraces;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using Newtonsoft.Json;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface ITransactionTraceAggregator
    {
        void Collect(TransactionTraceWireModelComponents transactionTraceWireModel);
    }

    public class TransactionTraceAggregator : AbstractAggregator<TransactionTraceWireModelComponents>, ITransactionTraceAggregator
    {
        private readonly IEnumerable<ITransactionCollector> _transactionCollectors;

        public TransactionTraceAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IEnumerable<ITransactionCollector> transactionCollectors)
            : base(dataTransportService, scheduler, processStatic)
        {
            _transactionCollectors = transactionCollectors;
        }

        protected override bool IsEnabled => _configuration.TransactionTracerEnabled;

        protected override TimeSpan HarvestCycle => _configuration.TransactionTracesHarvestCycle;

        public override void Collect(TransactionTraceWireModelComponents transactionTraceWireModel)
        {
            foreach (var transactionCollector in _transactionCollectors)
            {
                transactionCollector?.Collect(transactionTraceWireModel);
            }
        }

        protected override void Harvest()
        {
            var traceSamples = _transactionCollectors
                .Where(t => t != null)
                .SelectMany(t => t.GetCollectedSamples())
                .Distinct()
                .ToList();

            var traceWireModels = traceSamples
                .Select(t => t.CreateWireModel())
                .ToList();

            if (!traceWireModels.Any())
                return;

            LogUnencodedTraceData(traceWireModels);

            var responseStatus = DataTransportService.Send(traceWireModels);
            HandleResponse(responseStatus, traceSamples);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<TransactionTraceWireModelComponents> traceSamples)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    break;

                case DataTransportResponseStatus.Retain:
                    // Retain collected samples if applicable
                    foreach (var traceSample in traceSamples)
                    {
                        Collect(traceSample);
                    }
                    break;

                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                case DataTransportResponseStatus.Discard:
                default:
                    Log.Warn($"Discarding {traceSamples.Count} transaction traces due to collector response.");
                    break;
            }
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            foreach (var transactionCollector in _transactionCollectors)
            {
                transactionCollector?.GetCollectedSamples();
            }
        }

        private void LogUnencodedTraceData(IEnumerable<TransactionTraceWireModel> samples)
        {
            if (Log.IsDebugEnabled)
            {
                foreach (var sample in samples)
                {
                    if (sample != null)
                    {
                        Log.Debug("TransactionTraceData: {0}", SerializeTransactionTraceData(sample));
                    }
                }
            }
        }

        private static string SerializeTransactionTraceData(TransactionTraceWireModel transactionTraceWireModel)
        {
            try
            {
                return JsonConvert.SerializeObject(transactionTraceWireModel.TransactionTraceData);
            }
            catch (Exception exception)
            {
                return "Caught exception when trying to serialize TransactionTraceData: " + exception;
            }
        }
    }
}
