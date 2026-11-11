// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Api.Experimental;

public interface IDatabaseService : IDisposable
{
    long GetSqlId(string sql, DatastoreVendor vendor);
    string GetObfuscatedSql(string sql, DatastoreVendor vendor);

    /// <summary>
    /// Returns sql with every comment replaced by a same-length run of spaces, using the given vendor's dialect
    /// rules, leaving everything else (including string-literal content that merely looks like a comment)
    /// untouched. For callers that need to locate real code -- e.g. matching a bound parameter's own placeholder --
    /// without being misled by comment text that happens to contain lookalike tokens.
    ///
    /// Returns null when the input cannot be classified with confidence (an unterminated or ambiguous comment,
    /// or an unterminated string or quoted identifier). Callers should treat null as "do not proceed" rather
    /// than falling back to the original sql.
    /// </summary>
    string StripSqlComments(string sql, DatastoreVendor vendor);
}