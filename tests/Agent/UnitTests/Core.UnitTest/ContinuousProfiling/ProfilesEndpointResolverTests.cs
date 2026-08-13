// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.DataTransport;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ProfilesEndpointResolverTests
{
    [Test]
    public void ResolveFromConnectionInfo_builds_the_profiles_path_on_the_collectors_host_and_port()
    {
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.eu01.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);

        var endpoint = ProfilesEndpointResolver.ResolveFromConnectionInfo(connectionInfo);

        Assert.That(endpoint, Is.EqualTo("https://collector.eu01.nr-data.net:443/v1/profiles"));
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
