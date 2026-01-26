// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.AgentHealth;

namespace NewRelic.Agent.Core.Utilities;

public interface IAgentTimer : IDisposable
{
    void Start();
    void StopAndRecordMetric();
}

public class AgentTimer : IAgentTimer
{
    public AgentTimer(IAgentHealthReporter agentHealthReporter, string eventName)
    {
        _agentHealthReporter = agentHealthReporter;
        _eventName = eventName;
    }

    private readonly IAgentHealthReporter _agentHealthReporter;
    private readonly string _eventName;
    private System.Diagnostics.Stopwatch _stopWatch;

    public void Start()
    {
        _stopWatch = System.Diagnostics.Stopwatch.StartNew();
    }

    public void StopAndRecordMetric()
    {
        _stopWatch.Stop();
        _agentHealthReporter.ReportAgentTimingMetric(_eventName, _stopWatch.Elapsed);
    }

    public void Dispose()
    {
        StopAndRecordMetric();
    }
}