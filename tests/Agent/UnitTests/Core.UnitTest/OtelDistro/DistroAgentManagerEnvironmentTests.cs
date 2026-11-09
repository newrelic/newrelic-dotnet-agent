// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroAgentManagerEnvironmentTests
{
    // AppDomain.ProcessExit and DomainUnload cannot be raised in-process without reflection into private CLR
    // members, so these tests cover the subscribe/unsubscribe paths; the IIS/console distro runs prove the handler fires.
    [Test]
    public void SubscribeToExit_HooksProcessExitAndDomainUnload()
    {
        var environment = new DistroAgentManagerEnvironment();
        var calls = 0;
        EventHandler handler = (sender, e) => calls++;

        Assert.DoesNotThrow(() => environment.SubscribeToExit(handler));
        Assert.DoesNotThrow(() => environment.UnsubscribeFromExit(handler));
        Assert.That(calls, Is.EqualTo(0));
    }

    [Test]
    public void UnsubscribeFromExit_WithoutSubscribe_DoesNotThrow()
    {
        var environment = new DistroAgentManagerEnvironment();

        Assert.DoesNotThrow(() => environment.UnsubscribeFromExit((sender, e) => { }));
    }

    [Test]
    public void IsAnAgentManagerEnvironment()
    {
        Assert.That(new DistroAgentManagerEnvironment(), Is.InstanceOf<AgentManagerEnvironment>());
    }
}
