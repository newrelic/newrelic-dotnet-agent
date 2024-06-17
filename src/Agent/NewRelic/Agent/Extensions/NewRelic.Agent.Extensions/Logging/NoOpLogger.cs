// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Logging
{
    public class NoOpLogger : ILogger
    {
        public bool IsDebugEnabled => false;

        public bool IsErrorEnabled => false;

        public bool IsFinestEnabled => false;

        public bool IsInfoEnabled => false;

        public bool IsWarnEnabled => false;

        public void Debug(Exception exception, string message, params object[] args) { }
        public void Debug(string message, params object[] args) { }
        public void Error(Exception exception, string message, params object[] args) { }
        public void Error(string message, params object[] args) { }
        public void Finest(Exception exception, string message, params object[] args) { }
        public void Finest(string message, params object[] args) { }
        public void Info(Exception exception, string message, params object[] args) { }
        public void Info(string message, params object[] args) { }

        public bool IsEnabledFor(Level level)
        {
            return false;
        }

        public void Log(Level level, string message) { }

        public void Log(Level level, Exception ex, string message) { }

        public void Warn(Exception exception, string message, params object[] args) { }
        public void Warn(string message, params object[] args) { }

    }
}
