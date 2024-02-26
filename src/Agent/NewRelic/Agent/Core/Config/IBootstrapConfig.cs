// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Config
{
    public interface IBootstrapConfig
    {
        string AgentEnabledAt { get; }

        ILogConfig LogConfig { get; }

        bool ServerlessModeEnabled {get; }
    }
}
