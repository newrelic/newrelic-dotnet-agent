/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class RabbitMqConfiguration
    {
        private static string _rabbitMqServerIp;

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
    }
}
