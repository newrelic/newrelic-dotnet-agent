// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

/// <summary>
/// The one CP test that validates the <b>actual OTLP bytes on the wire</b>, not just the agent's own
/// debug-log dump. It runs against <see cref="AspNetCoreWebApiWithCollectorFixture"/> (a MockNewRelic-backed
/// fixture), so the profiles endpoint (resolved by the agent's <c>ProfilesEndpointResolver</c>)
/// resolves to the mock collector's own host+port with <c>/v1/profiles</c> appended. The mock's
/// <c>OtlpProfilesController</c> independently gzip-decodes and protobuf-parses each POST as a real
/// <c>ExportProfilesServiceRequest</c> and records a structural summary this test reads back.
///
/// Continuous profiling is enabled by config so a session starts at agent init; the web api's
/// <c>BurnCpu</c> action does several seconds of synchronous, inline CPU work on the request thread so the
/// sampler reliably captures on-CPU stacks and a non-empty profile is built, POSTed, received, and parsed.
/// The mock must be read back inside <c>exerciseApplication</c> -- <see cref="MockNewRelicFixture.Initialize"/>
/// shuts the collector process down as soon as it returns, so a <c>[Fact]</c> calling the collector would hit
/// a torn-down port.
/// </summary>
public class ContinuousProfilingOtlpReceiverTests : NewRelicIntegrationTest<AspNetCoreWebApiWithCollectorFixture>
{
    private const int SamplingIntervalMs = 1000;

    private readonly AspNetCoreWebApiWithCollectorFixture _fixture;
    private List<ProfilesSummaryDto> _receivedProfiles = new List<ProfilesSummaryDto>();

    private static readonly string SessionStartedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session started; sampling every (\d+) ms, draining every (\d+) ms\.";

    private static readonly string BuiltProfileLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"\[ContinuousProfiling\] Posting profile \((\w+)\); (\d+) bytes to (\S+)\.";

    public ContinuousProfilingOtlpReceiverTests(AspNetCoreWebApiWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");

                _fixture.EnvironmentVariables["NEW_RELIC_PROFILING_ENABLED"] = "true";
                _fixture.EnvironmentVariables["NEW_RELIC_CONTINUOUS_PROFILING_SAMPLING_INTERVAL_MS"] = SamplingIntervalMs.ToString();
            },
            exerciseApplication: () =>
            {
                // Session starts at agent init; confirm before generating sampleable work.
                _fixture.AgentLog.WaitForLogLine(SessionStartedLogLineRegex, TimeSpan.FromMinutes(1));

                // Synchronous 8s of on-CPU work on the request thread spans several sampling intervals so a
                // non-empty profile is built and POSTed to the mock's /v1/profiles endpoint.
                _fixture.BurnCpu(8);

                // A drain must build a non-empty ("built") profile before the mock can receive one.
                _fixture.AgentLog.WaitForLogLine(BuiltProfileLogLineRegex, TimeSpan.FromMinutes(2));

                // Poll the mock collector until it has independently parsed a CONTENT-valid profile -- one
                // that carries real decoded frames and resource identity, not just non-zero counts. Must
                // happen here, before Initialize() tears the collector process down.
                _receivedProfiles = WaitForContentValidProfile(TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    private List<ProfilesSummaryDto> WaitForContentValidProfile(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var latest = new List<ProfilesSummaryDto>();
        while (stopwatch.Elapsed < timeout)
        {
            latest = _fixture.GetCollectedOtlpProfiles(100).ToList();
            if (latest.Any(p => p.ContentValid))
            {
                return latest;
            }

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        return latest;
    }

    // Single [Fact]: AspNetCoreWebApiWithCollectorFixture/MockNewRelicFixture tears the mock collector's
    // process and test-output context down as soon as the ctor's exerciseApplication returns (see
    // MockNewRelicFixture.Initialize()), so it supports only one [Fact] per test class -- a second test
    // method's construction re-enters Initialize() against an already-torn-down process/logger and throws
    // "There is no currently active test." (matches the documented constraint in
    // ContinuousProfilerAgentCommandTests). All three assertion groups (M4's structural, content, and
    // entity.guid checks) are combined here rather than split across [Fact]s.
    [Fact]
    public void MockCollectorReceivesValidOtlpProfileWithRealContentAndEntityIdentity()
    {
        var validProfile = _receivedProfiles.FirstOrDefault(p => p.StructurallyValid);
        var contentProfile = _receivedProfiles.FirstOrDefault(p => p.ContentValid);
        var withEntityGuid = _receivedProfiles.FirstOrDefault(p => p.ContentValid && p.HasEntityGuid);

        NrAssert.Multiple(
            () => Assert.NotEmpty(_receivedProfiles),
            () => Assert.NotNull(validProfile),
            () => Assert.True(validProfile != null && validProfile.ResourceProfileCount > 0, "Received profile had no ResourceProfiles."),
            () => Assert.True(validProfile != null && validProfile.ScopeProfileCount > 0, "Received profile had no ScopeProfiles."),
            () => Assert.True(validProfile != null && validProfile.ProfileCount > 0, "Received profile had no Profiles."),
            () => Assert.True(validProfile != null && validProfile.SampleCount > 0, "Received profile had no Samples."),
            () => Assert.True(validProfile != null && validProfile.StringTableSize > 1, "Received profile had a trivial (reserved-only) string table."),

            // M4: assert on DECODED CONTENT, not just counts. A sample that is captured, encoded, shipped, and
            // empty or wrong (empty stacks, placeholder-only frame names, or attached to the wrong/absent
            // entity) passes every count-only assertion green -- this pins the actual bytes.
            () => Assert.NotNull(contentProfile),
            // Non-empty frames: guards N samples with empty stacks passing SampleCount > 0.
            () => Assert.True(contentProfile != null && contentProfile.MaxFramesInAnySample > 0,
                "No sample carried any frames (all stacks empty)."),
            () => Assert.True(contentProfile != null && contentProfile.SamplesWithFramesCount > 0,
                "No sample had a non-empty stack."),
            // Real names, not placeholders: guards a string table of only "UnknownClass.UnknownMethod(...)".
            () => Assert.True(contentProfile != null && contentProfile.HasRealFrameName,
                $"No sample carried a real (non-placeholder) resolved frame name. Example seen: '{contentProfile?.ExampleRealFrameName}'."),
            // Resource identity: guards wrong-entity attachment or an ingest-side resource drop.
            () => Assert.True(contentProfile != null && contentProfile.HasServiceName,
                "Received profile had no service.name resource attribute."),
            () => Assert.True(contentProfile != null && !string.IsNullOrEmpty(contentProfile.ServiceName),
                "Received profile's service.name was empty."),
            () => Assert.Contains("host", contentProfile?.ResourceAttributeKeys ?? new List<string>()),

            // entity.guid is only known post-connect; the mock collector returns one on connect, so at least
            // one profile built after connect must carry it.
            () => Assert.NotNull(withEntityGuid),
            () => Assert.True(withEntityGuid != null && !string.IsNullOrEmpty(withEntityGuid.EntityGuid),
                "No content-valid profile carried a non-empty entity.guid resource attribute.")
        );
    }
}
