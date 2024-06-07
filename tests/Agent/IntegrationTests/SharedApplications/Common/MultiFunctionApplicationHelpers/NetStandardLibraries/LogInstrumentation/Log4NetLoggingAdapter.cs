// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class Log4NetLoggingAdapter : ILoggingAdapter
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(LoggingTester));

        public Log4NetLoggingAdapter()
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
                Level = Level.Info
            };

            var logEvent = new LoggingEvent(logEventData);
            if (context.Count > 0)
            {
                // get keys as a list to allow assigning proprties directly
                var keys = new List<string>(context.Keys);

                // Direct properties method
                
                logEvent.Properties[keys[0]] = context[keys[0]];
                logEvent.Properties[keys[1]] = context[keys[1]];

                // other contexts context properties method
                log4net.GlobalContext.Properties[keys[2]] = context[keys[2]];
                log4net.ThreadContext.Properties[keys[3]] = context[keys[3]];
            }

            _log.Logger.Log(logEvent);
        }

        public void InfoWithParam(string message, object param)
        {
            _log.InfoFormat(message, param);
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

        public void NoMessage()
        {
            _log.Verbose("");
        }

        public void Configure()
        {
            BasicConfigurator.Configure(LogManager.GetRepository());
        }

        public void ConfigureWithInfoLevelEnabled()
        {
            Configure();
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%timestamp [%thread] %level %logger %ndc - %message %property{NR_LINKING}%newline";
            patternLayout.ActivateOptions();

            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout = patternLayout;
            consoleAppender.ActivateOptions();

            BasicConfigurator.Configure(LogManager.GetRepository(), consoleAppender);
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
#if NETCOREAPP2_2_OR_GREATER || NET471_OR_GREATER // Only supported in newer versions of .NET
            SerializedLayout serializedLayout = new SerializedLayout();
#if NETFRAMEWORK
            serializedLayout.AddMember("Message");
#else
            serializedLayout.AddMember("message");
#endif
            serializedLayout.AddMember("NR_LINKING");
            serializedLayout.ActivateOptions();

            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout = serializedLayout;
            consoleAppender.ActivateOptions();

            BasicConfigurator.Configure(LogManager.GetRepository(), consoleAppender);
#else
            throw new System.NotImplementedException();
#endif
        }

        public void LogMessageInNestedScopes()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Adds a "Verbose" log level to log4net to enable testing of "no message" log levels.
    /// </summary>
    public static class ILogExtentions
    {
        public static void Verbose(this ILog log, string message, Exception exception)
        {
            log.Logger.Log(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType,
                log4net.Core.Level.Emergency, message, exception);
        }

        public static void Verbose(this ILog log, string message)
        {
            log.Verbose(message, null);
        }

    }
}
