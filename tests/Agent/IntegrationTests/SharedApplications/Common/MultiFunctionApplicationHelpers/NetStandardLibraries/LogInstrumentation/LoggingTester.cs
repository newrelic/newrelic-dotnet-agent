// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    [Library]
    public class LoggingTester
    {
        private static Dictionary<string, ILoggingAdapter> _logs;

        [LibraryMethod]
        public static void SetFramework(string loggingFramework, string loggingPort)
        {
            _logs ??=  new Dictionary<string, ILoggingAdapter>();
            ILoggingAdapter logger = null;
            switch (loggingFramework.ToUpper())
            {
                case "LOG4NET":
                    logger = new Log4NetLoggingAdapter();
                    break;
                case "SERILOG":
                    logger = new SerilogLoggingAdapter();
                    break;
                case "SERILOGWEB": // .NET 8.0+ ONLY
#if NET10_0_OR_GREATER    
                    logger = new SerilogLoggingWebAdapter(loggingPort);
#endif
                    break;
                case "MICROSOFTLOGGING":
#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER
                    logger = new MicrosoftLoggingLoggingAdapter();
#endif
                    break;
                case "DUMMYMEL":
#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER
                    logger = new DummyMELAdapter();
#endif
                    break;
                case "NLOG":
                    logger = new NLogLoggingAdapter();
                    break;
                case "SITECORE":
#if NET48_OR_GREATER
                    logger = new SitecoreLoggingAdapter();
#endif
                    break;
                case "SERILOGEL":
#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER
                    logger = new SerilogExtensionsLoggingAdapter();
#endif
                    break;
                case "NLOGEL":
#if NET10_0_OR_GREATER || NET481_OR_GREATER
                    logger = new NLogExtensionsLoggingAdapter();
#endif
                    break;

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

        [LibraryMethod]
        public static void CreateSingleLogMessage(string message, string level)
        {
            CreateSingleLogMessage(message, level, null);
        }

        [LibraryMethod]
        public static void CreateSingleLogMessage(string message, string level, string context = null)
        {
            _logs.Keys.ToList().ForEach(k => CreateSingleLogMessage(k, message, level, context));
        }
        [LibraryMethod]
        public static void CreateSingleLogMessage(string logger, string message, string level, string context = null)
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

            string key = logger.ToUpper();

            switch (level.ToUpper())
            {
                case "DEBUG":
                    _logs[key].Debug(message);
                    break;
                case "INFO":
                    _logs[key].Info(message, contextDict);
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

        [LibraryMethod]
        public static void CreateSingleLogMessageWithParam(string message)
        {
            var param = new Person() { Id = 12345, Name = "John Smith" };
            _logs.Values.ToList().ForEach(l => l.InfoWithParam(message, param));
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
            _logs.Values.ToList().ForEach(l => l.LogMessageInNestedScopes());
        }

    }
}
