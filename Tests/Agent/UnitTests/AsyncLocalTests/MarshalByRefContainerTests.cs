using NewRelic.Providers.Storage.CallContext;
using NUnit.Framework;

namespace NewRelic.Providers.CallStack.AsyncLocalTests
{
	public class MarshalByRefContainerTests
	{
		[Test]
		public void ContainerShouldMarshalToOtherAppDomain()
		{
			//ARRANGE
			var setup = System.AppDomain.CurrentDomain.SetupInformation;
			var remoteAppDomain = System.AppDomain.CreateDomain("RemoteAppDomain", null, setup);
			var type = typeof(MarshalByRefContainer);
			var assemblyLoc = typeof(MarshalByRefContainer).Assembly.Location;

			//ACT
			var remoteContainer = (MarshalByRefContainer)remoteAppDomain.CreateInstanceFromAndUnwrap(assemblyLoc, type.FullName);
			var localContainer = remoteContainer;

			//ASSERT
			Assert.NotNull(localContainer);
			Assert.Null(localContainer.GetValue());
		}
	}
}

