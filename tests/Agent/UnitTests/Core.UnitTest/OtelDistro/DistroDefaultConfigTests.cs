// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Xml.Serialization;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.SharedInterfaces.Web;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture, Category("Configuration")]
public class DistroDefaultConfigTests
{
    // The distro plugin's newrelic.config, linked into this project as test content (see Core.UnitTest.csproj).
    private const string DistroConfigPath = "OtelDistro/newrelic.config";

    private DefaultConfiguration _defaultConfig;

    [SetUp]
    public void SetUp()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, DistroConfigPath);
        var serializer = new XmlSerializer(typeof(configuration), new XmlRootAttribute { ElementName = "configuration", Namespace = "urn:newrelic-config" });

        configuration localConfiguration;
        using (var reader = new StreamReader(path))
        {
            localConfiguration = (configuration)serializer.Deserialize(reader);
        }

        // Environment is mocked, so no NEW_RELIC_* override is visible: values below come from the file alone.
        _defaultConfig = new TestableDefaultConfiguration(Mock.Create<IEnvironment>(), localConfiguration, new ServerConfiguration(), new RunTimeConfiguration(),
            Mock.Create<IBootstrapConfiguration>(), Mock.Create<IProcessStatic>(), Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(), Mock.Create<IDnsStatic>());
    }

    [Test]
    public void DistroConfig_EnablesOpenTelemetryAndTraces()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_defaultConfig.OpenTelemetryEnabled, Is.True);
            Assert.That(_defaultConfig.OpenTelemetryTracingEnabled, Is.True);
        });
    }

    [Test]
    public void DistroConfig_IncludesAspNetCoreHttpAndSqlActivitySources()
    {
        Assert.That(_defaultConfig.OpenTelemetryTracingIncludedActivitySources,
            Is.EquivalentTo(new[] { "Microsoft.AspNetCore", "System.Net.Http", "Microsoft.Data.SqlClient", "System.Data.SqlClient" }));
    }
}
