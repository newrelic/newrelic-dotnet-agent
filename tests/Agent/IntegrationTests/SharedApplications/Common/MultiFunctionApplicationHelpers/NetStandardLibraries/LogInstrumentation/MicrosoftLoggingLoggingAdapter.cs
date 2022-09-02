// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER

using System;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Json;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class MicrosoftLoggingLoggingAdapter : ILoggingAdapter
    {
        private static Microsoft.Extensions.Logging.ILogger logger;

        public MicrosoftLoggingLoggingAdapter()
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

        public void Warn(string message)
        {
            logger.LogWarning(message);
        }

        public void Error(Exception exception)
        {
            logger.LogError(exception, exception.Message);
        }

        public void Fatal(string message)
        {
            logger.LogCritical(message);
        }

        public void NoMessage()
        {
            logger.LogCritical(string.Empty);
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
            // NOTE: This is a serilog logger we are setting up
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Scope} {NewLine}{Exception}"
                )
                .CreateLogger();

            CreateMelLogger(LogLevel.Debug, serilogLogger);
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            // NOTE: This is a serilog logger we are setting up
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console(new JsonFormatter())
                .CreateLogger();

            CreateMelLogger(LogLevel.Debug, serilogLogger);
        }

        private void CreateMelLogger(LogLevel minimumLogLevel, Serilog.ILogger serilogLoggerImpl = null)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(minimumLogLevel);

                // Either use serilog OR the built in console appender
                if (serilogLoggerImpl != null)
                {
                    builder.AddSerilog(serilogLoggerImpl);
                }
                else
                {
                    builder.AddConsole();
                }
            });
            logger = loggerFactory.CreateLogger<LoggingTester>();
        }
    }
}

#endif
