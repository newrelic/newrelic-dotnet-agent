// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET7_0_OR_GREATER || NET481_OR_GREATER

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Targets;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class NLogExtensionsLoggingAdapter : ILoggingAdapter
    {
        private static Microsoft.Extensions.Logging.ILogger logger;

        public NLogExtensionsLoggingAdapter()
        {
        }

        public void Debug(string message)
        {
            logger.LogDebug(message);
        }

        public void Info(string message)
        {
            logger.LogInformation(message);
        }

        public void Info(string message, Dictionary<string, object> context)
        {
            using (logger.BeginScope(context))
            {
                logger.LogInformation(message, context);
            }
        }

        public void InfoWithParam(string message, object param)
        {
            logger.LogInformation(message, param);
        }

        public void Warn(string message)
        {
            logger.LogWarning(message);
        }

        public void Error(Exception exception)
        {
            logger.LogError(exception, exception.Message);
        }

        public void ErrorNoMessage(Exception exception)
        {
            logger.LogError(exception, string.Empty);
        }

        public void Fatal(string message)
        {
            logger.LogCritical(message);
        }

        public void NoMessage()
        {
            logger.LogTrace(string.Empty);
        }

        public void Configure()
        {
            CreateMelLogger(LogLevel.Debug);
        }

        public void ConfigureWithInfoLevelEnabled()
        {
            CreateMelLogger(LogLevel.Information);
        }


        public void ConfigurePatternLayoutAppenderForDecoration()
        {

        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {

        }

        private void CreateMelLogger(LogLevel minimumLogLevel)
        {

            var logFactory = new NLog.LogFactory();
            var logConfig = new NLog.Config.LoggingConfiguration();
            var logConsole = new ConsoleTarget();
            // Required to use MEL's scope data (context data)
            var options = new NLogProviderOptions
            {
                IncludeScopes = true,
                CaptureMessageProperties = true,
            };

            logConfig.AddTarget("console", logConsole);
            logConfig.LoggingRules.Add(new NLog.Config.LoggingRule("*", NLog.LogLevel.Trace, logConsole));

            var config = new ConfigurationBuilder();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(minimumLogLevel);
                builder.AddNLog(logConfig, options);
            });
            logger = loggerFactory.CreateLogger<LoggingTester>();
        }
    }
}

#endif
