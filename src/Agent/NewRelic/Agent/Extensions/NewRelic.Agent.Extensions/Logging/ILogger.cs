// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

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
        bool IsDebugEnabled { get; }
        bool IsErrorEnabled { get; }
        bool IsFinestEnabled { get; }
        bool IsInfoEnabled { get; }
        bool IsWarnEnabled { get; }

        void Debug(Exception exception, string message, params object[] args);
        void Debug(string message, params object[] args);
        void Error(Exception exception, string message, params object[] args);
        void Error(string message, params object[] args);
        void Finest(Exception exception, string message, params object[] args);
        void Finest(string message, params object[] args);
        void Info(Exception exception, string message, params object[] args);
        void Info(string message, params object[] args);
        void Warn(Exception exception, string message, params object[] args);
        void Warn(string message, params object[] args);

        bool IsEnabledFor(Level level);
        void Log(Level level, string message);
        void Log(Level level, Exception ex,  string message);
    }
}
