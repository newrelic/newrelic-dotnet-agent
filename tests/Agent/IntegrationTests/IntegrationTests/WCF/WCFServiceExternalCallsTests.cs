// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPDisabled
{
    [Trait("Runtime", "Framework")]
    public class WCFService_IIS_ExternalCallsTests_ASPDisabled : WCFServiceExternalCallsTestsBase
    {
        public WCFService_IIS_ExternalCallsTests_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IISNoAsp) { }
    }
}

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPEnabled
{

    [Trait("Runtime", "Framework")]
    public class WCFService_IIS_ExternalCallsTests_ASPEnabled : WCFServiceExternalCallsTestsBase
    {
        public WCFService_IIS_ExternalCallsTests_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IIS) { }
    }
}

namespace NewRelic.Agent.IntegrationTests.WCF.Service.Self
{
    [Trait("Runtime", "Framework")]
    public class WCFService_Self_ExternalCallsTests : WCFServiceExternalCallsTestsBase
    {
        public WCFService_Self_ExternalCallsTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.Self) { }
    }
}
