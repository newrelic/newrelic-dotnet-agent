// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.ApplicationLibraries.Wcf;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS
{
    public abstract class WCFService_IIS : WCFServiceTestBase
    {
        public WCFService_IIS(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output, WCFBindingType bindingType, TracingTestOption tracingTestOption, ASPCompatibilityMode aspCompatibilityOption)
            : base(fixture, output, bindingType, tracingTestOption, HostingModel.IIS, aspCompatibilityOption, new WCFLogHelpers_IISHosted(fixture))
        {
        }
    }
}

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPDisabled
{
    public abstract class WCFService_IIS_ASPDisabled : WCFService_IIS
    {
        public WCFService_IIS_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output, WCFBindingType bindingType, TracingTestOption tracingTestOption)
            : base(fixture, output, bindingType, tracingTestOption, ASPCompatibilityMode.Disabled)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WebHTTP_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WebHTTP_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WSHTTP_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WSHTTP_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_BasicHTTP_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_BasicHTTP_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WebHTTP_DT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WebHTTP_DT_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WSHTTP_DT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WSHTTP_DT_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_BasicHTTP_DT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_BasicHTTP_DT_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WebHTTP_CAT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WebHTTP_CAT_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WSHTTP_CAT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WSHTTP_CAT_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_BasicHTTP_CAT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_BasicHTTP_CAT_ASPDisabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.CAT)
        {
        }
    }

}

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPEnabled
{
    public abstract class WCFService_IIS_ASPEnabled : WCFService_IIS
    {
        public WCFService_IIS_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output, WCFBindingType bindingType, TracingTestOption tracingTestOption)
            : base(fixture, output, bindingType, tracingTestOption, ASPCompatibilityMode.Enabled)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WebHTTP_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WebHTTP_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WSHTTP_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WSHTTP_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_BasicHTTP_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_BasicHTTP_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.None)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WebHTTP_DT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WebHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WSHTTP_DT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WSHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_BasicHTTP_DT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_BasicHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.DT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WebHTTP_CAT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WebHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_WSHTTP_CAT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WSHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, TracingTestOption.CAT)
        {
        }
    }

    [NetFrameworkTest]
    public class WCFService_IIS_BasicHTTP_CAT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_BasicHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, TracingTestOption.CAT)
        {
        }
    }

}
