// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Config
{
    public interface ILogConfig
    {
        bool Enabled { get; }

        string LogLevel { get; }

        string GetFullLogFileName();

        bool Console { get; }

        bool IsAuditLogEnabled { get; }
    }
}
