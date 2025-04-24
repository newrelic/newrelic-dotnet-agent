// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class AzureServiceBusConfiguration
    {
        private static string _connectionString;

    public static string ConnectionString
    {
        get
        {
                if (_connectionString == null)
                {
                    try
                    {
            var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("AzureServiceBusTests");
                        _connectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Azure Service Bus configuration is invalid.", ex);
                    }
                }

                return _connectionString;
            }
        }
    }
}
