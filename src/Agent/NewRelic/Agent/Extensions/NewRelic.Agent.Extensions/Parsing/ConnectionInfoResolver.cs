// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Parsing;

/// <summary>
/// Reconciles a ConnectionInfo parsed from a connection string against the database a
/// connection is actually pointed at right now.
/// </summary>
public static class ConnectionInfoResolver
{
    /// <summary>
    /// Returns the ConnectionInfo that describes where a command really ran. An application
    /// can change the active database on an already-open connection (ChangeDatabase,
    /// ChangeDatabaseAsync, or a USE statement) without the connection string changing, so
    /// when the live database name disagrees with the parsed one the live value wins and the
    /// result is cached per transaction under a composite key. Providers that do not expose a
    /// live database name pass null or empty and take the unchanged path.
    /// </summary>
    /// <param name="transaction">The current transaction, whose per-transaction cache holds the result.</param>
    /// <param name="parsedConnectionInfo">The ConnectionInfo parsed from the connection string. Never mutated.</param>
    /// <param name="connectionString">The connection string, already cached as its own key.</param>
    /// <param name="liveDatabaseName">The connection's current database, or null/empty if the provider does not report one.</param>
    public static ConnectionInfo ResolveWithLiveDatabase(ITransaction transaction, ConnectionInfo parsedConnectionInfo, string connectionString, string liveDatabaseName)
    {
        // OrdinalIgnoreCase is mandatory: MSSQL reports connection-string casing when a
        // connection is first opened but server casing after a switch, so a case-sensitive
        // compare would fire on every call and pollute the cache.
        //
        // parsedConnectionInfo can be null: the caller's cache (GetOrSetValueFromCache)
        // returns null when its key is null, and the key is the connection's
        // ConnectionString, so any ADO.NET provider that reports a null connection string
        // lands here with a null parsedConnectionInfo. Returning it unchanged reproduces
        // the pre-existing behavior exactly, since the old code passed that null straight
        // into StartDatastoreSegment.
        if (parsedConnectionInfo == null ||
            string.IsNullOrEmpty(liveDatabaseName) ||
            string.Equals(parsedConnectionInfo.DatabaseName, liveDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            return parsedConnectionInfo;
        }

        return (ConnectionInfo)transaction.GetOrSetValueFromCache(
            connectionString + "|" + liveDatabaseName,
            () => parsedConnectionInfo.WithDatabaseName(liveDatabaseName));
    }
}
