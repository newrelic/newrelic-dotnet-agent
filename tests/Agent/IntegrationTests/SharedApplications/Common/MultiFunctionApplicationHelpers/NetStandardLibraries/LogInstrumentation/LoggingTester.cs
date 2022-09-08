// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


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
        public static void SetFramework(string loggingFramework)
        {
            switch (loggingFramework.ToUpper())
            {
                case "LOG4NET":
                    _log = new Log4NetLoggingAdapter();
                    break;
                case "SERILOG":
                    _log = new SerilogLoggingAdapter();
                    break;
                case "SERILOGWEB": // .NET 6.0 ONLY
#if NET6_0    
                    _log = new SerilogLoggingWebAdapter();
#endif
                    break;
                case "MICROSOFTLOGGING":
#if NETCOREAPP2_1_OR_GREATER || NET48_OR_GREATER
                    _log = new MicrosoftLoggingLoggingAdapter();
#endif
                    break;
                case "NLOG":
                    _log = new NLogLoggingAdapter();
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
            switch (level.ToUpper())
            {
                case "DEBUG":
                    _log.Debug(message);
                    break;
                case "INFO":
                    _log.Info(message);
                    break;
                case "WARN":
                case "WARNING":
                    _log.Warn(message);
                    break;
                case "ERROR":
                    _log.Error(message);
                    break;
                case "FATAL":
                    _log.Fatal(message);
                    break;
                default:
                    _log.Info(message);
                    break;
            }
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        [LibraryMethod]
        public static async Task CreateSingleLogMessageAsyncNoAwait(string message, string level)
        {
            _ = Task.Run(() => CreateSingleLogMessage(message, level));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]

        public static async Task CreateSingleLogMessageInTransactionAsyncNoAwait(string message, string level)
        {
            _ = Task.Run(() => CreateSingleLogMessage(message, level));
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay(string message, string level)
        {
            _ = Task.Run(() => CreateSingleLogMessage(message, level));
            await Task.Delay(1000);
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

    }
}
