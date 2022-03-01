﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

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

        public const string Log4NetProviderName = "Microsoft.Extensions.Logging.Log4NetProvider";

        public const string SerilogProviderName = "Microsoft.Extensions.Logging.SerilogLoggerProvider";
    }
}
