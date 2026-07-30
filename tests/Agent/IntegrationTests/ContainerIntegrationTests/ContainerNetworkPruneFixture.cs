// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using Xunit;

[assembly: AssemblyFixture(typeof(NewRelic.Agent.ContainerIntegrationTests.ContainerNetworkPruneFixture))]

namespace NewRelic.Agent.ContainerIntegrationTests;

/// <summary>
/// Assembly-level fixture that removes orphaned docker networks left behind by container
/// test fixtures once ALL tests in this assembly have finished running.
///
/// This intentionally replaces the old per-fixture, unscoped "docker network prune -f" call
/// that used to run inside ContainerApplication.CleanupContainer() before every fixture start
/// (and on the retry path). With many fixtures starting/retrying in parallel, that global
/// prune would delete another fixture's just-created compose network in the window between
/// network creation and container attach, causing
/// "Error response from daemon: network &lt;project&gt;_default not found" flakes. Running
/// the prune exactly once, after every test has completed, removes that race entirely.
/// </summary>
public sealed class ContainerNetworkPruneFixture : IDisposable
{
    public void Dispose()
    {
        // Never run this in CI. Even scoped to "after all tests", a global prune only makes
        // sense for a single dev-laptop-style run of this assembly. On CI, other jobs/agents
        // sharing the same docker daemon (or a future change reintroducing parallel assemblies)
        // could still be relying on a network we would delete out from under them - the exact
        // class of race this fixture exists to avoid. Local/dev runs are single-assembly and
        // safe to clean up after.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
        {
            return;
        }

        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "network prune -f",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

            proc?.WaitForExit(30000);
        }
        catch
        {
            // Teardown must never fail a test run.
        }
    }
}
