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

        public void Error(Exception exception)
        {
            _log.Error(exception, exception.Message);
        }

        public void Fatal(string message)
        {
            _log.Fatal(message);
        }

        public void NoMessage()
        {
            _log.Trace(string.Empty);
        }

        public void Configure()
        {
            _log = GetLogger();
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            _log = GetLogger();
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            var jsonLayout = new JsonLayout {
                Attributes = {
                    new JsonAttribute ("time", "${longdate}"),
                    new JsonAttribute ("level", "${level:upperCase=true}"),
                    new JsonAttribute ("message", "${message}"),
                }
            };

            _log = _log = GetLogger(jsonLayout);
        }

        private Logger GetLogger(Layout layoutOverride = null)
        {
            var logFactory = new NLog.LogFactory();
            var logConfig = new NLog.Config.LoggingConfiguration();
            var logConsole = new ConsoleTarget();

            if (layoutOverride != null)
            {
                logConsole.Layout = layoutOverride;
            }

            logConfig.AddTarget("console", logConsole);
            logConfig.LoggingRules.Add(new NLog.Config.LoggingRule("*", LogLevel.Debug, logConsole));
            logFactory.Configuration = logConfig;
            return logFactory.GetLogger("LoggingTest");
        }
    }
}
