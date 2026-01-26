// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Events;

namespace NewRelic.Agent.Core.Utilities;

public interface IAgentTimerService
{
    IAgentTimer StartNew(params string[] nameParts);
}

public class AgentTimerService : ConfigurationBasedService, IAgentTimerService
{

    private readonly ConcurrentDictionary<string, InterlockedLongCounter> _eventCounters = new ConcurrentDictionary<string, InterlockedLongCounter>();

    public AgentTimerService(IAgentHealthReporter agentHealthReporter)
    {
        _agentHealthReporter = agentHealthReporter;
    }

    private bool _enabled = false;
    private int _sampleFrequency = 1;

    private readonly IAgentHealthReporter _agentHealthReporter;



    public IAgentTimer StartNew(params string[] nameParts)
    {
        if (!_enabled || _sampleFrequency == 0)
        {
            return null;
        }

        var eventName = nameParts.Length == 1 ? nameParts[0] : string.Join("/", nameParts);

        if (_sampleFrequency == 1)
        {
            return StartNewImpl(eventName);
        }


        var eventCounter = _eventCounters.GetOrAdd(eventName, (nm) => new InterlockedLongCounter(-1));
        var idx = eventCounter.Increment();

        if (idx % _sampleFrequency == 0)
        {
            return StartNewImpl(eventName);
        }

        return null;
    }

    private IAgentTimer StartNewImpl(string eventName)
    {
        var timer = new AgentTimer(_agentHealthReporter, eventName);
        timer.Start();
        return timer;
    }

    protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
    {
        _sampleFrequency = _configuration.DiagnosticsCaptureAgentTimingFrequency;
        _enabled = _configuration.DiagnosticsCaptureAgentTiming && _sampleFrequency > 0;

    }
}
