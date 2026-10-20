// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class LinuxUnicodeSpecialCharactersTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const string DockerComposeServiceName = "LinuxUnicodeByteOrderMarkTestFixture";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble";

    protected LinuxUnicodeSpecialCharactersTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile) { }

    public void CreateJpnInstrumentationXml()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <extension xmlns="urn:newrelic-extension">
              <instrumentation>
                <!-- Define the method which triggers the creation of a transaction. -->

                <tracerFactory>
                  <match assemblyName="SmokeTestApp" className="ContainerizedAspNetCoreApp.Controllers.WeatherForecastController">
                    <exactMethodMatcher methodName="何かをする" />
                  </match>
                </tracerFactory>

              </instrumentation>
            </extension>
            """;

        System.IO.File.WriteAllText(
            System.IO.Path.Combine(DestinationNewRelicExtensionsDirectoryPath, "JpnInstrumentation.xml")
            , xml);
    }
}

public class LinuxUnicodeSpecialCharactersTestFixtureCoreOldest : LinuxUnicodeSpecialCharactersTestFixture
{
    public LinuxUnicodeSpecialCharactersTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class LinuxUnicodeSpecialCharactersTestFixtureCoreLatest : LinuxUnicodeSpecialCharactersTestFixture
{
    public LinuxUnicodeSpecialCharactersTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}