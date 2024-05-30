// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    /// <summary>
    /// A "dummy" logging adapter that registers a "dummy" MEL.ILogger implementation that doesn't also implement IExternalScopeProvider.
    /// Used for testing error handling around context data retrieval in MicrosoftLoggingWrapper
    /// </summary>
    public class DummyMELAdapter : ILoggingAdapter
    {
        private static ILogger _logger;

        public DummyMELAdapter()
        {
        }

        public void Debug(string message) => _logger.LogDebug(message);

        public void Info(string message) => _logger.LogInformation(message);

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

        public void Warn(string message) => _logger.LogWarning(message);

        public void Error(Exception exception) => _logger.LogError(exception, exception.Message);

        public void ErrorNoMessage(Exception exception) => _logger.LogError(exception, string.Empty);

        public void Fatal(string message) => _logger?.LogCritical(message);

        public void NoMessage()
        {
            _logger.LogTrace(string.Empty);
        }

        public void Configure() => CreateMelLogger();

        public void ConfigureWithInfoLevelEnabled() => CreateMelLogger();

        public void ConfigurePatternLayoutAppenderForDecoration() => CreateMelLogger();

        public void ConfigureJsonLayoutAppenderForDecoration() => CreateMelLogger();
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

        private void CreateMelLogger()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);

                builder.AddDummyILogger();
            });

            _logger = loggerFactory.CreateLogger<LoggingTester>();
        }
    }

    // a bare-bones implementation of MEL.ILogger
    public class DummyILogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }
    }

    /// <summary>
    /// And an ILoggerProvider for DummyILogger
    /// </summary>
    [ProviderAlias("DummyILogger")]
    public class DummyILoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, DummyILogger> _loggers =
            new(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            _loggers.Clear();
        }

        public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new DummyILogger());
    }

    public static class DummyILoggerExtensions
    {
        /// <summary>
        /// Extension method for registering the DummyILoggerProvider
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static ILoggingBuilder AddDummyILogger(
            this ILoggingBuilder builder)
        {
            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, DummyILoggerProvider>());

            return builder;
        }
    }
}

#endif
