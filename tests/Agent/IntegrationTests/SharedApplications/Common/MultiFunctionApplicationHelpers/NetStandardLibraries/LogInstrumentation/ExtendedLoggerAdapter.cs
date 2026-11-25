// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET11_0 || NET481_OR_GREATER

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation;

/// <summary>
/// Logs through Microsoft.Extensions.Telemetry's ExtendedLogger. EnableEnrichment() replaces the default
/// LoggerFactory with ExtendedLoggerFactory, whose loggers implement ILogger directly instead of deriving
/// from Microsoft.Extensions.Logging.Logger.
/// </summary>
class ExtendedLoggerAdapter : ILoggingAdapter
{
    private static Microsoft.Extensions.Logging.ILogger logger;

    // Held for the life of the process so the logger and its providers are never disposed while in use
    private static ILoggerFactory loggerFactory;

    public ExtendedLoggerAdapter()
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

    public void InfoWithContextDictionary(string message, Dictionary<string, object> context)
    {
        using (logger.BeginScope(context))
        {
            logger.LogInformation(message);
        }
    }

    public void InfoWithObjectParameter(string message, object param)
    {
        logger.LogInformation(message, param);
    }

    public void InfoWithStructuredArgs(string messageTemplate, object[] args)
    {
        logger.LogInformation(messageTemplate, args);
    }

    public void InfoWithStructuredArgsAndContextDictionary(string messageTemplate, object[] args, Dictionary<string, object> context)
    {
        using (logger.BeginScope(context))
        {
            logger.LogInformation(messageTemplate, args);
        }
    }

    public void LogMessageInNestedScopes()
    {
        using (var _ = logger.BeginScope("{ScopeKey1}", "scopeValue1"))
        {
            logger.LogInformation("Outer Scope");

            using (var __ = logger.BeginScope("{ScopeKey1}", "scopeValue2"))
            {
                logger.LogInformation("Inner Scope");
            }
        }
    }

    public void Configure()
    {
        CreateExtendedLogger(LogLevel.Debug);
    }

    public void ConfigureWithInfoLevelEnabled()
    {
        CreateExtendedLogger(LogLevel.Information);
    }

    public void ConfigurePatternLayoutAppenderForDecoration()
    {
    }

    public void ConfigureJsonLayoutAppenderForDecoration()
    {
    }

    private void CreateExtendedLogger(LogLevel minimumLogLevel)
    {
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLogLevel);
            builder.AddSimpleConsole(options => options.IncludeScopes = true);

            // Swaps in ExtendedLoggerFactory (Microsoft.Extensions.Telemetry)
            builder.EnableEnrichment();
        });

        // ILogger<T> is a thin wrapper that delegates to an inner logger created by the factory (an ExtendedLogger here),
        // which is the shape most applications use
        logger = loggerFactory.CreateLogger<LoggingTester>();

        // LoggerFactory.Create wraps the real factory, so the wrapped type isn't visible; verify via a non-generic logger
        var innerLoggerType = loggerFactory.CreateLogger("ExtendedLoggerTypeCheck").GetType();
        if (innerLoggerType.Name != "ExtendedLogger")
        {
            throw new InvalidOperationException($"Expected the factory to create ExtendedLogger instances but got {innerLoggerType.FullName}");
        }
    }
}
#endif
