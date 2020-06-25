/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Extensions.Parsing
{
    public class ConnectionInfo
    {
        public ConnectionInfo(string host, string portPathOrId, string databaseName, string instanceName = null)
        {
            Host = ValueOrUnknown(host);
            PortPathOrId = ValueOrUnknown(portPathOrId);
            DatabaseName = ValueOrUnknown(databaseName);
            InstanceName = instanceName;
        }

        private static string ValueOrUnknown(string value)
        {
            return string.IsNullOrEmpty(value) ? "unknown" : value;
        }

        public string Host { get; private set; }
        public string PortPathOrId { get; private set; }
        public string DatabaseName { get; private set; }
        public string InstanceName { get; private set; }
    }
}
