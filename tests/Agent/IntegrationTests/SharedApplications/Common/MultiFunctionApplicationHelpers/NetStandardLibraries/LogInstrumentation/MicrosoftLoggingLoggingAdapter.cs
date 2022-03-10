// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETCOREAPP2_1_OR_GREATER // This is only applicable to the .net core agent

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
            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .WriteTo.Console()
              .CreateLogger();

            CreateLogger(); ;
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {NR_LINKING} {NewLine}{Exception}"
                )
                .CreateLogger();

            CreateLogger();
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .WriteTo.Console(new JsonFormatter())
              .CreateLogger();

            CreateLogger();
        }

        private void CreateLogger()
        {
#if NETCOREAPP2_1 || NETCOREAPP2_2 // .NET Core 2.1 & 2.2 don;t support LoggerFactory.Create
            var loggerFactory = new LoggerFactory()
                .AddSerilog()
                .AddConsole((s, ll) => ManualFilter(s, ll));

#else
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("NonHostConsoleApp.Program", LogLevel.Debug)
                    .AddSerilog()
                    .AddConsole();
            });
#endif
            logger = loggerFactory.CreateLogger<LoggingTester>();
        }

        private bool ManualFilter(string category, LogLevel level)
        {
            if (category == "Microsoft" || category == "System")
            {
                if (level >= LogLevel.Warning)
                {
                    return true;
                }

                return false;
            }
            else if (category == "NonHostConsoleApp.Program")
            {
                if (level >= LogLevel.Debug)
                {
                    return true;
                }

                return false;
            }

            return false;
        }

    }
}

#endif
