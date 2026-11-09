// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Config;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroBootstrapConfigurationTests
{
    private IBootstrapConfiguration _inner;
    private DistroBootstrapConfiguration _wrapper;

    [SetUp]
    public void SetUp()
    {
        _inner = Mock.Create<IBootstrapConfiguration>();
        _wrapper = new DistroBootstrapConfiguration(_inner);
    }

    [Test]
    public void Ctor_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DistroBootstrapConfiguration(null));
    }

    [Test]
    public void DistroModeEnabled_IsAlwaysTrue()
    {
        Assert.That(_wrapper.DistroModeEnabled, Is.True);
    }

    [Test]
    public void EveryOtherMember_ForwardsToInner()
    {
        var logConfig = Mock.Create<ILogConfig>();

        Mock.Arrange(() => _inner.ServerlessModeEnabled).Returns(true);
        Mock.Arrange(() => _inner.DebugStartupDelaySeconds).Returns(7);
        Mock.Arrange(() => _inner.ConfigurationFileName).Returns("newrelic.config");
        Mock.Arrange(() => _inner.AgentEnabled).Returns(true);
        Mock.Arrange(() => _inner.AgentEnabledAt).Returns("web.config");
        Mock.Arrange(() => _inner.LogConfig).Returns(logConfig);
        Mock.Arrange(() => _inner.ServerlessFunctionName).Returns("MyFunc");
        Mock.Arrange(() => _inner.ServerlessFunctionVersion).Returns("2");
        Mock.Arrange(() => _inner.AzureFunctionModeDetected).Returns(true);
        Mock.Arrange(() => _inner.GCSamplerV2Enabled).Returns(true);
        Mock.Arrange(() => _inner.AgentControlEnabled).Returns(true);
        Mock.Arrange(() => _inner.HealthDeliveryLocation).Returns("/tmp/health");
        Mock.Arrange(() => _inner.HealthFrequency).Returns(5);

        Assert.Multiple(() =>
        {
            Assert.That(_wrapper.ServerlessModeEnabled, Is.True);
            Assert.That(_wrapper.DebugStartupDelaySeconds, Is.EqualTo(7));
            Assert.That(_wrapper.ConfigurationFileName, Is.EqualTo("newrelic.config"));
            Assert.That(_wrapper.AgentEnabled, Is.True);
            Assert.That(_wrapper.AgentEnabledAt, Is.EqualTo("web.config"));
            Assert.That(_wrapper.LogConfig, Is.SameAs(logConfig));
            Assert.That(_wrapper.ServerlessFunctionName, Is.EqualTo("MyFunc"));
            Assert.That(_wrapper.ServerlessFunctionVersion, Is.EqualTo("2"));
            Assert.That(_wrapper.AzureFunctionModeDetected, Is.True);
            Assert.That(_wrapper.GCSamplerV2Enabled, Is.True);
            Assert.That(_wrapper.AgentControlEnabled, Is.True);
            Assert.That(_wrapper.HealthDeliveryLocation, Is.EqualTo("/tmp/health"));
            Assert.That(_wrapper.HealthFrequency, Is.EqualTo(5));
        });
    }
}
