// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Api.Experimental;

public interface IDatabaseService : IDisposable
{
    long GetSqlId(string sql, DatastoreVendor vendor);
    string GetObfuscatedSql(string sql, DatastoreVendor vendor);

    /// <summary>
    /// Replaces every "@paramName" placeholder in sql that matches a key in literalsByParamName with that key's
    /// value, using the given vendor's dialect rules to recognize comments and quoted content so a placeholder
    /// inside either is left untouched rather than substituted. See SqlLiteralScanner.InlineParameters for the full
    /// substitution and failure contract.
    /// </summary>
    string InlineSqlParameters(string sql, DatastoreVendor vendor, IReadOnlyDictionary<string, string> literalsByParamName);
}