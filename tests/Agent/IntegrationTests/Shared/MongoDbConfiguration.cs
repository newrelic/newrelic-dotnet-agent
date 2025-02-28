// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class MongoDbConfiguration
    {
        private static string _mongoDb3_2ConnectionString;
        private static string _MongoDbLatestConnectionString;

        // example: "mongodb://1.2.3.4:4444"
        public static string MongoDb3_2ConnectionString
        {
            get
            {
                if (_mongoDb3_2ConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MongoDB32Tests");
                        _mongoDb3_2ConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MongoDb3_2ConnectionString configuration is invalid.", ex);
                    }
                }

                return _mongoDb3_2ConnectionString;
            }
        }

        public static string MongoDbLatestConnectionString
        {
            get
            {
                if (_MongoDbLatestConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MongoDBLatestTests");
                        _MongoDbLatestConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MongoDbLatestConnectionString configuration is invalid.", ex);
                    }
                }

                return _MongoDbLatestConnectionString;
            }
        }

    }
}
