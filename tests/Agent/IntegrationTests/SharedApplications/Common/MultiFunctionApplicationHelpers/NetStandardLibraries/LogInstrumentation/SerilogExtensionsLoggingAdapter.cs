// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class SerilogExtensionsLoggingAdapter : ILoggingAdapter
    {
        private static Microsoft.Extensions.Logging.ILogger _logger;

        public SerilogExtensionsLoggingAdapter()
        {
        }

        public void Debug(string message)
        {
            _logger.LogDebug(message);
        }

        public void Info(string message)
        {
            _logger.LogInformation(message);
        }

        public void Info(string message, Dictionary<string, object> context)
        {
            using (_logger.BeginScope(context))
            {
                _logger.LogInformation(message);
            }
        }

        public void InfoWithParam(string message, object param)
        {
            _logger.LogInformation(message, param);
        }

        public void Warn(string message)
        {
            _logger.LogWarning(message);
        }

        public void Error(Exception exception)
        {
            _logger.LogError(exception, exception.Message);
        }

        public void ErrorNoMessage(Exception exception)
        {
            _logger.LogError(exception, string.Empty);
        }

        public void Fatal(string message)
        {
            _logger.LogCritical(message);
        }

        public void NoMessage()
        {
            _logger.LogTrace(string.Empty);
        }

        public void Configure()
        {
            CreateMelLogger(LogLevel.Information);
        }

        public void ConfigureWithInfoLevelEnabled()
        {
            CreateMelLogger(LogLevel.Information);
        }


        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            CreateMelLogger(LogLevel.Debug, "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Scope} {NewLine}{Exception}");
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            CreateMelLogger(LogLevel.Debug, new JsonFormatter());
        }

        public void LogMessageInNestedScopes()
        {
            using (var _ = _logger.BeginScope("{ScopeKey1}", "scopeValue1"))
            {
                _logger.LogInformation("Outer Scope");

                using (var __ = _logger.BeginScope("{ScopeKey1}", "scopeValue2"))
                {
                    _logger.LogInformation("Inner Scope");
                }
            }
        }

        private void CreateMelLogger(LogLevel minimumLogLevel) => CreateMelLogger(minimumLogLevel, null, null);

        private void CreateMelLogger(LogLevel minimumLogLevel, string template) => CreateMelLogger(minimumLogLevel, template, null);

        private void CreateMelLogger(LogLevel minimumLogLevel, ITextFormatter format) => CreateMelLogger(minimumLogLevel, null, format);

        private void CreateMelLogger(LogLevel minimumLogLevel, string template, ITextFormatter format)
        {
            var config = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext();

            if (template != null)
            {
                config = config.WriteTo.Console(outputTemplate: template);
            }
            if (format != null)
            {
                config = config.WriteTo.Console(format);
            }
            var serilog = config.CreateLogger();

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(minimumLogLevel);
                builder.AddSerilog(serilog);
            });
            _logger = loggerFactory.CreateLogger<LoggingTester>();
        }


    }
}

#endif
