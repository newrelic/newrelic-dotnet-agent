// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest;

[TestFixture]
public class AgentManagerEnvironmentTests
{
    private AgentManagerEnvironment _environment;

    [SetUp]
    public void SetUp()
    {
        _environment = new AgentManagerEnvironment();
    }

    [TearDown]
    public void TearDown()
    {
        Agent.IsAgentShuttingDown = false;
    }

    [Test]
    public void CreateContainer_ReturnsAgentContainer()
    {
        using var container = _environment.CreateContainer();

        Assert.That(container, Is.InstanceOf<AgentContainer>());
    }

    [Test]
    public void ExtensionsDirectory_MatchesAgentInstallConfiguration()
    {
        Assert.That(_environment.ExtensionsDirectory, Is.EqualTo(AgentInstallConfiguration.InstallPathExtensionsDirectory));
    }

    [Test]
    public void MarkAgentShuttingDown_SetsFlag()
    {
        Agent.IsAgentShuttingDown = false;

        _environment.MarkAgentShuttingDown();

        Assert.That(Agent.IsAgentShuttingDown, Is.True);
    }

    [Test]
    public void KillAgentSubscription_ReceivesPublishedEvent_AndUnsubscribeStops()
    {
        var received = 0;
        Action<KillAgentEvent> handler = _ => received++;

        try
        {
            _environment.SubscribeToKillAgent(handler);
            EventBus<KillAgentEvent>.Publish(new KillAgentEvent());

            _environment.UnsubscribeFromKillAgent(handler);
            EventBus<KillAgentEvent>.Publish(new KillAgentEvent());
        }
        finally
        {
            EventBus<KillAgentEvent>.Unsubscribe(handler);
        }

        Assert.That(received, Is.EqualTo(1));
    }

    [Test]
    public void PublishCleanShutdownEvents_PublishesPreCleanThenClean()
    {
        var order = new List<string>();
        Action<PreCleanShutdownEvent> preClean = _ => order.Add("PreCleanShutdownEvent");
        Action<CleanShutdownEvent> clean = _ => order.Add("CleanShutdownEvent");

        try
        {
            EventBus<PreCleanShutdownEvent>.Subscribe(preClean);
            EventBus<CleanShutdownEvent>.Subscribe(clean);

            _environment.PublishCleanShutdownEvents();
        }
        finally
        {
            EventBus<PreCleanShutdownEvent>.Unsubscribe(preClean);
            EventBus<CleanShutdownEvent>.Unsubscribe(clean);
        }

        Assert.That(order, Is.EqualTo(new[] { "PreCleanShutdownEvent", "CleanShutdownEvent" }));
    }
}
