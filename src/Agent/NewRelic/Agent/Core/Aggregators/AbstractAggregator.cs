// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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
        protected readonly IScheduler _scheduler;
        private readonly IProcessStatic _processStatic;
        protected readonly TimeSpan DefaultHarvestCycle = TimeSpan.FromMinutes(1);

        protected AbstractAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic)
        {
            DataTransportService = dataTransportService;
            _scheduler = scheduler;
            _processStatic = processStatic;

            _subscriptions.Add<AgentConnectedEvent>(OnAgentConnected);
            _subscriptions.Add<PreCleanShutdownEvent>(OnPreCleanShutdown);
        }

        public abstract void Collect(T wireModel);

        protected abstract void Harvest();

        protected abstract bool IsEnabled { get; }

        protected virtual TimeSpan HarvestCycle => DefaultHarvestCycle;

        private void OnAgentConnected(AgentConnectedEvent _)
        {
            if (IsEnabled)
            {
                _scheduler.ExecuteEvery(Harvest, HarvestCycle);
            }
            else
            {
                _scheduler.StopExecuting(Harvest, TimeSpan.FromSeconds(2));
            }
        }

        private void OnPreCleanShutdown(PreCleanShutdownEvent obj)
        {
            _scheduler.StopExecuting(Harvest, TimeSpan.FromSeconds(2));

            if (!_configuration.CollectorSendDataOnExit || !IsEnabled)
                return;

            var uptime = DateTime.Now - _processStatic.GetCurrentProcess().StartTime;
            if (!(uptime.TotalMilliseconds > _configuration.CollectorSendDataOnExitThreshold))
                return;

            Harvest();
        }

        public override void Dispose()
        {
            base.Dispose();
            _scheduler.StopExecuting(Harvest);
        }
    }
}
