﻿/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AgentApiExecutor : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AgentApiExecutor";
        private const string ExecutableName = @"NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor.exe";
        private const string TargetFramework = "net35";

        public AgentApiExecutor()
            : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }
    }

    public class HSMAgentApiExecutor : AgentApiExecutor
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }
}
