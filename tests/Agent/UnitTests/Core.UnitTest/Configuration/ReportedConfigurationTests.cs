// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Configuration;

[TestFixture]
public class ReportedConfigurationTests
{
    private IConfiguration _configuration;
    private ReportedConfiguration _reportedConfiguration;

    [SetUp]
    public void SetUp()
    {
        _configuration = Mock.Create<IConfiguration>();
        _reportedConfiguration = new ReportedConfiguration(_configuration);
    }

    [Test]
    public void AgentRunId_returns_null_when_underlying_value_is_null()
    {
        Mock.Arrange(() => _configuration.AgentRunId).Returns((object)null);

        Assert.That(_reportedConfiguration.AgentRunId, Is.Null);
    }

    [Test]
    public void AgentRunId_returns_stringified_value_when_underlying_value_is_set()
    {
        Mock.Arrange(() => _configuration.AgentRunId).Returns(12345);

        Assert.That(_reportedConfiguration.AgentRunId, Is.EqualTo("12345"));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("a-license-key", true)]
    public void AgentLicenseKeyConfigured_reflects_whitespace_check(string licenseKey, bool expected)
    {
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(licenseKey);

        Assert.That(_reportedConfiguration.AgentLicenseKeyConfigured, Is.EqualTo(expected));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("proxyhost", true)]
    public void ProxyHostConfigured_reflects_whitespace_check(string proxyHost, bool expected)
    {
        Mock.Arrange(() => _configuration.ProxyHost).Returns(proxyHost);

        Assert.That(_reportedConfiguration.ProxyHostConfigured, Is.EqualTo(expected));
    }

    [Test]
    public void ProxyPortConfigured_is_always_true()
    {
        Assert.That(_reportedConfiguration.ProxyPortConfigured, Is.True);
    }

    [Test]
    public void RootSamplerName_delegates_to_ToSamplerTypeString()
    {
        Mock.Arrange(() => _configuration.RootSamplerType).Returns(SamplerType.AlwaysOn);

        Assert.That(_reportedConfiguration.RootSamplerName, Is.EqualTo(SamplerType.AlwaysOn.ToSamplerTypeString()));
    }

    [Test]
    public void EventListenerSamplersEnabled_setter_is_a_no_op()
    {
        Mock.Arrange(() => _configuration.EventListenerSamplersEnabled).Returns(true);

        _reportedConfiguration.EventListenerSamplersEnabled = false;

        Assert.That(_reportedConfiguration.EventListenerSamplersEnabled, Is.True);
    }

    [Test]
    public void AzureFunctionResourceIdWithFunctionName_delegates_to_underlying_configuration()
    {
        Mock.Arrange(() => _configuration.AzureFunctionResourceIdWithFunctionName("MyFunction")).Returns("resource-id");

        Assert.That(_reportedConfiguration.AzureFunctionResourceIdWithFunctionName("MyFunction"), Is.EqualTo("resource-id"));
    }

    [Test]
    public void GetAppSettings_delegates_to_underlying_configuration()
    {
        var appSettings = new Dictionary<string, string> { { "key", "value" } };
        Mock.Arrange(() => _configuration.GetAppSettings()).Returns(appSettings);

        Assert.That(_reportedConfiguration.GetAppSettings(), Is.SameAs(appSettings));
    }
}
