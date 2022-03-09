// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETCOREAPP2_1_OR_GREATER

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
            logger.LogTrace(message);
        }

        public void Configure()
        {
            var serilogLogger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .WriteTo.Console()
              .CreateLogger();

            CreateLogger(serilogLogger); ;
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {NR_LINKING} {NewLine}{Exception}"
                )
                .CreateLogger();

            CreateLogger(serilogLogger);
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            var serilogLogger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .WriteTo.Console(new JsonFormatter())
              .CreateLogger();

            CreateLogger(serilogLogger);
        }

        private void CreateLogger(Serilog.Core.Logger serilogLogger)
        {
#if MS_EXTENSIONS_LOGGING_LEGACY
            var loggerFactory = new LoggerFactory()
                .AddConsole(LogLevel.Warning);

#else
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("NonHostConsoleApp.Program", LogLevel.Debug)
                    .AddConsole();
            });
#endif
            loggerFactory.AddSerilog(serilogLogger);
            logger = loggerFactory.CreateLogger<LoggingTester>();
        }

    }
}

#endif
