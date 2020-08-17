// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.ApplicationLibraries.Wcf;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.WCF.Service.Self
{
    public abstract class WCFService_Self : WCFServiceTestBase
    {
        public WCFService_Self(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output, WCFBindingType bindingType, TracingTestOption testOption)
            : base(fixture, output, bindingType, testOption, HostingModel.Self, ASPCompatibilityMode.Disabled, new WCFLogHelpers_SelfHosted(fixture))
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_NetTCP : WCFService_Self
    {
        public WCFService_Self_NetTCP(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.NetTcp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_WebHTTP : WCFService_Self
    {
        public WCFService_Self_WebHTTP(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_WSHTTP : WCFService_Self
    {
        public WCFService_Self_WSHTTP(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_BasicHTTP : WCFService_Self
    {
        public WCFService_Self_BasicHTTP(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_WebHTTP_DT : WCFService_Self
    {
        public WCFService_Self_WebHTTP_DT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_WSHTTP_DT : WCFService_Self
    {
        public WCFService_Self_WSHTTP_DT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_BasicHTTP_DT : WCFService_Self
    {
        public WCFService_Self_BasicHTTP_DT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_WebHTTP_CAT : WCFService_Self
    {
        public WCFService_Self_WebHTTP_CAT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_WSHTTP_CAT : WCFService_Self
    {
        public WCFService_Self_WSHTTP_CAT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_BasicHTTP_CAT : WCFService_Self
    {
        public WCFService_Self_BasicHTTP_CAT(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_Custom : WCFService_Self
    {
        public WCFService_Self_Custom(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.Custom, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_Self_CustomClass : WCFService_Self
    {
        public WCFService_Self_CustomClass(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.CustomClass, TracingTestOption.CAT)
        {
        }
    }
}
