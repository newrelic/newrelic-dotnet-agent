// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Logging
{
    public enum LogProvider
    {
        Log4Net,
        Serilog,
        NLog
    }

    public static class LogProviders
    {
        public static readonly bool[] RegisteredLogProvider = new bool[Enum.GetNames(typeof(LogProvider)).Length];

        public static readonly List<string> Log4NetProviderNames = new List<string> { "Microsoft.Extensions.Logging.Log4NetProvider", "log4net.Extensions.Logging.Log4NetProvider" };

        public static readonly List<string> SerilogProviderNames = new List<string> { "Microsoft.Extensions.Logging.SerilogLoggerProvider", "Serilog.Extensions.Logging.SerilogLoggerProvider" };
    }
}
