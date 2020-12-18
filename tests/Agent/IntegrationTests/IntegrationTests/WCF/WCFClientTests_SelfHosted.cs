// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.WCF.Client.Self
{
    public abstract class WCFClient_Self : WCFClientTestBase
    {
        public WCFClient_Self(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output, WCFBindingType bindingType, TracingTestOption tracingTestOption)
            : base(fixture, output, bindingType, tracingTestOption, HostingModel.Self, ASPCompatibilityMode.Disabled, new WCFLogHelpers_SelfHosted(fixture))
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_NetTCP : WCFClient_Self
    {
        public WCFClient_Self_NetTCP(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.NetTcp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_WebHTTP : WCFClient_Self
    {
        public WCFClient_Self_WebHTTP(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_WSHTTP : WCFClient_Self
    {
        public WCFClient_Self_WSHTTP(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_BasicHTTP : WCFClient_Self
    {
        public WCFClient_Self_BasicHTTP(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_WebHTTP_DT : WCFClient_Self
    {
        public WCFClient_Self_WebHTTP_DT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_WSHTTP_DT : WCFClient_Self
    {
        public WCFClient_Self_WSHTTP_DT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_BasicHTTP_DT : WCFClient_Self
    {
        public WCFClient_Self_BasicHTTP_DT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_NetTCP_DT : WCFClient_Self
    {
        public WCFClient_Self_NetTCP_DT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.NetTcp, TracingTestOption.DT)
        {
        }
    }


    [NetFrameworkTest]
    public class WCFClient_Self_WebHTTP_CAT : WCFClient_Self
    {
        public WCFClient_Self_WebHTTP_CAT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_WSHTTP_CAT : WCFClient_Self
    {
        public WCFClient_Self_WSHTTP_CAT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_BasicHTTP_CAT : WCFClient_Self
    {
        public WCFClient_Self_BasicHTTP_CAT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_NetTCP_CAT : WCFClient_Self
    {
        public WCFClient_Self_NetTCP_CAT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.NetTcp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_Custom : WCFClient_Self
    {
        public WCFClient_Self_Custom(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.Custom, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFClient_Self_CustomClass : WCFClient_Self
    {
        public WCFClient_Self_CustomClass(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.CustomClass, TracingTestOption.None)
        {
        }
    }
}
