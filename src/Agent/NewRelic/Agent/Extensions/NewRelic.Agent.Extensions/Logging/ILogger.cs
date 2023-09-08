// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Logging
{
    public enum Level
    {
        Finest,
        Debug,
        Info,
        Warn,
        Error
    }

    public interface ILogger
    {
        bool IsEnabledFor(Level level);
        void Log(Level level, string message);
    }
}
