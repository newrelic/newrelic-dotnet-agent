// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Agent.IntegrationTests.WCF;
using Xunit;
using Xunit.Abstractions;
using static NewRelic.Agent.IntegrationTests.WCF.WCFTestBase;

namespace NewRelic.Agent.IntegrationTests.RequestHeadersCapture.WCF
{
    public abstract class AllowAllHeadersDisabledTests : WCFEmptyTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, HostingModel hostingModel, WCFBindingType bindingType)
            : base(fixture, output, hostingModel, bindingType) { }

        protected override NewRelicConfigModifier SetupConfiguration()
        {
            return base.SetupConfiguration().SetAllowAllHeaders(false);
        }

        protected override void AddFixtureCommands()
        {
            base.AddFixtureCommands();

            _fixture.AddCommand($"WCFClient GetDataWithHeaders");
        }

        [Fact]
        public override void TestHeadersCaptured()
        {
            base.TestHeadersCaptured();
        }
    }

    #region IIS

    [NetFrameworkTest]
    public class IIS_Basic_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public IIS_Basic_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IIS, WCFBindingType.BasicHttp) { }
    }

    [NetFrameworkTest]
    public class IIS_Web_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public IIS_Web_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IIS, WCFBindingType.WebHttp) { }
    }


    [NetFrameworkTest]
    public class IIS_WS_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public IIS_WS_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IIS, WCFBindingType.WSHttp) { }
    }

    #endregion IIS

    #region IISNoAsp

    [NetFrameworkTest]
    public class IISNoAsp_Basic_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public IISNoAsp_Basic_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IISNoAsp, WCFBindingType.BasicHttp) { }
    }

    [NetFrameworkTest]
    public class IISNoAsp_Web_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public IISNoAsp_Web_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IISNoAsp, WCFBindingType.WebHttp) { }
    }


    [NetFrameworkTest]
    public class IISNoAsp_WS_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public IISNoAsp_WS_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IISNoAsp, WCFBindingType.WSHttp) { }
    }

    #endregion IISNoAsp

    #region Self

    [NetFrameworkTest]
    public class Self_Basic_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public Self_Basic_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.Self, WCFBindingType.BasicHttp) { }
    }

    [NetFrameworkTest]
    public class Self_Web_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public Self_Web_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.Self, WCFBindingType.WebHttp) { }
    }

    [NetFrameworkTest]
    public class Self_WS_AllowAllHeadersDisabledTests : AllowAllHeadersDisabledTests
    {
        public Self_WS_AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.Self, WCFBindingType.WSHttp) { }
    }

    #endregion Self
}
