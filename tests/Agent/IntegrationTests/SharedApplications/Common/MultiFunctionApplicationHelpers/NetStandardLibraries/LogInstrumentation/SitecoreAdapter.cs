// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET48_OR_GREATER

extern alias Sitecore;

using System;
using System.Collections.Generic;
using Sitecore.log4net;
using Sitecore.log4net.Appender;
using Sitecore.log4net.Config;
using Sitecore.log4net.Layout;
using Sitecore.log4net.spi;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class SitecoreLoggingAdapter : ILoggingAdapter
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(LoggingTester));

        public SitecoreLoggingAdapter()
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
            var logEventData = new LoggingEventData()
            {
                Message = message,
                Level = Level.INFO
            };

            var logEvent = new LoggingEvent(logEventData);
            if (context.Count > 0)
            {
                // get keys as a list to allow assigning proprties directly
                var keys = new List<string>(context.Keys);

                // Direct properties method

                logEvent.Properties[keys[0]] = context[keys[0]];
                logEvent.Properties[keys[1]] = context[keys[1]];

                MDC.Set(keys[2], context[keys[2]].ToString());
                MDC.Set(keys[3], context[keys[3]].ToString());
            }
            _log.Logger.Log(logEvent);
        }

        public void InfoWithParam(string message, object param)
        {
            // TODO: Not sure what the equivalent would be (or if there is one)
            //_log.InfoFormat(message, param);
            throw new System.NotImplementedException();
        }

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Error(Exception exception)
        {
            _log.Error(exception.Message, exception);
        }

        public void ErrorNoMessage(Exception exception)
        {
            _log.Error(string.Empty, exception);
        }

        public void Fatal(string message)
        {
            _log.Fatal(message);
        }

        public void Configure()
        {
            BasicConfigurator.Configure(LogManager.GetLoggerRepository());
        }

        public void ConfigureWithInfoLevelEnabled()
        {
            Configure();
            ((Sitecore.log4net.Repository.Hierarchy.Hierarchy)LogManager.GetLoggerRepository()).Root.Level = Level.INFO;
            // TODO: Do we need to signal that the config has changed?
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%timestamp [%thread] %level %logger %ndc - %message %property{NR_LINKING}%newline";
            patternLayout.ActivateOptions();

            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout = patternLayout;
            consoleAppender.ActivateOptions();

            BasicConfigurator.Configure(LogManager.GetLoggerRepository(), consoleAppender);
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            // TODO: Not supported?
            throw new System.NotImplementedException();
        }

        public void LogMessageInNestedScopes()
        {
            throw new NotImplementedException();
        }

        public void NoMessage()
        {
            throw new NotImplementedException();
        }
    }
}

#endif
