using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class NetCoreAttributeInstrumentationFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"NetCoreAttributeInstrumentationApplication";
        private const string ExecutableName = @"NetCoreAttributeInstrumentationApplication.exe";
        public NetCoreAttributeInstrumentationFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true))
        {
        }
    }
}
