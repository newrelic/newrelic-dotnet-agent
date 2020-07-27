/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

namespace NewRelic.Agent.IntegrationTests.Shared.Couchbase
{
    public class CouchbaseTestObject
    {
        private static string _couchbaseServerUrl;
        private static string _couchbaseTestBucket;

        public static string CouchbaseServerUrl
        {
            get
            {
                if (_couchbaseServerUrl == null)
                {
                    var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("CouchbaseTests");
                    _couchbaseServerUrl = testConfiguration["ServerUrl"];
                }

                return _couchbaseServerUrl;
            }
        }

        public static string CouchbaseTestBucket
        {
            get
            {
                if (_couchbaseTestBucket == null)
                {
                    var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("CouchbaseTests");
                    _couchbaseTestBucket = testConfiguration["TestBucket"];
                }

                return _couchbaseTestBucket;
            }
        }

        public string Name { get; set; }
    }
}
