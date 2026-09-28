// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Metrics;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ContinuousProfilingServiceFactoryTests
{
    private IContainer _container;
    private IConfiguration _configuration;
    private IAgentHealthReporter _agentHealthReporter;

    [SetUp]
    public void SetUp()
    {
        _container = Mock.Create<IContainer>();
        _configuration = Mock.Create<IConfiguration>();
        _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
    }

    [TearDown]
    public void TearDown()
    {
        _container.Dispose();
    }

    [Test]
    public void TryCreate_AlwaysConstructsTheService_RegardlessOfConfiguration()
    {
        Mock.Arrange(() => _configuration.ContinuousProfilingEnabled).Returns(false);

        var result = ContinuousProfilingServiceFactory.TryCreate(_container, _configuration, _agentHealthReporter);

        Assert.That(result, Is.Not.Null);
        Mock.Assert(() => _container.Resolve<INativeMethods>(), Occurs.Once());
    }

    [Test]
    public void TryCreate_ResolvesContinuousProfilingSpecificSupportabilityCounters_NotTheSharedOtelBridgeOnes()
    {
        Mock.Arrange(() => _configuration.ContinuousProfilingEnabled).Returns(true);

        var result = ContinuousProfilingServiceFactory.TryCreate(_container, _configuration, _agentHealthReporter);

        Assert.That(result, Is.Not.Null);
        Mock.Assert(() => _container.Resolve<IContinuousProfilingSupportabilityMetricCounters>(), Occurs.Once());
        Mock.Assert(() => _container.Resolve<IOtelBridgeSupportabilityMetricCounters>(), Occurs.Never());
    }

    [Test]
    public void TryCreate_ReturnsNullWithoutThrowing_WhenConstructionFails()
    {
        Mock.Arrange(() => _configuration.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _container.Resolve<INativeMethods>())
            .Throws(new EntryPointNotFoundException("simulated P/Invoke resolution failure"));

        ContinuousProfilingService result = null;

        Assert.DoesNotThrow(() => result = ContinuousProfilingServiceFactory.TryCreate(_container, _configuration, _agentHealthReporter));
        Assert.That(result, Is.Null);
    }
}
