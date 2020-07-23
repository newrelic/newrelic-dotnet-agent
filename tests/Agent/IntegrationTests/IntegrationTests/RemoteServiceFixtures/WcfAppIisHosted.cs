using System;
using System.ServiceModel;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class WcfAppIisHosted : RemoteApplicationFixture
    {
        private const String ApplicationDirectoryName = "WcfAppIisHosted";

        [NotNull]
        public readonly String ExpectedTransactionName = @"WebTransaction/DotNetService/MyService/GetData";

        public WcfAppIisHosted() : base(new RemoteWebApplication(ApplicationDirectoryName, ApplicationType.Bounded))
        {
        }

        public void GetString()
        {
            var wcfService = Wcf.GetClient<Applications.WcfAppIisHosted.IMyService>(DestinationServerName, Port, "MyService.svc");
            var actualResult = wcfService.GetData(42);
            Assert.Equal("You entered: 42", actualResult);
        }

        public void ThrowException()
        {
            var wcfService = Wcf.GetClient<Applications.WcfAppIisHosted.IMyService>(DestinationServerName, Port, "MyService.svc");
            Assert.Throws<FaultException<ExceptionDetail>>(() => wcfService.ThrowException());
        }
    }
}
