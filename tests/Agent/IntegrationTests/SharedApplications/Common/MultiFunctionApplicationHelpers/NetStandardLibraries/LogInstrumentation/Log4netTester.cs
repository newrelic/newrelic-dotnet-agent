// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using log4net;
using log4net.Config;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    [Library]
    public static class Log4netTester
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Log4netTester));

        [LibraryMethod]
        public static void Configure()
        {
            BasicConfigurator.Configure();
        }

        [LibraryMethod]
        public static void CreateSingleLogMessage(string message, string level)
        {
            switch (level)
            {
                case "info":
                    log.Info(message);
                    break;
                case "debug":
                    log.Debug(message);
                    break;
                case "error":
                    log.Error(message);
                    break;
                case "fatal":
                    log.Fatal(message);
                    break;
                default:
                    log.Info(message);
                    break;
            }
        }
    }
}
