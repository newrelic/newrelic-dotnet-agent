// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class AzureOpenAiConfiguration
    {
        public static string Endpoint
        {
            get
            {
                var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("AzureOpenAITests");
                return testConfiguration["Endpoint"];
            }
        }

        public static string ApiKey
        {
            get
            {
                var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("AzureOpenAITests");
                return testConfiguration["ApiKey"];
            }
        }
    }
}
