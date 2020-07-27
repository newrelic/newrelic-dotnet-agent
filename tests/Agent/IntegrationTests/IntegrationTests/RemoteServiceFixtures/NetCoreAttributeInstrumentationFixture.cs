using System;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class NetCoreAttributeInstrumentationFixture : RemoteApplicationFixture
    {
        private const String ApplicationDirectoryName = @"NetCoreAttributeInstrumentationApplication";
        private const String ExecutableName = @"NetCoreAttributeInstrumentationApplication.exe";
        public NetCoreAttributeInstrumentationFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true))
        {
        }
    }
}
