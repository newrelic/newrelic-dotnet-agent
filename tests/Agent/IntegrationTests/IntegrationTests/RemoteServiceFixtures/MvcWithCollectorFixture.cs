// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class MvcWithCollectorFixture : MockNewRelicFixture
    {
        public MvcWithCollectorFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Bounded))
        {
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default";
            GetStringAndAssertContains(address, "<html>");
        }

        public void GenerateCallsToCustomInstrumentationEditorMethods()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/Get";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void StartAgent()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/StartAgent";
            GetStringAndAssertContains(address, "<html>");
        }
    }
}
