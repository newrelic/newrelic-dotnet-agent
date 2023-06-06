// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Aggregators
{
    public abstract class AbstractAggregator<T> : ConfigurationBasedService
    {
        protected readonly IDataTransportService DataTransportService;
        private readonly IScheduler _scheduler;
        private readonly IProcessStatic _processStatic;

        protected AbstractAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic)
        {
            DataTransportService = dataTransportService;
            _scheduler = scheduler;
            _processStatic = processStatic;

            _subscriptions.Add<StopHarvestEvent>(OnStopHarvestEvent);
            _subscriptions.Add<AgentConnectedEvent>(OnAgentConnected);
            _subscriptions.Add<PreCleanShutdownEvent>(OnPreCleanShutdown);
        }

        private void OnStopHarvestEvent(StopHarvestEvent obj)
        {
            _scheduler.StopExecuting(HarvestAction, TimeSpan.FromSeconds(2));
        }

        private void HarvestAction() => Task.Run(HarvestAsync).GetAwaiter().GetResult();

        public abstract void Collect(T wireModel);

        protected abstract Task HarvestAsync();

        protected abstract bool IsEnabled { get; }

        protected virtual TimeSpan HarvestCycle => _configuration.DefaultHarvestCycle;

        private void OnAgentConnected(AgentConnectedEvent _)
        {
            if (IsEnabled)
            {
                _scheduler.ExecuteEvery(HarvestAction, HarvestCycle);
            }
            else
            {
                _scheduler.StopExecuting(HarvestAction, TimeSpan.FromSeconds(2));
            }
        }

        private void OnPreCleanShutdown(PreCleanShutdownEvent obj)
        {
            _scheduler.StopExecuting(HarvestAction, TimeSpan.FromSeconds(2));

            if (!_configuration.CollectorSendDataOnExit || !IsEnabled)
                return;

            var uptime = DateTime.Now - _processStatic.GetCurrentProcess().StartTime;
            if (!(uptime.TotalMilliseconds > _configuration.CollectorSendDataOnExitThreshold))
                return;

            Task.Run(HarvestAsync).GetAwaiter().GetResult();
        }

        public override void Dispose()
        {
            base.Dispose();
            _scheduler.StopExecuting(HarvestAction);
        }

    }
}
