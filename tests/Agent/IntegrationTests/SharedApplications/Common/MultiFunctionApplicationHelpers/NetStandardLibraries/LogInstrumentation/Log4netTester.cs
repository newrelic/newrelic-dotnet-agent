// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Reflection;
using System.Runtime.CompilerServices;
using log4net;
using log4net.Config;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    [Library]
    public static class Log4netTester
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Log4netTester));

        [LibraryMethod]
        public static void Configure()
        {
            BasicConfigurator.Configure(LogManager.GetRepository(Assembly.GetCallingAssembly()));
        }

        [LibraryMethod]
        public static void CreateSingleLogMessage(string message, string level)
        {
            switch (level.ToUpper())
            {
                case "DEBUG":
                    log.Debug(message);
                    break;
                case "INFO":
                    log.Info(message);
                    break;
                case "WARN":
                case "WARNING":
                    log.Warn(message);
                    break;
                case "ERROR":
                    log.Error(message);
                    break;
                case "FATAL":
                    log.Fatal(message);
                    break;
                default:
                    log.Info(message);
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
    }
}
