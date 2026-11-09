// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroAgentHostTests
{
    [SetUp]
    public void SetUp()
    {
        DistroAgentHost.ResetForTesting();
    }

    [TearDown]
    public void TearDown()
    {
        DistroAgentHost.ResetForTesting();
    }

    [Test]
    public void Start_CalledTwice_ReturnsSameInstance()
    {
        var agent = Mock.Create<IDistroAgent>();
        var factoryCalls = 0;
        Func<IDistroAgent> factory = () =>
        {
            factoryCalls++;
            return agent;
        };

        var first = DistroAgentHost.Start(factory);
        var second = DistroAgentHost.Start(factory);

        Assert.Multiple(() =>
        {
            Assert.That(factoryCalls, Is.EqualTo(1));
            Assert.That(first, Is.SameAs(agent));
            Assert.That(second, Is.SameAs(first));
        });
    }

    [Test]
    public void Start_WhenFactoryThrows_ThrowsAndAllowsRetry()
    {
        var agent = Mock.Create<IDistroAgent>();
        var expected = new InvalidOperationException("start-up failed");

        var thrown = Assert.Throws<InvalidOperationException>(() => DistroAgentHost.Start(() => throw expected));
        var retried = DistroAgentHost.Start(() => agent);

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(expected));
            Assert.That(retried, Is.SameAs(agent));
        });
    }

    [Test]
    public void Start_WhenFactoryReturnsNull_ThrowsAndAllowsRetry()
    {
        var agent = Mock.Create<IDistroAgent>();

        Assert.Throws<InvalidOperationException>(() => DistroAgentHost.Start(() => null));
        var retried = DistroAgentHost.Start(() => agent);

        Assert.That(retried, Is.SameAs(agent));
    }

    [Test]
    public void Start_WithNullFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DistroAgentHost.Start(null));
    }

    [Test]
    public void Current_BeforeStart_IsNull()
    {
        Assert.That(DistroAgentHost.Current, Is.Null);
    }

    [Test]
    public void Current_AfterStart_ReturnsSameInstanceWithoutCallingFactoryAgain()
    {
        var agent = Mock.Create<IDistroAgent>();
        var factoryCalls = 0;

        var started = DistroAgentHost.Start(() =>
        {
            factoryCalls++;
            return agent;
        });

        Assert.Multiple(() =>
        {
            Assert.That(DistroAgentHost.Current, Is.SameAs(started));
            Assert.That(DistroAgentHost.Current, Is.SameAs(agent));
            Assert.That(factoryCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Current_AfterResetForTesting_IsNull()
    {
        DistroAgentHost.Start(() => Mock.Create<IDistroAgent>());

        DistroAgentHost.ResetForTesting();

        Assert.That(DistroAgentHost.Current, Is.Null);
    }

    [Test]
    public void ResetForTesting_ClearsInstance_SoNextStartInvokesFactoryAgain()
    {
        var firstAgent = Mock.Create<IDistroAgent>();
        var secondAgent = Mock.Create<IDistroAgent>();

        DistroAgentHost.Start(() => firstAgent);
        DistroAgentHost.ResetForTesting();
        var afterReset = DistroAgentHost.Start(() => secondAgent);

        Assert.That(afterReset, Is.SameAs(secondAgent));
    }
}
