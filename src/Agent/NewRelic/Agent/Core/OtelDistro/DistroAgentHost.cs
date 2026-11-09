// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Public front door for "distro mode": starts NR Core once per process without the NR profiler.
/// Independent of AgentManager / AgentInitializer, which stay untouched for agent mode.
/// </summary>
public static class DistroAgentHost
{
    private static readonly object _lock = new object();
    private static IDistroAgent _instance;

    /// <summary>
    /// Starts Core in distro mode. Idempotent: later calls return the first successfully started instance.
    /// A start-up failure propagates to the caller and leaves no instance cached, so a later call invokes the factory again.
    /// That retry is only meaningful for a failure before Core initialization (for example in the factory itself).
    /// Once a real start has reached the agent's shutdown (a fatal configuration problem, or a KillAgentEvent during
    /// start-up), process-wide statics stay set and are not reset by this host: Agent.IsAgentShuttingDown remains
    /// true, Serilog is closed, InternalApi points at the disposed agent API, and ExtensionsLoader stays initialized.
    /// A retry after that does not produce a working agent.
    /// </summary>
    public static IDistroAgent Start()
    {
        return Start(() => new DistroAgentManager(new DistroAgentManagerEnvironment()));
    }

    /// <summary>
    /// Test seam for <see cref="Start()"/>: same idempotence, with the agent built by <paramref name="factory"/>.
    /// </summary>
    public static IDistroAgent Start(Func<IDistroAgent> factory)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        lock (_lock)
        {
            if (_instance != null)
            {
                return _instance;
            }

            var agent = factory();
            if (agent == null)
            {
                throw new InvalidOperationException("The distro agent factory returned null.");
            }

            _instance = agent;
            return _instance;
        }
    }

    /// <summary>
    /// The running distro agent, or null when <see cref="Start()"/> has not (successfully) run. Never starts Core.
    /// </summary>
    public static IDistroAgent Current
    {
        get
        {
            lock (_lock)
            {
                return _instance;
            }
        }
    }

    /// <summary>
    /// Clears the cached instance without shutting it down. Unit tests only.
    /// </summary>
    public static void ResetForTesting()
    {
        lock (_lock)
        {
            _instance = null;
        }
    }
}
