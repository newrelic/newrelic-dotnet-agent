// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER

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

        public void Error(string message)
        {
            logger.LogError(message);
        }

        public void Fatal(string message)
        {
            // This seems odd...
            logger.LogTrace(message);
        }

        public void Configure()
        {
            CreateLogger(LogLevel.Debug);
        }

        public void ConfigureWithInfoLevelEnabled()
        {
            // TODO: Need to test what happens when Serilog Provider (and possibly others) is used with MEL, as it
            // TODO: subscribes to to ALL events in the pipeline with the intention of performing its own filtering.
            CreateLogger(LogLevel.Information);
        }

        // NOTE: We are using serilog in this case
        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Scope} {NewLine}{Exception}"
                )
                .CreateLogger();

            CreateLogger(LogLevel.Debug, serilogLogger);
        }

        // NOTE: We are using serilog in this case
        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console(new JsonFormatter())
                .CreateLogger();

            CreateLogger(LogLevel.Debug, serilogLogger);
        }

        private void CreateLogger(LogLevel minimumLogLevel, Serilog.ILogger serilogLoggerImpl = null)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddFilter("Default", minimumLogLevel);

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
