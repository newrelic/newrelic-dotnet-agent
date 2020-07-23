using System;
using System.ServiceModel;
using JetBrains.Annotations;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
	public static class Wcf
	{
		[NotNull] public static T GetClient<T>([NotNull] String destinationServerName, [NotNull] String destinationServerPort, [CanBeNull] String path = null)
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
