// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class SerilogLoggingAdapter : ILoggingAdapter
    {
        private static ILogger _log;

        public SerilogLoggingAdapter()
        {
        }

        public void Debug(string message)
        {
            _log.Debug(message);
        }

        public void Info(string message)
        {
            _log.Information(message);
        }

        public void Info(string message, Dictionary<string, object> context)
        {
            var loggerConfig = new LoggerConfiguration();

            loggerConfig
                .MinimumLevel.Information()
                .Enrich.With(new ContextDataEnricher(context))
                .WriteTo.Console();

            var logger = loggerConfig.CreateLogger();

            logger.Information(message);
        }

        public void InfoWithParam(string message, object param)
        {
            _log.Information(message, param);
        }

        public void Warn(string message)
        {
            _log.Warning(message);
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
            _log.Verbose(string.Empty);
        }

        public void Configure()
        {
            var loggerConfig = new LoggerConfiguration();

            loggerConfig
                .MinimumLevel.Debug()
                .WriteTo.Console();

            _log = loggerConfig.CreateLogger();
        }

        public void ConfigureWithInfoLevelEnabled()
        {
            var loggerConfig = new LoggerConfiguration();

            loggerConfig
                .MinimumLevel.Information()
                .WriteTo.Console();

            _log = loggerConfig.CreateLogger();
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            var loggerConfig = new LoggerConfiguration();

            loggerConfig
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {NR_LINKING} {NewLine}{Exception}"
                );

            _log = loggerConfig.CreateLogger();
        }

        // net462 is using Serilog 1.5.14 - a very old, but supported version of Serilog. This does not have a Console sink that supports JSON.
        public void ConfigureJsonLayoutAppenderForDecoration()
        {
#if !NET462
            var loggerConfig = new LoggerConfiguration();

            loggerConfig
                .MinimumLevel.Debug()
                .WriteTo.Console(new JsonFormatter());

            _log = loggerConfig.CreateLogger();
#endif
        }

        public void LogMessageInNestedScopes()
        {
            throw new NotImplementedException();
        }
    }

    public class ContextDataEnricher : ILogEventEnricher
    {
        private readonly Dictionary<string, object> _contextData;

        public ContextDataEnricher(Dictionary<string, object> contextData)
        {
            _contextData = contextData;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (_contextData != null)
            {
                foreach (var kvp in _contextData)
                {
                    var property = propertyFactory.CreateProperty(kvp.Key, kvp.Value);
                    logEvent.AddPropertyIfAbsent(property);
                }
            }
        }
    }
}
