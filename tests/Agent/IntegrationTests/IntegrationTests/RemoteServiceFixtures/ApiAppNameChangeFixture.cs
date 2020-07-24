using System;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class ApiAppNameChangeFixture : RemoteApplicationFixture
    {
        private const String ApplicationDirectoryName = @"ApiAppNameChange";
        private const String ExecutableName = @"NewRelic.Agent.IntegrationTests.Applications.ApiAppNameChange.exe";
        private const String TargetFramework = "net451";

        public ApiAppNameChangeFixture()
            : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }
    }
}
