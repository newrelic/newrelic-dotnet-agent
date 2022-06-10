// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class CosmosDBConfiguration
    {
        private static string _authKey;
        private static string _cosmosDBServer;

        public static string AuthKey
        {
            get
            {
                if (_authKey == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("CosmosDBTests");
                        _authKey = testConfiguration["AuthKey"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("CosmosDB authentication key configuration is invalid.", ex);
                    }
                }

                return _authKey;
            }
        }

        public static string CosmosDBServer
        {
            get
            {
                if (_cosmosDBServer == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("CosmosDBTests");
                        _cosmosDBServer = testConfiguration["Server"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("CosmosDB configuration is invalid.", ex);
                    }
                }

                return _cosmosDBServer;
            }
        }
    }
}
