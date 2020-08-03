// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Parsing.ConnectionString
{
    public interface IConnectionStringParser
    {
        ConnectionInfo GetConnectionInfo();
    }
}
