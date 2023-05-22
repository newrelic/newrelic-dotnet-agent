// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Logging
{
    public static class AuditLog
    {
        /// <summary>
        /// Logs <paramref name="message"/> at the AUDIT level, a custom log level that is not well-defined in popular logging providers like log4net. This log level should be used only as dictated by the security team to satisfy auditing requirements.
        /// </summary>
        public static void Log(string message)
        {
            // use Fatal log level to ensure audit log messages never get filtered due to level restrictions
            Serilog.Log.Logger.Fatal("{Message} {Audit}", message, LoggerBootstrapper.GetAuditLevel());
        }
    }
}
