// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Api.Experimental
{
    public interface IDatabaseService : IDisposable
    {
        long GetSqlId(string sql, DatastoreVendor vendor);
        string GetObfuscatedSql(string sql, DatastoreVendor vendor);
    }
}
