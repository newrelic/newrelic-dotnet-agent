// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Config
{
    public interface ILogConfig
    {
        string LogLevel { get; }

        string GetFullLogFileName();

        bool FileLockingModelSpecified { get; }
        configurationLogFileLockingModel FileLockingModel { get; }

        bool Console { get; }

        bool IsAuditLogEnabled { get; }
    }
}
