// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.WCF.Client.Self
{
    public abstract class WCFClient_Self : WCFClientTestBase
    {
        public WCFClient_Self(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingType, WCFLegacyTracingTestOption tracingTestOption)
            : base(fixture, output, bindingType, tracingTestOption, HostingModel.Self, ASPCompatibilityMode.Disabled, new WCFLogHelpers_SelfHosted(fixture))
        {
        }
    }

    public class WCFClient_Self_NetTCP : WCFClient_Self
    {
        public WCFClient_Self_NetTCP(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.NetTcp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_Self_WebHTTP : WCFClient_Self
    {
        public WCFClient_Self_WebHTTP(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_Self_WSHTTP : WCFClient_Self
    {
        public WCFClient_Self_WSHTTP(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_Self_BasicHTTP : WCFClient_Self
    {
        public WCFClient_Self_BasicHTTP(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_Self_WebHTTP_DT : WCFClient_Self
    {
        public WCFClient_Self_WebHTTP_DT(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_Self_WSHTTP_DT : WCFClient_Self
    {
        public WCFClient_Self_WSHTTP_DT(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_Self_BasicHTTP_DT : WCFClient_Self
    {
        public WCFClient_Self_BasicHTTP_DT(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_Self_NetTCP_DT : WCFClient_Self
    {
        public WCFClient_Self_NetTCP_DT(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.NetTcp, WCFLegacyTracingTestOption.DT)
        {
        }
    }


    public class WCFClient_Self_WebHTTP_CAT : WCFClient_Self
    {
        public WCFClient_Self_WebHTTP_CAT(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFClient_Self_WSHTTP_CAT : WCFClient_Self
    {
        public WCFClient_Self_WSHTTP_CAT(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFClient_Self_BasicHTTP_CAT : WCFClient_Self
    {
        public WCFClient_Self_BasicHTTP_CAT(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFClient_Self_NetTCP_CAT : WCFClient_Self
    {
        public WCFClient_Self_NetTCP_CAT(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.NetTcp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFClient_Self_Custom : WCFClient_Self
    {
        public WCFClient_Self_Custom(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.Custom, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_Self_CustomClass : WCFClient_Self
    {
        public WCFClient_Self_CustomClass(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.CustomClass, WCFLegacyTracingTestOption.None)
        {
        }
    }
}
