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

		public override void Collect(TransactionTraceWireModelComponents transactionTraceWireModel)
		{
			foreach(var transactionCollector in _transactionCollectors)
			{
				transactionCollector?.Collect(transactionTraceWireModel);
			}
		}

		protected override void Harvest()
		{
			var traces = _transactionCollectors
				.Where(t => t != null)
				.SelectMany(t => t.GetCollectedSamples())
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
					ClearTransactionTraces(); // Only clear traces after successfully sending data
					break;
				case DataTransportResponseStatus.Retain:
				case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
				case DataTransportResponseStatus.Discard:
				default:
					break;
			}
		}

		private void ClearTransactionTraces()
		{
			foreach (var transactionCollector in _transactionCollectors)
			{
				transactionCollector?.ClearCollectedSamples();
			}
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			// It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
			// If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

			foreach(var transactionCollector in _transactionCollectors)
			{
				transactionCollector?.GetCollectedSamples();
			}
		}

		private void LogUnencodedTraceData(IEnumerable<TransactionTraceWireModel> samples)
		{
			if (Log.IsDebugEnabled)
			{
				foreach(var sample in samples)
				{
					if ( sample != null)
					{
						Log.DebugFormat("TransactionTraceData: {0}", SerializeTransactionTraceData(sample));
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
