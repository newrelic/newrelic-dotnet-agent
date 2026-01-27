// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Requests;

public class GetCleanedAndFormattedSqlRequest
{
    public readonly string SqlStatement;

    public GetCleanedAndFormattedSqlRequest(string sqlStatement)
    {
        if (sqlStatement == null) throw new ArgumentNullException("sqlStatement");
        SqlStatement = sqlStatement;
    }
}
