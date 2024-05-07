// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface IErrorTraceAggregator
    {
        void Collect(ErrorTraceWireModel errorTraceWireModel);
    }

    public class ErrorTraceAggregator : AbstractAggregator<ErrorTraceWireModel>, IErrorTraceAggregator
    {
        private readonly ReaderWriterLockSlim _readerWriterLock = new ReaderWriterLockSlim();

        private ICollection<ErrorTraceWireModel> _errorTraceWireModels = new ConcurrentList<ErrorTraceWireModel>();

        private uint _errorTraceCollectionMaximum;
        private readonly IAgentHealthReporter _agentHealthReporter;

        public ErrorTraceAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
            GetAndResetCollection();
        }

        public override void Dispose()
        {
            base.Dispose();
            _readerWriterLock.Dispose();
        }

        protected override TimeSpan HarvestCycle => _configuration.ErrorTracesHarvestCycle;

        protected override bool IsEnabled => _configuration.ErrorCollectorEnabled;

        public override void Collect(ErrorTraceWireModel errorTraceWireModel)
        {
            _agentHealthReporter.ReportErrorTraceCollected();

            _readerWriterLock.EnterReadLock();
            try
            {
                AddToCollection(errorTraceWireModel);
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }
        protected override void ManualHarvest(string transactionId) => InternalHarvest(transactionId);
        protected override void Harvest() => InternalHarvest();
        protected void InternalHarvest(string transactionId = null)
        {
            Log.Finest("Error Trace harvest starting.");

            ICollection<ErrorTraceWireModel> errorTraceWireModels;

            _readerWriterLock.EnterWriteLock();
            try
            {
                errorTraceWireModels = GetAndResetCollection();
            }
            finally
            {
                _readerWriterLock.ExitWriteLock();
            }

            if (errorTraceWireModels.Count <= 0)
                return;

            var responseStatus = DataTransportService.Send(errorTraceWireModels, transactionId);

            HandleResponse(responseStatus, errorTraceWireModels);

            Log.Finest("Error Trace harvest finished.");
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            GetAndResetCollection();
        }

        private ICollection<ErrorTraceWireModel> GetAndResetCollection()
        {
            _errorTraceCollectionMaximum = _configuration.ErrorsMaximumPerPeriod;
            return Interlocked.Exchange(ref _errorTraceWireModels, new ConcurrentList<ErrorTraceWireModel>());
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
            var savedErrorTraceWireModels = GetAndResetCollection();

            // It is possible that newer, incoming error traces will be added to our collection before we add the retained and saved ones.
            foreach (var model in errorTraceWireModels)
            {
                if (model != null)
                {
                    AddToCollection(model);
                }
            }

            foreach (var model in savedErrorTraceWireModels)
            {
                if (model != null)
                {
                    AddToCollection(model);
                }
            }
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<ErrorTraceWireModel> errorTraceWireModels)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    _agentHealthReporter.ReportErrorTracesSent(errorTraceWireModels.Count);
                    break;
                case DataTransportResponseStatus.Retain:
                    Retain(errorTraceWireModels);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                case DataTransportResponseStatus.Discard:
                default:
                    break;
            }
        }
    }
}
