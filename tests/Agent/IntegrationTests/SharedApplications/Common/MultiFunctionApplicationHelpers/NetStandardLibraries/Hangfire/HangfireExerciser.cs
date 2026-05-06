// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET10_0_OR_GREATER || NET481_OR_GREATER || NET8_0 || NET462

using System.Threading;
using Hangfire;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Hangfire;

[Library]
public class HangfireExerciser
{
    private BackgroundJobServer _server;

    [LibraryMethod]
    public void StartHost()
    {
        NewRelic.Api.Agent.NewRelic.StartAgent();
        var options = new BackgroundJobServerOptions
        {
            Queues = new[] { "alpha", "default" }
        };

#if NET10_0_OR_GREATER || NET481_OR_GREATER
        var cLevel = CompatibilityLevel.Version_180;
#else
        var cLevel = CompatibilityLevel.Version_170;
#endif

        // Configure Hangfire with memory storage (for testing)
        GlobalConfiguration.Configuration
            .SetDataCompatibilityLevel(cLevel)
            .UseInMemoryStorage()
            .UseColouredConsoleLogProvider();

        _server = new BackgroundJobServer(options);
    }

    [LibraryMethod]
    public void StopHost()
    {
        _server?.Dispose();
    }

    [LibraryMethod]
    public void EnqueueJobs()
    {
        BackgroundJob.Enqueue(() => TestJobs.SimpleJob("test-job"));
        BackgroundJob.Enqueue(() => TestJobs.FailingJob());
        var parentJobId = BackgroundJob.Enqueue(() => TestJobs.SimpleJob("parent-job"));
        BackgroundJob.ContinueJobWith(parentJobId, () => TestJobs.SimpleJob("child-job"));

        BackgroundJob.Enqueue(() => TestJobs.SimpleAsyncJob("test-job"));
        BackgroundJob.Enqueue(() => TestJobs.FailingAsyncJob());
        var parentJobIdAsync = BackgroundJob.Enqueue(() => TestJobs.SimpleAsyncJob("parent-job"));
        BackgroundJob.ContinueJobWith(parentJobIdAsync, () => TestJobs.SimpleAsyncJob("child-job"));

        Thread.Sleep(20 * 1000);
    }
}

#endif
