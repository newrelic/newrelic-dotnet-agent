// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class OpenAIConfiguration
    {
        public static string ApiKey
        {
            get
            {
                var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("OpenAITests");
                return testConfiguration["ApiKey"];
            }
        }
    }
}
