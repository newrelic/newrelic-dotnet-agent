/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class NServiceBusBasicMvcApplicationFixture : RemoteApplicationFixture
    {
        public NServiceBusBasicMvcApplicationFixture() : base(new RemoteWebApplication("NServiceBusBasicMvcApplication", ApplicationType.Unbounded))
        {
        }
        #region MessageQueueController Actions

        public void GetMessageQueue_NServiceBus_Send()
        {
            var address = $"http://{DestinationServerName}:{Port}/MessageQueue/NServiceBus_Send";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMessageQueue_NServiceBus_SendValid()
        {
            var address = $"http://{DestinationServerName}:{Port}/MessageQueue/NServiceBus_SendValid";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMessageQueue_NServiceBus_SendInvalid()
        {
            var address = $"http://{DestinationServerName}:{Port}/MessageQueue/NServiceBus_SendInvalid";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        #endregion
    }
}
