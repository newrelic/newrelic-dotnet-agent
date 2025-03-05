// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.Shared;

public static class AzureServiceBusConfiguration
{
    public static string ConnectionString
    {
        get
        {
            var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("AzureServiceBusTests");
            return testConfiguration["ConnectionString"];
        }
    }
}
