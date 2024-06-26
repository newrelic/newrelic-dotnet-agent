// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public static class AwsSQSConfiguration
    {
        public static string AwsAccessKeyId
        {
            get
            {
                var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("");
                return testConfiguration.DefaultSetting.AwsAccessKeyId;
            }
        }

        public static string AwsSecretAccessKey
        {
            get
            {
                var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("");
                return testConfiguration.DefaultSetting.AwsSecretAccessKey;
            }
        }

        public static string AwsRegion
        {
            get
            {
                var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("");
                return testConfiguration.DefaultSetting.AwsRegion;
            }
        }

        public static string ServiceUrl
        {
            get
            {
                var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("AwsSQSTests");
                return testConfiguration["ServiceUrl"];
            }
        }
    }
}
