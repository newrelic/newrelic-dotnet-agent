// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Parsing
{
    public class ConnectionInfo
    {
        public ConnectionInfo(string vendor, string host, int port, string databaseName, string instanceName = null)
        {
            Vendor = vendor;
            Host = ValueOrUnknown(host);
            if (port >= 0)
            {
                Port = port;
            }
            PathOrId = ValueOrUnknown(string.Empty);
            DatabaseName = ValueOrUnknown(databaseName);
            InstanceName = instanceName;
        }

        public ConnectionInfo(string vendor, string host, string pathOrId, string databaseName, string instanceName = null)
        {
            Vendor = vendor;
            Host = ValueOrUnknown(host);
            Port = null;
            PathOrId = ValueOrUnknown(pathOrId);
            DatabaseName = ValueOrUnknown(databaseName);
            InstanceName = instanceName;
        }

        private static string ValueOrUnknown(string value)
        {
            return string.IsNullOrEmpty(value) ? "unknown" : value;
        }

        public string Vendor { get; private set; }
        public string Host { get; private set; }
        public string PortPathOrId { get => (Port != null) ? Port.ToString() : PathOrId; }
        public int? Port { get; private set; } = null;
        public string PathOrId { get; private set; } = string.Empty;
        public string DatabaseName { get; private set; }
        public string InstanceName { get; private set; }
    }
}
