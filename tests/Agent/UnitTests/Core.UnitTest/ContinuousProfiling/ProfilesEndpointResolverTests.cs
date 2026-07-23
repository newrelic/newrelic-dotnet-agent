// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ProfilesEndpointResolverTests
{
    private IConfiguration _configuration;

    [SetUp]
    public void SetUp()
    {
        _configuration = Mock.Create<IConfiguration>();
    }

    [Test]
    public void Resolve_uses_us_endpoint_for_a_non_regional_license_key()
    {
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(new string('0', 40));

        var endpoint = ProfilesEndpointResolver.Resolve(_configuration, _ => null);

        Assert.That(endpoint, Is.EqualTo("https://otlp.nr-data.net/v1/profiles"));
    }

    [Test]
    public void Resolve_derives_the_regional_endpoint_from_a_regional_license_key()
    {
        // A region-prefixed license key of the form "eu01x..." selects the region-aware host.
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("eu01x" + new string('0', 35));

        var endpoint = ProfilesEndpointResolver.Resolve(_configuration, _ => null);

        Assert.That(endpoint, Is.EqualTo("https://otlp.eu01.nr-data.net/v1/profiles"));
    }

    [Test]
    public void Resolve_prefers_a_full_url_override_verbatim()
    {
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("eu01x" + new string('0', 35));

        var endpoint = ProfilesEndpointResolver.Resolve(_configuration,
            name => name == ProfilesEndpointResolver.EndpointOverrideEnvVar ? "https://custom.example/v1development/profiles" : null);

        Assert.That(endpoint, Is.EqualTo("https://custom.example/v1development/profiles"));
    }

    [Test]
    public void Resolve_appends_the_profiles_path_to_a_host_only_override()
    {
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(new string('0', 40));

        var endpoint = ProfilesEndpointResolver.Resolve(_configuration,
            name => name == ProfilesEndpointResolver.EndpointOverrideEnvVar ? "https://otlp.gov.nr-data.net" : null);

        Assert.That(endpoint, Is.EqualTo("https://otlp.gov.nr-data.net/v1/profiles"));
    }

    [Test]
    public void Resolve_appends_the_profiles_path_to_a_host_only_override_with_trailing_slash()
    {
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(new string('0', 40));

        var endpoint = ProfilesEndpointResolver.Resolve(_configuration,
            name => name == ProfilesEndpointResolver.EndpointOverrideEnvVar ? "https://otlp.gov.nr-data.net/" : null);

        Assert.That(endpoint, Is.EqualTo("https://otlp.gov.nr-data.net/v1/profiles"));
    }

    [Test]
    public void Resolve_falls_back_to_us_endpoint_when_license_key_is_null()
    {
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns((string)null);

        var endpoint = ProfilesEndpointResolver.Resolve(_configuration, _ => null);

        Assert.That(endpoint, Is.EqualTo("https://otlp.nr-data.net/v1/profiles"));
    }

    [Test]
    public void Resolve_ignores_a_blank_override()
    {
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(new string('0', 40));

        var endpoint = ProfilesEndpointResolver.Resolve(_configuration,
            name => name == ProfilesEndpointResolver.EndpointOverrideEnvVar ? "   " : null);

        Assert.That(endpoint, Is.EqualTo("https://otlp.nr-data.net/v1/profiles"));
    }

    [Test]
    public void Resolve_tolerates_a_throwing_environment_reader()
    {
        Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(new string('0', 40));

        Assert.That(() => ProfilesEndpointResolver.Resolve(_configuration, _ => throw new InvalidOperationException("boom")),
            Throws.Nothing);
    }
}
