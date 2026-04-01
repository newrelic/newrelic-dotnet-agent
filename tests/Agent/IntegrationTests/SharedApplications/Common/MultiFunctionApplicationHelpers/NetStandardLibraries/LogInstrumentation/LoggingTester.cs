// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Microsoft.Extensions.Logging is only supported in .NET Core 2.1+ and .NET Framework 4.8+
#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER
#define MEL
#endif

#if NET10_0 || NET481_OR_GREATER
#define MFALATEST
#endif

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using Newtonsoft.Json;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation;

[Library]
public class LoggingTester
{
    private static Dictionary<string, ILoggingAdapter> _logs;

#region configuration
    [LibraryMethod]
    public static void SetFramework(string loggingFramework, string loggingPort)
    {
        _logs ??=  new Dictionary<string, ILoggingAdapter>();
        ILoggingAdapter logger = null;
        switch (loggingFramework.ToUpper())
        {
            // Logging frameworks available in all TFMs we build the ConsoleMF system for
            case "LOG4NET":
                logger = new Log4NetLoggingAdapter();
                break;
            case "NLOG":
                logger = new NLogLoggingAdapter();
                break;
            case "SERILOG":
                logger = new SerilogLoggingAdapter();
                break;
            // Logging frameworks involving Microsoft.Extensions.Logging which is only supported in .NET Core 2.1+ and .NET Framework 4.8+
#if MEL
            case "MICROSOFTLOGGING":
                logger = new MelLoggingAdapter();
                break;
            case "DUMMYMEL":
                logger = new DummyMELAdapter();
                break;
            case "SERILOGEL":
                logger = new SerilogExtensionsLoggingAdapter();
                break;
#if MFALATEST // NLog.Extensions.Logging is only included in the MFALatestPackages project
            case "NLOGEL":
                logger = new NLogExtensionsLoggingAdapter();
                break;
#endif
#endif
            // Logging frameworks only available in certain TFMs due to package dependencies
#if MFALATEST && NET
            case "SERILOGWEB": // Requires Serilog.AspNetCore which is only included in the MFALatestPackages project
                logger = new SerilogLoggingWebAdapter(loggingPort);
                break;
#endif
#if NET48_OR_GREATER
            case "SITECORE":
                    logger = new SitecoreLoggingAdapter();
                break;
#endif
            default:
                throw new System.ArgumentNullException(nameof(loggingFramework));
        }
        _logs[loggingFramework.ToUpper()] = logger;
    }


    [LibraryMethod]
    public static void Configure()
    {
        _logs.Values.ToList().ForEach(l => l.Configure());
    }

    [LibraryMethod]
    public static void ConfigureWithInfoLevelEnabled()
    {
        _logs.Values.ToList().ForEach(l => l.ConfigureWithInfoLevelEnabled());
    }

    [LibraryMethod]
    public static void ConfigurePatternLayoutAppenderForDecoration()
    {
        _logs.Values.ToList().ForEach(l => l.ConfigurePatternLayoutAppenderForDecoration());
    }

    [LibraryMethod]
    public static void ConfigureJsonLayoutAppenderForDecoration()
    {
        _logs.Values.ToList().ForEach(l => l.ConfigureJsonLayoutAppenderForDecoration());
    }

#endregion


    [LibraryMethod]
    public static void CreateSingleLogMessage(string message, string level)
    {
        _logs.Keys.ToList().ForEach(logger => LogMessageAtLevel(logger, message, level));
    }

    [LibraryMethod]
    public static async Task CreateSingleLogMessageAsync(string message, string level)
    {
        await Task.Run(() => CreateSingleLogMessage(message, level));
    }

    [LibraryMethod]
    public static void CreateSingleLogMessageWithObjectParameter(string message)
    {
        var param = new Person() { Id = 12345, Name = "John Smith" };
        _logs.Values.ToList().ForEach(logger => logger.InfoWithObjectParameter(message, param));
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void CreateSingleLogMessageInTransaction(string message, string level)
    {
        CreateSingleLogMessage(message, level);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static async Task CreateSingleLogMessageInTransactionAsync(string message, string level)
    {
        await Task.Run(() => CreateSingleLogMessage(message, level));
    }

    [LibraryMethod]
    [Trace]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void CreateSingleLogMessageWithTraceAttribute(string message, string level)
    {
        CreateSingleLogMessage(message, level);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void CreateTwoLogMessagesInTransactionWithDifferentTraceAttributes(string message, string level)
    {
        CreateSingleLogMessage(message, level);
        CreateSingleLogMessageWithTraceAttribute(message, level);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void CreateSingleLogMessageInTransactionWithObjectParameter(string message)
    {
        CreateSingleLogMessageWithObjectParameter(message);
    }

    // The reason this method takes a logger as a parameter, rather than performing the same action for all configured loggers
    // like the other methods, is that the test code in ContextDataTests that uses it configures different context data for
    // different loggers
    [LibraryMethod]
    public static void CreateSingleLogMessageWithContext(string logger, string message, string context = null)
    {
        var contextDict = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(context))
        {
            var array = context.Split(',');

            foreach (var item in array)
            {
                var pairs = item.Split('=');

                if (!contextDict.ContainsKey(pairs[0]))
                {
                    contextDict.Add(pairs[0], pairs[1]);
                }
            }
        }
        _logs[logger.ToUpper()].InfoWithContextDictionary(message, contextDict);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void CreateSingleLogMessageWithStructuredArgs(string messageTemplate, string argString)
    {
        // Args is a string of comma separated values that will be parsed into an object array to be passed as structured args.
        // This is done to work around the fact that we can only pass strings from the integration test methods

        var args = argString.Split(',').Select(a => (object)a).ToArray();

        _logs.Values.ToList().ForEach(l =>
        {
            l.InfoWithStructuredArgs(messageTemplate, args);
        });
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void CreateSingleLogMessageWithStructuredArgsAndContext(string messageTemplate, string argString, string context = null)
    {
        var args = argString.Split(',').Select(a => (object)a).ToArray();

        var contextDict = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(context))
        {
            var array = context.Split(',');

            foreach (var item in array)
            {
                var pairs = item.Split('=');

                if (!contextDict.ContainsKey(pairs[0]))
                {
                    contextDict.Add(pairs[0], pairs[1]);
                }
            }
        }
        _logs.Values.ToList().ForEach(l =>
        {
            l.InfoWithStructuredArgsAndContextDictionary(messageTemplate, args, contextDict);
        });

    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void LogMessageInNestedScopes()
    {
        _logs.Values.ToList().ForEach(l => l.LogMessageInNestedScopes());
    }

    private static void LogMessageAtLevel(string logger, string message, string level)
    {
        string key = logger.ToUpper();

        switch (level.ToUpper())
        {
            case "DEBUG":
                _logs[key].Debug(message);
                break;
            case "INFO":
                _logs[key].Info(message);
                break;
            case "WARN":
            case "WARNING":
                _logs[key].Warn(message);
                break;
            case "ERROR":
                _logs[key].Error(ExceptionBuilder.BuildException(message));
                break;
            case "FATAL":
                _logs[key].Fatal(message);
                break;
            case "NOMESSAGE":
                _logs[key].ErrorNoMessage(ExceptionBuilder.BuildException(message));
                break;
            default:
                _logs[key].Info(message);
                break;
        }
    }
}

public class Person
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Include)]
    public string Name { get; set; }

    [JsonProperty("id", NullValueHandling = NullValueHandling.Include)]
    public int? Id { get; set; }
}
