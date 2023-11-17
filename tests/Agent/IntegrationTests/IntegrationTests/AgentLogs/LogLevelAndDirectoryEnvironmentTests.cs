// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.IO;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentLogs
{
    public abstract class LogLevelAndDirectoryEnvironmentTests<T> : NewRelicIntegrationTest<T> where T : ConsoleDynamicMethodFixture
    {
        private readonly T _fixture;

        string _configLogLevel => "debug";
        string _configLogDirectory => @"X:\fake\path";

        string _envLogLevel => "info";
        string _generalEnvLogDirectory => Path.Combine(_fixture.RemoteApplication.DefaultLogFileDirectoryPath, "envLogs");
        string _profilerEnvLogDirectory => Path.Combine(_fixture.RemoteApplication.DefaultLogFileDirectoryPath, "profEnvLogs");

        public LogLevelAndDirectoryEnvironmentTests(T fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel(_configLogLevel);
                    _fixture.RemoteApplication.NewRelicConfig.SetLogDirectory(_configLogDirectory);

                    _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEWRELIC_LOG_LEVEL", _envLogLevel);
                    _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEWRELIC_LOG_DIRECTORY", _generalEnvLogDirectory);
                    _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEWRELIC_PROFILER_LOG_DIRECTORY", _profilerEnvLogDirectory);

                    _fixture.AddCommand("HttpClientDriver Get");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void AgentLog()
        {
            var configLocation = new AgentLogFile(_configLogDirectory, _fixture.TestLogger, throwIfNotFound: false);
            var generalEnvLocation = new AgentLogFile(_generalEnvLogDirectory, _fixture.TestLogger, throwIfNotFound: false);
            var profilerEnvLocation = new AgentLogFile(_profilerEnvLogDirectory, _fixture.TestLogger, throwIfNotFound: false);

            Assert.False(configLocation.Found);
            Assert.True(generalEnvLocation.Found);
            Assert.False(profilerEnvLocation.Found);

            var agentLines = generalEnvLocation.GetFileLines();

            Assert.DoesNotContain($"{_configLogLevel}: [pid: ", agentLines, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void ProfilerLog()
        {
            var configLocation = new ProfilerLogFile(_configLogDirectory, throwIfNotFound: false);
            var generalEnvLocation = new ProfilerLogFile(_generalEnvLogDirectory, throwIfNotFound: false);
            var profilerEnvLocation = new ProfilerLogFile(_profilerEnvLogDirectory, throwIfNotFound: false);

            Assert.False(configLocation.Found);
            Assert.False(generalEnvLocation.Found);
            Assert.True(profilerEnvLocation.Found);

            var agentLines = generalEnvLocation.GetFileLines();

            Assert.DoesNotContain($"<-- New logging level set: {_configLogLevel}", agentLines, StringComparer.OrdinalIgnoreCase);
        }
    }

    [NetFrameworkTest]
    public class LogLevelAndDirectoryEnvironmentTests_net48 : LogLevelAndDirectoryEnvironmentTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public LogLevelAndDirectoryEnvironmentTests_net48(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output) { }
    }

    [NetCoreTest]
    public class LogLevelAndDirectoryEnvironmentTestsOldest : LogLevelAndDirectoryEnvironmentTests<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public LogLevelAndDirectoryEnvironmentTestsOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output) { }
    }

    [NetCoreTest]
    public class LogLevelAndDirectoryEnvironmentTestsLatest : LogLevelAndDirectoryEnvironmentTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public LogLevelAndDirectoryEnvironmentTestsLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output) { }
    }
}
