// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class MongoDbConfiguration
    {
        private static string _mongoDb3_2ConnectionString;
        private static string _mongoDb3_6ConnectionString;

        // example: "mongodb://1.2.3.4:4444"
        public static string MongoDb3_2ConnectionString
        {
            get
            {
                if (_mongoDb3_2ConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MongoDBTests");
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

        public static string MongoDb3_6ConnectionString
        {
            get
            {
                if (_mongoDb3_6ConnectionString == null)
                {
                    try
                    {
                        // The name "MongoDB26Tests" is cruft leftover from when the associated tests only tested version 2.6 of the client driver
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MongoDB26Tests");
                        _mongoDb3_6ConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MongoDb3_6ConnectionString configuration is invalid.", ex);
                    }
                }

                return _mongoDb3_6ConnectionString;
            }
        }

    }
}
