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
                        //_authKey = testConfiguration["AuthKey"];
                        _authKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
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
                        //_cosmosDBServer = testConfiguration["Server"];
                        _cosmosDBServer = "https://localhost:8081/";
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
