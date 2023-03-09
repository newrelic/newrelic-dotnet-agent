// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Config
{
    public interface ILogConfig
    {
        string LogLevel { get; }

        string GetFullLogFileName();

        // TODO: Not needed with Serilog
        bool FileLockingModelSpecified { get; }
        // TODO: Not needed with Serilog
        configurationLogFileLockingModel FileLockingModel { get; }

        bool Console { get; }

        bool IsAuditLogEnabled { get; }
    }
}
