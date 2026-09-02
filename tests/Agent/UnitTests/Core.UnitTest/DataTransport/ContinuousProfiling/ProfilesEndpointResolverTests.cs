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
    public void ResolveFromConnectionInfo_brackets_an_ipv6_literal_host()
    {
        // L11: UriBuilder brackets a raw IPv6 literal automatically -- documents the current
        // (correct) behavior rather than changing it. Real risk here is near-zero anyway: host is
        // always the NR-controlled redirect_host DNS name, never a raw IP.
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("::1");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);

        var endpoint = ProfilesEndpointResolver.ResolveFromConnectionInfo(connectionInfo);

        Assert.That(endpoint, Is.EqualTo("https://[::1]/v1/profiles"));
    }

    [Test]
    public void ResolveFromConnectionInfo_builds_an_http_endpoint_when_the_collector_protocol_is_http()
    {
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("http");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.eu01.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(80);

        var endpoint = ProfilesEndpointResolver.ResolveFromConnectionInfo(connectionInfo);

        Assert.That(endpoint, Is.EqualTo("http://collector.eu01.nr-data.net/v1/profiles"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void ResolveFromConnectionInfo_does_not_throw_for_a_null_or_blank_protocol(string protocol)
    {
        // UriBuilder tolerates a null/empty scheme rather than throwing -- it just omits the "://"
        // separator (e.g. "host:443/v1/profiles"). Pins the current no-throw behavior so a future
        // .NET UriBuilder change that starts throwing here is caught, since OnAgentConnected calls
        // ResolveFromConnectionInfo with no surrounding try/catch.
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns(protocol);
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.eu01.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);

        string endpoint = null;
        Assert.That(() => endpoint = ProfilesEndpointResolver.ResolveFromConnectionInfo(connectionInfo), Throws.Nothing);
        Assert.That(endpoint, Is.Not.Null);
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
