// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class MongoDbConfiguration
    {
        private static string _mongoDbConnectionString;
        private static string _mongoDb26ConnectionString;
        private static string _mongoDb26Server;
        private static string _mongoDb26Port;

        // example: "mongodb://1.2.3.4:4444"
        public static string MongoDbConnectionString
        {
            get
            {
                if (_mongoDbConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MongoDBTests");
                        _mongoDbConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MongoDbConnectionString configuration is invalid.", ex);
                    }
                }

                return _mongoDbConnectionString;
            }
        }

        public static string MongoDb26ConnectionString
        {
            get
            {
                if (_mongoDb26ConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MongoDB26Tests");
                        _mongoDb26ConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MongoDb26ConnectionString configuration is invalid.", ex);
                    }
                }

                return _mongoDb26ConnectionString;
            }
        }

        public static string MongoDb26Server
        {
            get
            {
                if (_mongoDb26Server == null)
                {
                    try
                    {
                        var uri = new UriBuilder(MongoDb26ConnectionString);
                        _mongoDb26Server = uri.Host;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MongoDb26Server configuration is invalid.", ex);
                    }
                }

                return _mongoDb26Server;
            }
        }

        public static string MongoDb26Port
        {
            get
            {
                if (_mongoDb26Port == null)
                {
                    try
                    {
                        var uri = new UriBuilder(MongoDb26ConnectionString);
                        _mongoDb26Port = uri.Port.ToString();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MongoDb26Port configuration is invalid.", ex);
                    }
                }

                return _mongoDb26Port;
            }
        }
    }
}
