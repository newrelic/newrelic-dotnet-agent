// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NLog;
using NLog.Layouts;
using NLog.Targets;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class NLogLoggingAdapter : ILoggingAdapter
    {
        private static Logger _log;

        public NLogLoggingAdapter()
        {
        }

        public void Debug(string message)
        {
            _log.Debug(message);
        }

        public void Info(string message)
        {
            _log.Info(message);
        }

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Error(string message)
        {
            _log.Error(message);
        }

        public void Fatal(string message)
        {
            _log.Fatal(message);
        }

        public void Configure()
        {
            var logconsole = new ConsoleTarget();
        
            NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(logconsole, LogLevel.Debug);
            _log = LogManager.GetLogger("LoggingTest");
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            // The default layout is plain text and NLog appends NR-LINKING to the message automatically.
            Configure();
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            var logconsole = new ConsoleTarget();
            logconsole.Layout = new JsonLayout();

            NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(logconsole, LogLevel.Debug);
            _log = LogManager.GetLogger("LoggingTest");
        }
    }
}
