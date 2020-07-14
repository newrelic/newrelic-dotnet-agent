using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using MoreLinq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Aggregators
{
	public interface IErrorTraceAggregator
	{
		void Collect([NotNull] ErrorTraceWireModel errorTraceWireModel);
	}

	public class ErrorTraceAggregator : AbstractAggregator<ErrorTraceWireModel>, IErrorTraceAggregator
	{
		[NotNull]
		private ICollection<ErrorTraceWireModel> _errorTraceWireModels = new ConcurrentList<ErrorTraceWireModel>();

		[NotNull]
		private uint _errorTraceCollectionMaximum = 0;
		[NotNull]
		private readonly IAgentHealthReporter _agentHealthReporter;

		public ErrorTraceAggregator([NotNull] IDataTransportService dataTransportService, [NotNull] IScheduler scheduler, [NotNull] IProcessStatic processStatic, [NotNull] IAgentHealthReporter agentHealthReporter)
			: base(dataTransportService, scheduler, processStatic)
		{
			_agentHealthReporter = agentHealthReporter;
			ResetCollections();
		}

		public override void Collect(ErrorTraceWireModel errorTraceWireModel)
		{
			_agentHealthReporter.ReportErrorTraceCollected();
			AddToCollection(errorTraceWireModel);
		}

		protected override void Harvest()
		{
			var errorTraceWireModels = _errorTraceWireModels;
			ResetCollections();

			if (errorTraceWireModels.Count <= 0)
				return;

			_agentHealthReporter.ReportErrorTracesSent(errorTraceWireModels.Count);
			var responseStatus = DataTransportService.Send(errorTraceWireModels);

			HandleResponse(responseStatus, errorTraceWireModels);
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			// It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
			// If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

			ResetCollections();
		}

		private void ResetCollections()
		{
			_errorTraceWireModels = new ConcurrentList<ErrorTraceWireModel>();
			_errorTraceCollectionMaximum = _configuration.ErrorsMaximumPerPeriod;
			// TODO: Add collection for synthetics once synthetics is implemented
		}

		private void AddToCollection(ErrorTraceWireModel errorTraceWireModel)
		{
			if (_errorTraceWireModels.Count >= _errorTraceCollectionMaximum)
				return;

			_errorTraceWireModels.Add(errorTraceWireModel);
		}

		private void Retain([NotNull] IEnumerable<ErrorTraceWireModel> errorTraceWireModels)
		{
			errorTraceWireModels = errorTraceWireModels.ToList();
			_agentHealthReporter.ReportErrorTracesRecollected(errorTraceWireModels.Count());

			// It is possible, but unlikely, to lose incoming error traces here due to a race condition
			var savedErrorTraceWireModels = _errorTraceWireModels;
			ResetCollections();

			// It is possible that newer, incoming error traces will be added to our collection before we add the retained and saved ones.
			errorTraceWireModels
				.Where(@error => @error != null)
				.ForEach(AddToCollection);
			savedErrorTraceWireModels
				.Where(@error => @error != null)
				.ForEach(AddToCollection);
		}

		private void HandleResponse(DataTransportResponseStatus responseStatus, [NotNull] IEnumerable<ErrorTraceWireModel> errorTraceWireModels)
		{
			switch (responseStatus)
			{
				case DataTransportResponseStatus.ServiceUnavailableError:
				case DataTransportResponseStatus.ConnectionError:
					Retain(errorTraceWireModels);
					break;
				case DataTransportResponseStatus.PostTooBigError:
				case DataTransportResponseStatus.OtherError:
				case DataTransportResponseStatus.RequestSuccessful:
				default:
					break;
			}
		}
	}
}
