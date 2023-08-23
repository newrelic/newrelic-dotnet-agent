// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Logging
{
    public static class LogProviders
    {
        // This will only be set once, when a Microsoft.Extensions.Logging registration method is called.
        // It can safely be read concurrently.
        public static bool KnownMELProviderEnabled = false;

        public static readonly List<string> KnownMELProviders = new List<string>
        {
            "Microsoft.Extensions.Logging.Log4NetProvider",
            "log4net.Extensions.Logging.Log4NetProvider",
            "Microsoft.Extensions.Logging.SerilogLoggerProvider",
            "Serilog.Extensions.Logging.SerilogLoggerProvider",
            "Microsoft.Extensions.Logging.NLogLoggerProvider",
            "NLog.Extensions.Logging.NLogLoggerProvider"
        };
    }
}
