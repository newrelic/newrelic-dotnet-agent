// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
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

        public void Info(string message, Dictionary<string, object> context)
        {
            LogEventInfo logEvent = new LogEventInfo(LogLevel.Info, null, message);
            foreach (var kvp in context)
            {
                logEvent.Properties[kvp.Key] = kvp.Value;
            }
            _log.Log(logEvent);
        }

        public void InfoWithParam(string message, object param)
        {
            _log.Info(message, param);
        }

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Error(Exception exception)
        {
            _log.Error(exception, exception.Message);
        }

        public void ErrorNoMessage(Exception exception)
        {
            _log.Error(exception, string.Empty);
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
            _log = GetLogger(LogLevel.Debug);
        }

        public void ConfigureWithInfoLevelEnabled()
        {
            _log = GetLogger(LogLevel.Info);
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            _log = GetLogger(LogLevel.Debug);
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

            _log = _log = GetLogger(LogLevel.Debug, jsonLayout);
        }

        public void LogMessageInNestedScopes()
        {
            throw new NotImplementedException();
        }

        private Logger GetLogger(LogLevel minimumLogLevel, Layout layoutOverride = null)
        {
            var logFactory = new NLog.LogFactory();
            var logConfig = new NLog.Config.LoggingConfiguration();
            var logConsole = new ConsoleTarget();

            if (layoutOverride != null)
            {
                logConsole.Layout = layoutOverride;
            }

            logConfig.AddTarget("console", logConsole);
            logConfig.LoggingRules.Add(new NLog.Config.LoggingRule("*", minimumLogLevel, logConsole));
            logFactory.Configuration = logConfig;
            return logFactory.GetLogger("NLogLoggingTest");
        }

    }
}
