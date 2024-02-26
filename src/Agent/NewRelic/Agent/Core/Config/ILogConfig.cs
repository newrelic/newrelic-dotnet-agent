// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Config
{
    public interface ILogConfig
    {
        bool Enabled { get; }

        string LogLevel { get; }

        string GetFullLogFileName();

        bool Console { get; }

        bool IsAuditLogEnabled { get; }

        int MaxLogFileSizeMB { get; } // only used if LogRollingStrategy is "size"

        int MaxLogFiles { get; }

        LogRollingStrategy LogRollingStrategy { get; }
    }

    public enum LogRollingStrategy
    {
        Size,
        Day
    }

    public static class LogRollingStrategyExtensions
    {
        public static LogRollingStrategy ToLogRollingStrategy(this configurationLogLogRollingStrategy value)
        {
            switch (value)
            {
                case configurationLogLogRollingStrategy.size:
                    return LogRollingStrategy.Size;
                case configurationLogLogRollingStrategy.day:
                    return LogRollingStrategy.Day;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }
    }
}
