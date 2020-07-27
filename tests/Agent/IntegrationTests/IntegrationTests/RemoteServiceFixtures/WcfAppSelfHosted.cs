using System;
using System.ServiceModel;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class WcfAppSelfHosted : RemoteApplicationFixture
    {
        private const String ApplicationDirectoryName = "WcfAppSelfHosted";
        private const String ExecutableName = "NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted.exe";
        private const String TargetFramework = "net451";

        public readonly String ExpectedTransactionName = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted.IWcfService.GetString";

        public WcfAppSelfHosted() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }

        public void GetString()
        {
            var wcfAppSelfHosted = Wcf.GetClient<Applications.WcfAppSelfHosted.IWcfService>(DestinationServerName, Port);
            const string expectedResult = Applications.WcfAppSelfHosted.WcfService.WcfServiceGetStringResponse;
            var actualResult = wcfAppSelfHosted.GetString();
            Assert.Equal(expectedResult, actualResult);
        }

        public void ReturnString()
        {
            var wcfAppSelfHosted = Wcf.GetClient<Applications.WcfAppSelfHosted.IWcfService>(DestinationServerName, Port);
            const string expectedResult = "foo";
            var actualResult = wcfAppSelfHosted.ReturnString(expectedResult);
            Assert.Equal(expectedResult, actualResult);
        }

        public void ThrowException()
        {
            var wcfAppSelfHosted = Wcf.GetClient<Applications.WcfAppSelfHosted.IWcfService>(DestinationServerName, Port);
            Assert.Throws<FaultException>(() => wcfAppSelfHosted.ThrowException());
        }
    }

    public class HSMWcfAppSelfHosted : WcfAppSelfHosted
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }
}
