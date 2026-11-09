// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Distro mode's <see cref="IAgentManagerEnvironment"/>. Same pass-throughs as agent mode, except the exit hook:
/// there is no AgentInitializer in distro mode, so the handler goes straight onto the CLR events that
/// AgentInitializer.OnExit is raised from in agent mode (ProcessExit and DomainUnload).
/// </summary>
public sealed class DistroAgentManagerEnvironment : AgentManagerEnvironment
{
    public override void SubscribeToExit(EventHandler handler)
    {
        AppDomain.CurrentDomain.ProcessExit += handler;
        AppDomain.CurrentDomain.DomainUnload += handler;
    }

    public override void UnsubscribeFromExit(EventHandler handler)
    {
        AppDomain.CurrentDomain.ProcessExit -= handler;
        AppDomain.CurrentDomain.DomainUnload -= handler;
    }
}
