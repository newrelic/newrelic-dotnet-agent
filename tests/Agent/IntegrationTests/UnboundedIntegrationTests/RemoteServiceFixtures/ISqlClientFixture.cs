/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public interface IMsSqlClientFixture : IDisposable
    {
        IntegrationTestConfiguration TestConfiguration { get; }
        ITestOutputHelper TestLogger { get; set; }
        string DestinationNewRelicConfigFilePath { get; }
        string DestinationNewRelicExtensionsDirectoryPath { get; }
        AgentLogFile AgentLog { get; }
        string TableName { get; }
        string ProcedureName { get; }
        void Actions(Action setupConfiguration, Action exerciseApplication);
        void Initialize();
        void GetMsSql();
        void GetMsSqlAsync();
        void GetMsSqlAsync_WithParameterizedQuery(bool paramsWithAtSign);
        void GetMsSqlParameterizedStoredProcedure(bool paramsWithAtSign);
        void GetMsSql_WithParameterizedQuery(bool paramsWithAtSign);
    }
}
