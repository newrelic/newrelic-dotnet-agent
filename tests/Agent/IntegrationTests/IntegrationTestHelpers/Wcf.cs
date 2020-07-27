using System;
using System.ServiceModel;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Wcf
    {
        public static T GetClient<T>(String destinationServerName, String destinationServerPort, String path = null)
        {
            if (destinationServerName == null)
                throw new ArgumentNullException("destinationServerName");
            if (destinationServerPort == null)
                throw new ArgumentNullException("destinationServerPort");

            var myBinding = new BasicHttpBinding();
            var myEndpoint = new EndpointAddress(String.Format(@"http://{0}:{1}/{2}", destinationServerName, destinationServerPort, path));
            var myChannelFactory = new ChannelFactory<T>(myBinding, myEndpoint);
            var client = myChannelFactory.CreateChannel();
            Assert.NotNull(client);

            return client;
        }
    }
}
