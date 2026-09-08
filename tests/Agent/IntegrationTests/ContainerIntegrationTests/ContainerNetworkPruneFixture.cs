// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using Xunit;

[assembly: AssemblyFixture(typeof(NewRelic.Agent.ContainerIntegrationTests.ContainerNetworkPruneFixture))]

namespace NewRelic.Agent.ContainerIntegrationTests;

/// <summary>
/// Assembly-level fixture that removes orphaned docker networks left behind by container test fixtures
/// once ALL tests in this assembly have finished running. A global prune run per-fixture (or mid-run) would
/// race and delete another fixture's just-created compose network before it attaches, causing
/// "network &lt;project&gt;_default not found" flakes -- running it once, after everything completes, avoids that.
/// </summary>
public sealed class ContainerNetworkPruneFixture : IDisposable
{
    public void Dispose()
    {
        // CI runs multiple jobs/agents against the same docker daemon, so even a post-run global prune
        // could delete a network another job still needs. Only single-assembly local/dev runs are safe.
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
