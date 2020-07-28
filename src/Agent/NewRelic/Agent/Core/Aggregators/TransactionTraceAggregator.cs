using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.TransactionTraces;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using Newtonsoft.Json;

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

        public override void Collect(TransactionTraceWireModelComponents transactionTraceWireModel)
        {
            _transactionCollectors.ForEach(collector => collector?.Collect(transactionTraceWireModel));
        }

        protected override void Harvest()
        {
            var traces = _transactionCollectors
                .Where(t => t != null)
                .SelectMany(t => t.GetAndClearCollectedSamples())
                .Distinct()
                .Select(t => t.CreateWireModel())
                .ToList();

            if (!traces.Any())
                return;

            LogUnencodedTraceData(traces);

            var responseStatus = DataTransportService.Send(traces);
            HandleResponse(responseStatus, traces);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<TransactionTraceWireModel> traces)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                case DataTransportResponseStatus.ServiceUnavailableError:
                case DataTransportResponseStatus.ConnectionError:
                case DataTransportResponseStatus.PostTooBigError:
                case DataTransportResponseStatus.OtherError:
                default:
                    break;
            }
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            _transactionCollectors.ForEach(collector => collector?.GetAndClearCollectedSamples());
        }

        private void LogUnencodedTraceData(IEnumerable<TransactionTraceWireModel> samples)
        {
            if (Log.IsDebugEnabled)
            {
                samples
                    .Where(transactionSample => transactionSample != null)
                    .Select(SerializeTransactionTraceData)
                    .ForEach(serializedTransactionData => Log.DebugFormat("TransactionTraceData: {0}", serializedTransactionData));
            }
        }

        private static String SerializeTransactionTraceData(TransactionTraceWireModel transactionTraceWireModel)
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
