// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NewRelic.Agent.Extensions.Parsing;

public class ConnectionInfo
{
    /// <summary>
    /// Ceiling on <see cref="_databaseNameOverrides"/> per instance, so an application that
    /// reaches thousands of databases through one connection string cannot grow this without
    /// limit. Past the ceiling copies are still handed out, just not cached.
    /// </summary>
    private const int MaxCachedDatabaseNames = 100;

    // Copies handed out by WithDatabaseName, keyed by database name. Purely a cache: each
    // entry is fully determined by this instance plus its key, so it stays correct even
    // though ConnectionInfoParser hands one instance to every caller sharing a connection
    // string. Created on first use because most connections never switch database. Read and
    // published through Volatile/Interlocked rather than being marked volatile, since passing
    // a volatile field to Interlocked.CompareExchange warns (CS0420).
    private ConcurrentDictionary<string, ConnectionInfo> _databaseNameOverrides;

    // Tracked separately because ConcurrentDictionary.Count locks every one of the
    // dictionary's internal buckets, which would block concurrent writers.
    private int _databaseNameOverrideCount;

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
    /// connection string this ConnectionInfo was parsed from. Changes no value this instance
    /// reports. Every distinct database name is copied once and then reused, so an application
    /// that moves between databases on one connection stops allocating here once it has
    /// visited each of them - including when it switches back to one it used earlier.
    /// </summary>
    public ConnectionInfo WithDatabaseName(string databaseName)
    {
        // An absent name normalizes to "unknown" and is not worth a cache entry. The resolver
        // never passes one, because it reads an absent live name as "no switch happened".
        if (string.IsNullOrEmpty(databaseName))
        {
            return new ConnectionInfo(this, databaseName);
        }

        var overrides = Volatile.Read(ref _databaseNameOverrides);
        if (overrides == null)
        {
            // OrdinalIgnoreCase to match the comparison callers already make: MSSQL reports
            // connection-string casing when a connection is first opened but server casing
            // after a switch, and those name the same database.
            var created = new ConcurrentDictionary<string, ConnectionInfo>(StringComparer.OrdinalIgnoreCase);
            overrides = Interlocked.CompareExchange(ref _databaseNameOverrides, created, null) ?? created;
        }

        if (overrides.TryGetValue(databaseName, out var cached))
        {
            return cached;
        }

        var copy = new ConnectionInfo(this, databaseName);

        if (Volatile.Read(ref _databaseNameOverrideCount) >= MaxCachedDatabaseNames)
        {
            return copy;
        }

        // GetOrAdd with a value rather than a factory, so no closure is built. It returns the
        // copy another thread stored if one raced ahead, which keeps a single instance per
        // name; the increment only fires on the thread whose copy actually went in.
        var stored = overrides.GetOrAdd(databaseName, copy);
        if (ReferenceEquals(stored, copy))
        {
            Interlocked.Increment(ref _databaseNameOverrideCount);
        }

        return stored;
    }

    public string Host { get; private set; }
    public string PortPathOrId { get => (Port != null) ? Port.ToString() : PathOrId; }
    public int? Port { get; private set; } = null;
    public string PathOrId { get; private set; } = string.Empty;
    public string DatabaseName { get; private set; }
    public string InstanceName { get; private set; }
}