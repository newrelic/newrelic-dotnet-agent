// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

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
    /// when the live database name disagrees with the parsed one the live value wins.
    /// Providers that do not expose a live database name pass null or empty and take the
    /// unchanged path.
    /// </summary>
    /// <param name="parsedConnectionInfo">The ConnectionInfo parsed from the connection string. Never mutated.</param>
    /// <param name="liveDatabaseName">The connection's current database, or null/empty if the provider does not report one.</param>
    public static ConnectionInfo ResolveWithLiveDatabase(ConnectionInfo parsedConnectionInfo, string liveDatabaseName)
    {
        // OrdinalIgnoreCase is mandatory: MSSQL reports connection-string casing when a
        // connection is first opened but server casing after a switch, so a case-sensitive
        // compare would read every call as a database switch.
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

        // WithDatabaseName caches per database name, so this allocates nothing once the
        // application has visited each database it uses, switches back included. The result
        // deliberately does not go through the per-transaction cache: that cache is keyed by
        // string, and the only correct key there combines the connection string with the
        // database name, which means building a connection-string-length key on every command.
        // A connection string that omits the database parses to "unknown" and so never matches
        // the live name, making that the steady state for those applications, not a rare case.
        return parsedConnectionInfo.WithDatabaseName(liveDatabaseName);
    }
}
