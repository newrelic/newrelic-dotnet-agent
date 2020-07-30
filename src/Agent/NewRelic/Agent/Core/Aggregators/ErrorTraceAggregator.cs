/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using System.Linq;
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
        void Collect(ErrorTraceWireModel errorTraceWireModel);
    }

    public class ErrorTraceAggregator : AbstractAggregator<ErrorTraceWireModel>, IErrorTraceAggregator
    {
        private ICollection<ErrorTraceWireModel> _errorTraceWireModels = new ConcurrentList<ErrorTraceWireModel>();
        private uint _errorTraceCollectionMaximum = 0;
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
            errorTraceWireModels
                .Where(@error => @error != null)
                .ForEach(AddToCollection);
            savedErrorTraceWireModels
                .Where(@error => @error != null)
                .ForEach(AddToCollection);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, IEnumerable<ErrorTraceWireModel> errorTraceWireModels)
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
