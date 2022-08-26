// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using log4net;
using log4net.Appender;
using log4net.Config;
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

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Error(Exception exception)
        {
            _log.Error(exception.Message, exception);
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
            BasicConfigurator.Configure(LogManager.GetRepository(Assembly.GetCallingAssembly()));
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%timestamp [%thread] %level %logger %ndc - %message %property{NR_LINKING}%newline";
            patternLayout.ActivateOptions();

            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout = patternLayout;
            consoleAppender.ActivateOptions();

            var callingAssembly = Assembly.GetCallingAssembly();

            BasicConfigurator.Configure(LogManager.GetRepository(Assembly.GetCallingAssembly()), consoleAppender);
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
#if NETCOREAPP2_2_OR_GREATER || NET471_OR_GREATER // Only supported in newer verisons of .NET
            SerializedLayout serializedLayout = new SerializedLayout();
            serializedLayout.AddMember("NR_LINKING");
            serializedLayout.ActivateOptions();

            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout = serializedLayout;
            consoleAppender.ActivateOptions();

            BasicConfigurator.Configure(LogManager.GetRepository(Assembly.GetCallingAssembly()), consoleAppender);
#else
            throw new System.NotImplementedException();
#endif
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
