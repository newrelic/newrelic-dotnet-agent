// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.SharedInterfaces;

namespace NewRelic.Agent.Core.Aggregators
{
    public abstract class AbstractAggregator<T> : ConfigurationBasedService
    {
        protected readonly IDataTransportService DataTransportService;
        protected readonly IServerlessModeDataTransportService ServerlessModeDataTransportService;
        private readonly IScheduler _scheduler;
        private readonly IProcessStatic _processStatic;

        protected AbstractAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic)
        {
            DataTransportService = dataTransportService;

            if (dataTransportService is IServerlessModeDataTransportService service)
                ServerlessModeDataTransportService = service;

            _scheduler = scheduler;
            _processStatic = processStatic;

            _subscriptions.Add<StopHarvestEvent>(OnStopHarvestEventAsync);
            _subscriptions.Add<AgentConnectedEvent>(OnAgentConnected);
            _subscriptions.Add<PreCleanShutdownEvent>(OnPreCleanShutdown);

            _subscriptions.Add<ManualHarvestEvent>(OnManualHarvest);
        }

        private void OnManualHarvest(ManualHarvestEvent manualHarvestEvent)
        {
            ManualHarvestAsync(manualHarvestEvent.TransactionId);
        }

        private async Task OnStopHarvestEventAsync(StopHarvestEvent obj)
        {
            await _scheduler.StopExecutingAsync(HarvestAsync, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }

        public abstract void Collect(T wireModel);

        protected abstract Task HarvestAsync();

        protected abstract Task ManualHarvestAsync(string transactionId);

        protected abstract bool IsEnabled { get; }

        protected virtual TimeSpan HarvestCycle => _configuration.DefaultHarvestCycle;

        private void OnAgentConnected(AgentConnectedEvent _)
        {
            if (IsEnabled)
            {
                _scheduler.ExecuteEvery(HarvestAsync, HarvestCycle);
            }
            else
            {
                _scheduler.StopExecuting(HarvestAsync, TimeSpan.FromSeconds(2));
            }
        }

        private void OnPreCleanShutdown(PreCleanShutdownEvent obj)
        {
            _scheduler.StopExecuting(HarvestAsync, TimeSpan.FromSeconds(2));

            if (!_configuration.CollectorSendDataOnExit || !IsEnabled)
                return;

            var uptime = DateTime.Now - _processStatic.GetCurrentProcess().StartTime;
            if (!(uptime.TotalMilliseconds > _configuration.CollectorSendDataOnExitThreshold))
                return;

            HarvestAsync();
        }

        public override void Dispose()
        {
            _scheduler.StopExecuting(HarvestAsync);
            base.Dispose();
        }

    }
}
