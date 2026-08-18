// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DataTransport.ContinuousProfiling;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.DataTransport.ContinuousProfiling;

[TestFixture]
public class ProfilesEndpointResolverTests
{
    [Test]
    public void ResolveFromConnectionInfo_builds_the_profiles_path_on_the_collectors_host()
    {
        // Standard port (443 for https) is omitted, same as MeterBridgeConfiguration.BuildOtlpEndpoint's
        // UriBuilder-based endpoint -- not written out explicitly like a non-default port would be.
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.eu01.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);

        var endpoint = ProfilesEndpointResolver.ResolveFromConnectionInfo(connectionInfo);

        Assert.That(endpoint, Is.EqualTo("https://collector.eu01.nr-data.net/v1/profiles"));
    }

    [Test]
    public void ResolveFromConnectionInfo_includes_a_non_default_port()
    {
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.eu01.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(8080);

        var endpoint = ProfilesEndpointResolver.ResolveFromConnectionInfo(connectionInfo);

        Assert.That(endpoint, Is.EqualTo("https://collector.eu01.nr-data.net:8080/v1/profiles"));
    }

    [Test]
    public void ResolveFromConnectionInfo_returns_null_for_a_null_connection_info()
    {
        Assert.That(ProfilesEndpointResolver.ResolveFromConnectionInfo(null), Is.Null);
    }

    [Test]
    public void ResolveFromConnectionInfo_returns_null_when_the_host_is_empty()
    {
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.Host).Returns(string.Empty);

        Assert.That(ProfilesEndpointResolver.ResolveFromConnectionInfo(connectionInfo), Is.Null);
    }
}
