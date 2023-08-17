// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Logging;
using NewRelic.Core;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// Simple wrapper for audit logging, shared by implementations of IHttpClient
    /// </summary>
    public static class DataTransportAuditLogger
    {
        /// <summary>
        ///     This represents the direction or flow of data. Used for audit logs.
        /// </summary>
        public enum AuditLogDirection
        {
            Sent = 1,
            Received = 2
        }

        /// <summary>
        ///     This represents the origin or source of data. Used for audit logs.
        /// </summary>
        public enum AuditLogSource
        {
            Collector = 1,
            Beacon = 2,
            InstrumentedApp = 3
        }

        public const string AuditLogFormat = "Data {0} from the {1} : {2}";
        private const string LicenseKeyParameterName = "license_key";

        public static void Log(AuditLogDirection direction, AuditLogSource source, string uri)
        {
            if (AuditLog.IsAuditLogEnabled)
            {
                var message = string.Format(AuditLogFormat, direction, source,
                    Strings.ObfuscateLicenseKeyInAuditLog(uri, LicenseKeyParameterName));
                AuditLog.Log(message);
            }
        }
    }
}
