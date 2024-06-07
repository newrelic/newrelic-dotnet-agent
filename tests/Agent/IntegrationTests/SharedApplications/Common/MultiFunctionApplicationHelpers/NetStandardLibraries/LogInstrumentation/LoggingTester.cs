// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    [Library]
    public class LoggingTester
    {
        private static ILoggingAdapter _log;

        [LibraryMethod]
        public static void SetFramework(string loggingFramework, string loggingPort)
        {
            switch (loggingFramework.ToUpper())
            {
                case "LOG4NET":
                    _log = new Log4NetLoggingAdapter();
                    break;
                case "SERILOG":
                    _log = new SerilogLoggingAdapter();
                    break;
                case "SERILOGWEB": // .NET 8.0+ ONLY
#if NET8_0_OR_GREATER    
                    _log = new SerilogLoggingWebAdapter(loggingPort);
#endif
                    break;
                case "MICROSOFTLOGGING":
#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER
                    _log = new MicrosoftLoggingLoggingAdapter();
#endif
                    break;
                case "DUMMYMEL":
#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER
                    _log = new DummyMELAdapter();
#endif
                    break;
                case "NLOG":
                    _log = new NLogLoggingAdapter();
                    break;
                case "SITECORE":
#if NET48_OR_GREATER
                    _log = new SitecoreLoggingAdapter();
#endif
                    break;
                case "SERILOGEL":
#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER
                    _log = new SerilogExtensionsLoggingAdapter();
#endif
                    break;
                case "NLOGEL":
#if NET8_0_OR_GREATER || NET481_OR_GREATER
                    _log = new NLogExtensionsLoggingAdapter();
#endif
                    break;

                default:
                    throw new System.ArgumentNullException(nameof(loggingFramework));
            }
        }


        [LibraryMethod]
        public static void Configure()
        {
            _log.Configure();
        }

        [LibraryMethod]
        public static void ConfigureWithInfoLevelEnabled()
        {
            _log.ConfigureWithInfoLevelEnabled();
        }

        [LibraryMethod]
        public static void ConfigurePatternLayoutAppenderForDecoration()
        {
            _log.ConfigurePatternLayoutAppenderForDecoration();
        }

        [LibraryMethod]
        public static void ConfigureJsonLayoutAppenderForDecoration()
        {
            _log.ConfigureJsonLayoutAppenderForDecoration();
        }

        [LibraryMethod]
        public static void CreateSingleLogMessage(string message, string level)
        {
            CreateSingleLogMessage(message, level, null);
        }

        [LibraryMethod]
        public static void CreateSingleLogMessage(string message, string level, string context = null)
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

            switch (level.ToUpper())
            {
                case "DEBUG":
                    _log.Debug(message);
                    break;
                case "INFO":
                    _log.Info(message, contextDict);
                    break;
                case "WARN":
                case "WARNING":
                    _log.Warn(message);
                    break;
                case "ERROR":
                    _log.Error(ExceptionBuilder.BuildException(message));
                    break;
                case "FATAL":
                    _log.Fatal(message);
                    break;
                case "NOMESSAGE":
                    _log.ErrorNoMessage(ExceptionBuilder.BuildException(message));
                    break;
                default:
                    _log.Info(message);
                    break;
            }
        }

        [LibraryMethod]
        public static void CreateSingleLogMessageWithParam(string message)
        {
            var param = new Person() { Id = 12345, Name = "John Smith" };
            _log.InfoWithParam(message, param);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void CreateSingleLogMessageInTransaction(string message, string level)
        {
            CreateSingleLogMessage(message, level);
        }

        [LibraryMethod]
        public static async Task CreateSingleLogMessageAsync(string message, string level)
        {
            await Task.Run(() => CreateSingleLogMessage(message, level));
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
        public static void CreateSingleLogMessageInTransactionWithParam(string message)
        {
            CreateSingleLogMessageWithParam(message);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void LogMessageInNestedScopes()
        {
            _log.LogMessageInNestedScopes();
        }

    }
}
