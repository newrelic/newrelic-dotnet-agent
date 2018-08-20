using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Aggregators
{
	public interface IErrorTraceAggregator
	{
		void Collect(ErrorTraceWireModel errorTraceWireModel);
	}

	public class ErrorTraceAggregator : AbstractAggregator<ErrorTraceWireModel>, IErrorTraceAggregator
	{
		private ICollection<ErrorTraceWireModel> _errorTraceWireModels = new ConcurrentList<ErrorTraceWireModel>();

		private uint _errorTraceCollectionMaximum;
		private readonly IAgentHealthReporter _agentHealthReporter;

		public ErrorTraceAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
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

		private void Retain(IEnumerable<ErrorTraceWireModel> errorTraceWireModels)
		{
			errorTraceWireModels = errorTraceWireModels.ToList();
			_agentHealthReporter.ReportErrorTracesRecollected(errorTraceWireModels.Count());

			// It is possible, but unlikely, to lose incoming error traces here due to a race condition
			var savedErrorTraceWireModels = _errorTraceWireModels;
			ResetCollections();

			// It is possible that newer, incoming error traces will be added to our collection before we add the retained and saved ones.
			foreach(var model in errorTraceWireModels)
			{
				if ( model != null)
				{
					AddToCollection(model);
				}
			}

			foreach(var model in savedErrorTraceWireModels)
			{
				if ( model != null)
				{
					AddToCollection(model);
				}
			}
		}

		private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<ErrorTraceWireModel> errorTraceWireModels)
		{
			switch (responseStatus)
			{
				case DataTransportResponseStatus.CommunicationError:
				case DataTransportResponseStatus.RequestTimeout:
				case DataTransportResponseStatus.ServerError:
				case DataTransportResponseStatus.ConnectionError:
					Retain(errorTraceWireModels);
					break;
				case DataTransportResponseStatus.RequestSuccessful:
					_agentHealthReporter.ReportErrorTracesSent(errorTraceWireModels.Count);
					break;
				case DataTransportResponseStatus.PostTooBigError:
				case DataTransportResponseStatus.OtherError:
				default:
					break;
			}
		}
	}
}
