// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NewRelic.Agent.Core.Logging
{
    public static class AuditLog
    {
        // a lazy ILogger instance that injects an "Audit" property
        private static Lazy<ILogger> _lazyAuditLogger = new Lazy<ILogger>(() =>
            Serilog.Log.Logger.ForContext(LogLevelExtensions.AuditLevel, LogLevelExtensions.AuditLevel));

        /// <summary>
        /// Logs <paramref name="message"/> at the AUDIT level. This log level should be used only as dictated by the security team to satisfy auditing requirements.
        /// </summary>
        public static void Log(string message)
        {
                // use Fatal log level to ensure audit log messages never get filtered due to level restrictions
                _lazyAuditLogger.Value.Fatal(message);
        }

        internal static LoggerConfiguration IncludeOnlyAuditLog(this LoggerConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Filter.ByIncludingOnly($"{LogLevelExtensions.AuditLevel} is not null");

            //return loggerConfiguration.Filter.ByIncludingOnly(logEvent =>
            //    logEvent.Properties.ContainsKey(LogLevelExtensions.AuditLevel));
        }
        internal static LoggerConfiguration ExcludeAuditLog(this LoggerConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Filter.ByIncludingOnly($"{LogLevelExtensions.AuditLevel} is null");
        }
    }
}
