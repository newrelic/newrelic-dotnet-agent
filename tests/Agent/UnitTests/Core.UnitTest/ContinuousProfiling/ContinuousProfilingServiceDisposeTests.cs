// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.Time;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

/// <summary>
/// Covers the teardown contract AgentManager relies on when its startup sequence throws after continuous
/// profiling has already started: the abort path runs Shutdown(false) -> StopServices() -> Dispose() on the
/// half-built manager, and that Dispose must fully stop and join the native worker rather than orphaning a
/// live sampler thread for the process lifetime.
/// </summary>
[TestFixture]
public class ContinuousProfilingServiceDisposeTests
{
    private ISampleSource _source;
    private INativeContinuousProfiler _native;
    private IProfilesTransport _transport;
    private IScheduler _scheduler;
    private IAgentHealthReporter _health;
    private IConfiguration _config;
    private ContinuousProfilingService _service;

    [SetUp]
    public void SetUp()
    {
        _source = Mock.Create<ISampleSource>();
        _native = Mock.Create<INativeContinuousProfiler>();
        _transport = Mock.Create<IProfilesTransport>();
        _scheduler = Mock.Create<IScheduler>();
        _health = Mock.Create<IAgentHealthReporter>();
        _config = Mock.Create<IConfiguration>();

        _service = new ContinuousProfilingService(_source, _native, _transport, _scheduler, _health);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
        // Reset the process-wide seam so one test's enabled context can't leak into another.
        ContinuousProfilingContext.Instance = new ContinuousProfilingContext();
    }

    private void ArrangeEnabled(int intervalMs = 10000)
    {
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(intervalMs);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        _service.OverrideConfigForTesting(_config);
    }

    [Test]
    public void Dispose_of_an_active_session_stops_native_sampling_before_joining_the_worker()
    {
        // The AgentManager abort path (a startup throw after continuous profiling has started) tears this
        // service down via Shutdown(false) -> StopServices() -> Dispose(). A running session must be both
        // stopped (Stop, so the sampler quits suspending threads) AND joined (Shutdown), not merely joined
        // while still sampling -- otherwise the native worker is left half-running until process exit.
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        _service.Dispose();

        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Mock.Assert(() => _native.Shutdown(), Occurs.Once());
    }
}
