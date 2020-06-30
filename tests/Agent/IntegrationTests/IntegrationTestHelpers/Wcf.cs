/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.ServiceModel;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Wcf
    {
        public static T GetClient<T>(string destinationServerName, int destinationServerPort, string path = null)
        {
            if (destinationServerName == null)
                throw new ArgumentNullException(nameof(destinationServerName));

            var myBinding = new BasicHttpBinding();
            var myEndpoint = new EndpointAddress($@"http://{destinationServerName}:{destinationServerPort}/{path}");
            var myChannelFactory = new ChannelFactory<T>(myBinding, myEndpoint);
            var client = myChannelFactory.CreateChannel();

            Assert.NotEqual(default(T), client);

            return client;
        }
    }
}
