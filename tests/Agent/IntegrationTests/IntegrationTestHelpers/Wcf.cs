// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.ServiceModel;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Wcf
    {
        public static T GetClient<T>(string destinationServerName, string destinationServerPort, string path = null)
        {
            if (destinationServerName == null)
                throw new ArgumentNullException("destinationServerName");
            if (destinationServerPort == null)
                throw new ArgumentNullException("destinationServerPort");

            var myBinding = new BasicHttpBinding();
            var myEndpoint = new EndpointAddress(string.Format(@"http://{0}:{1}/{2}", destinationServerName, destinationServerPort, path));
            var myChannelFactory = new ChannelFactory<T>(myBinding, myEndpoint);
            var client = myChannelFactory.CreateChannel();
            Assert.NotNull(client);

            return client;
        }
    }
}
