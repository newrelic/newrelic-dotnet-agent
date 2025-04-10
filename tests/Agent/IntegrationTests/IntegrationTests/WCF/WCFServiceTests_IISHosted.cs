// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS
{
    public abstract class WCFService_IIS : WCFServiceTestBase
    {
        public WCFService_IIS(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingType, WCFLegacyTracingTestOption tracingTestOption, ASPCompatibilityMode aspCompatibilityOption)
            : base(fixture, output, bindingType, tracingTestOption, HostingModel.IIS, aspCompatibilityOption, new WCFLogHelpers_IISHosted(fixture))
        {
        }
    }
}

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPDisabled
{
    public abstract class WCFService_IIS_ASPDisabled : WCFService_IIS
    {
        public WCFService_IIS_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingType, WCFLegacyTracingTestOption tracingTestOption)
            : base(fixture, output, bindingType, tracingTestOption, ASPCompatibilityMode.Disabled)
        {
        }
    }

    public class WCFService_IIS_WebHTTP_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WebHTTP_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFService_IIS_WSHTTP_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WSHTTP_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFService_IIS_BasicHTTP_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_BasicHTTP_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFService_IIS_WebHTTP_DT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WebHTTP_DT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFService_IIS_WSHTTP_DT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WSHTTP_DT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFService_IIS_BasicHTTP_DT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_BasicHTTP_DT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFService_IIS_WebHTTP_CAT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WebHTTP_CAT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFService_IIS_WSHTTP_CAT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_WSHTTP_CAT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFService_IIS_BasicHTTP_CAT_ASPDisabled : WCFService_IIS_ASPDisabled
    {
        public WCFService_IIS_BasicHTTP_CAT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

}

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPEnabled
{
    public abstract class WCFService_IIS_ASPEnabled : WCFService_IIS
    {
        public WCFService_IIS_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingType, WCFLegacyTracingTestOption tracingTestOption)
            : base(fixture, output, bindingType, tracingTestOption, ASPCompatibilityMode.Enabled)
        {
        }
    }

    public class WCFService_IIS_WebHTTP_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WebHTTP_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFService_IIS_WSHTTP_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WSHTTP_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFService_IIS_BasicHTTP_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_BasicHTTP_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFService_IIS_WebHTTP_DT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WebHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFService_IIS_WSHTTP_DT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WSHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFService_IIS_BasicHTTP_DT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_BasicHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFService_IIS_WebHTTP_CAT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WebHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFService_IIS_WSHTTP_CAT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_WSHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFService_IIS_BasicHTTP_CAT_ASPEnabled : WCFService_IIS_ASPEnabled
    {
        public WCFService_IIS_BasicHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

}
