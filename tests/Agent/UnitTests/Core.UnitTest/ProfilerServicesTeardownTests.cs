// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest;

/// <summary>
/// Covers <see cref="ProfilerServicesTeardown.Stop"/>: ordering, null-safety, and cross-step exception isolation
/// of the profiler teardown that AgentManager delegates to. Do not reference AgentManager here; on .NET
/// Framework that runs its static initializer, which starts a full agent and leaks it into later tests.
/// </summary>
[TestFixture]
public class ProfilerServicesTeardownTests
{
    [Test]
    public void Stop_runs_thread_profiling_stop_before_continuous_profiling_dispose()
    {
        var order = new List<string>();

        ProfilerServicesTeardown.Stop(
            () => order.Add("stop-thread-profiling"),
            () => order.Add("dispose-continuous-profiling"));

        Assert.That(order, Is.EqualTo(new[] { "stop-thread-profiling", "dispose-continuous-profiling" }));
    }

    [Test]
    public void Stop_tolerates_both_steps_being_null()
    {
        // In production either service reference can be null (serverless mode, or a CP construction that
        // threw), in which case StopServices passes a no-op lambda -- but the helper must also be safe when
        // handed null delegates directly.
        Assert.DoesNotThrow(() => ProfilerServicesTeardown.Stop(null, null));
    }

    [Test]
    public void Stop_runs_the_continuous_profiling_step_when_the_thread_profiling_step_is_null()
    {
        var disposed = false;

        ProfilerServicesTeardown.Stop(null, () => disposed = true);

        Assert.That(disposed, Is.True);
    }

    [Test]
    public void Stop_runs_the_thread_profiling_step_when_the_continuous_profiling_step_is_null()
    {
        var stopped = false;

        ProfilerServicesTeardown.Stop(() => stopped = true, null);

        Assert.That(stopped, Is.True);
    }

    [Test]
    public void Stop_disposes_continuous_profiling_even_when_stopping_thread_profiling_throws()
    {
        // The regression: ThreadProfilingService.Stop() joins a sampling worker and can throw. The original
        // unguarded teardown skipped the continuous-profiling dispose in that case, orphaning the CP native
        // sampler thread for the process lifetime. The dispose must still run, and the original failure must
        // still propagate so the shutdown path's error/health reporting is preserved.
        var disposed = false;

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            ProfilerServicesTeardown.Stop(
                () => throw new InvalidOperationException("stop failed"),
                () => disposed = true));

        Assert.Multiple(() =>
        {
            Assert.That(disposed, Is.True, "continuous profiling must be disposed even when the thread-profiling stop throws");
            Assert.That(thrown.Message, Is.EqualTo("stop failed"), "the thread-profiling stop failure must still propagate for shutdown error/health reporting");
        });
    }

    [Test]
    public void Stop_still_ran_the_thread_profiling_stop_when_disposing_continuous_profiling_throws()
    {
        // The thread-profiling stop runs first, so a throw from the continuous-profiling dispose cannot undo
        // it; the dispose failure propagates on its own.
        var stopped = false;

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            ProfilerServicesTeardown.Stop(
                () => stopped = true,
                () => throw new InvalidOperationException("dispose failed")));

        Assert.Multiple(() =>
        {
            Assert.That(stopped, Is.True, "the thread-profiling stop must have run before the continuous-profiling dispose threw");
            Assert.That(thrown.Message, Is.EqualTo("dispose failed"));
        });
    }
}
