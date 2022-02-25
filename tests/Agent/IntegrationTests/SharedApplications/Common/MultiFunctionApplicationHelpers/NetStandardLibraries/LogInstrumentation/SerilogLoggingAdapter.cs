// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Serilog;
using Serilog.Core;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class SerilogLoggingAdapter : ILoggingAdapter
    {
        private static Logger _log;

        public SerilogLoggingAdapter()
        {
        }

        public void Debug(string message)
        {
            _log.Debug(message);
        }

        public void Info(string message)
        {
            _log.Information(message);
        }

        public void Warn(string message)
        {
            _log.Warning(message);
        }

        public void Error(string message)
        {
            _log.Error(message);
        }

        public void Fatal(string message)
        {
            _log.Fatal(message);
        }

        public void Configure()
        {
            var loggerConfig = new LoggerConfiguration();

            loggerConfig
                .MinimumLevel.Debug()
                .WriteTo.Console();

            _log = loggerConfig.CreateLogger();
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            throw new System.NotImplementedException();
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            throw new System.NotImplementedException();
        }
    }
}
