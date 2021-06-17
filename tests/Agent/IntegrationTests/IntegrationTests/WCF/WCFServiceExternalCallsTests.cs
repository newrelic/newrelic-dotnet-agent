// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPDisabled
{
    public class WCFService_IIS_ExternalCallsTests_ASPDisabled : WCFServiceExternalCallsTestsBase
    {
        public WCFService_IIS_ExternalCallsTests_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, HostingModel.IIS, ASPCompatibilityMode.Disabled, new WCFLogHelpers_IISHosted(fixture))
        {
        }

        [Fact]
        public void ExternalCallsTests()
        {
            base.ExternalCallsTestsCommon();
        }
    }
}

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPEnabled
{

    public class WCFService_IIS_ExternalCallsTests_ASPEnabled : WCFServiceExternalCallsTestsBase
    {
        public WCFService_IIS_ExternalCallsTests_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, HostingModel.IIS, ASPCompatibilityMode.Enabled, new WCFLogHelpers_IISHosted(fixture))
        {
        }

        [Fact]
        public void ExternalCallsTests()
        {
            base.ExternalCallsTestsCommon();
        }
    }
}

namespace NewRelic.Agent.IntegrationTests.WCF.Service.Self
{
    public class WCFService_Self_ExternalCallsTests : WCFServiceExternalCallsTestsBase
    {
        public WCFService_Self_ExternalCallsTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, HostingModel.Self, ASPCompatibilityMode.Disabled, new WCFLogHelpers_SelfHosted(fixture))
        {
        }

        [Fact]
        public void ExternalCallsTests()
        {
            base.ExternalCallsTestsCommon();
        }
    }
}
