// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0



namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class RabbitMqConfiguration
    {
        private static string _rabbitMqServerIp;
        private static string _rabbitMqUsername;
        private static string _rabbitMqPassword;

        public static string RabbitMqServerIp
        {
            get
            {
                if (_rabbitMqServerIp == null)
                {
                    var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("RabbitMqTests");
                    _rabbitMqServerIp = testConfiguration["Server"];
                }

                return _rabbitMqServerIp;
            }
        }
        public static string RabbitMqUsername
        {
            get
            {
                if (_rabbitMqUsername == null)
                {
                    var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("RabbitMqTests");
                    _rabbitMqUsername = testConfiguration["Username"];
                }

                return _rabbitMqUsername;
            }
        }
        public static string RabbitMqPassword
        {
            get
            {
                if (_rabbitMqPassword == null)
                {
                    var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("RabbitMqTests");
                    _rabbitMqPassword = testConfiguration["Password"];
                }

                return _rabbitMqPassword;
            }
        }
    }
}
