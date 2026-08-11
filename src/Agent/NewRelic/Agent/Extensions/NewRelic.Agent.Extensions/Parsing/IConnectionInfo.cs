// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Parsing;

public class ConnectionInfo
{
    public ConnectionInfo(string host, int port, string databaseName, string instanceName = null)
    {
        Host = ValueOrUnknown(host);
        if (port >= 0)
        {
            Port = port;
        }
        PathOrId = ValueOrUnknown(string.Empty);
        DatabaseName = ValueOrUnknown(databaseName);
        InstanceName = instanceName;
    }

    public ConnectionInfo(string host, string pathOrId, string databaseName, string instanceName = null)
    {
        Host = ValueOrUnknown(host);
        Port = null;
        PathOrId = ValueOrUnknown(pathOrId);
        DatabaseName = ValueOrUnknown(databaseName);
        InstanceName = instanceName;
    }

    /// <summary>
    /// Copy constructor used by <see cref="WithDatabaseName"/>. Copies Port, PathOrId,
    /// Host, and InstanceName verbatim rather than re-entering a public constructor,
    /// because a ConnectionInfo carries either Port or PathOrId and re-entering would
    /// lose whichever one was set.
    /// </summary>
    private ConnectionInfo(ConnectionInfo other, string databaseName)
    {
        Host = other.Host;
        Port = other.Port;
        PathOrId = other.PathOrId;
        InstanceName = other.InstanceName;
        DatabaseName = ValueOrUnknown(databaseName);
    }

    private static string ValueOrUnknown(string value)
    {
        return string.IsNullOrEmpty(value) ? "unknown" : value;
    }

    /// <summary>
    /// Returns a copy of this ConnectionInfo with a different database name. Used when the
    /// active database was changed on an already-open connection, which does not change the
    /// connection string this ConnectionInfo was parsed from. Does not modify this instance.
    /// </summary>
    public ConnectionInfo WithDatabaseName(string databaseName) => new ConnectionInfo(this, databaseName);

    public string Host { get; private set; }
    public string PortPathOrId { get => (Port != null) ? Port.ToString() : PathOrId; }
    public int? Port { get; private set; } = null;
    public string PathOrId { get; private set; } = string.Empty;
    public string DatabaseName { get; private set; }
    public string InstanceName { get; private set; }
}