// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.WCF.Client.IIS
{

    public abstract class WCFClient_IIS : WCFClientTestBase
    {
        public WCFClient_IIS(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingType, WCFLegacyTracingTestOption tracingTestOption, ASPCompatibilityMode aspCompatOption)
            : base(fixture, output, bindingType, tracingTestOption, HostingModel.IIS, aspCompatOption, new WCFLogHelpers_IISHosted(fixture))
        {
        }
    }
}

namespace NewRelic.Agent.IntegrationTests.WCF.Client.IIS.ASPDisabled
{
    public abstract class WCFClient_IIS_ASPDisabled : WCFClient_IIS
    {
        public WCFClient_IIS_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingType, WCFLegacyTracingTestOption tracingTestOption)
            : base(fixture, output, bindingType, tracingTestOption, ASPCompatibilityMode.Disabled)
        {
        }
    }

    public class WCFClient_IIS_WebHTTP_ASPDiabled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_WebHTTP_ASPDiabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_IIS_WSHTTP_ASPDisabled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_WSHTTP_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_IIS_BasicHTTP_ASPDisabled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_BasicHTTP_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_IIS_WebHTTP_DT_ASPDisabled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_WebHTTP_DT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_IIS_WSHTTP_DT_ASPDisasbled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_WSHTTP_DT_ASPDisasbled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_IIS_BasicHTTP_DT_ASPDisabled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_BasicHTTP_DT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_IIS_WebHTTP_CAT_AspDisabled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_WebHTTP_CAT_AspDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFClient_IIS_WSHTTP_CAT_ASPDisabled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_WSHTTP_CAT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFClient_IIS_BasicHTTP_CAT_ASPDisabled : WCFClient_IIS_ASPDisabled
    {
        public WCFClient_IIS_BasicHTTP_CAT_ASPDisabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

}

namespace NewRelic.Agent.IntegrationTests.WCF.Client.IIS.ASPEnabled
{
    public abstract class WCFClient_IIS_ASPEnabled : WCFClient_IIS
    {
        public WCFClient_IIS_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingType, WCFLegacyTracingTestOption tracingTestOption)
            : base(fixture, output, bindingType, tracingTestOption, ASPCompatibilityMode.Enabled)
        {
        }
    }

    public class WCFClient_IIS_WebHTTP_ASPDiabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_WebHTTP_ASPDiabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_IIS_WSHTTP_ASPEnabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_WSHTTP_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_IIS_BasicHTTP_ASPEnabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_BasicHTTP_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.None)
        {
        }
    }

    public class WCFClient_IIS_WebHTTP_DT_ASPEnabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_WebHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_IIS_WSHTTP_DT_ASPEnabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_WSHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_IIS_BasicHTTP_DT_ASPEnabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_BasicHTTP_DT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.DT)
        {
        }
    }

    public class WCFClient_IIS_WebHTTP_CAT_ASPEnabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_WebHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WebHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFClient_IIS_WSHTTP_CAT_ASPEnabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_WSHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.WSHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }

    public class WCFClient_IIS_BasicHTTP_CAT_ASPEnabled : WCFClient_IIS_ASPEnabled
    {
        public WCFClient_IIS_BasicHTTP_CAT_ASPEnabled(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, WCFBindingType.BasicHttp, WCFLegacyTracingTestOption.CAT)
        {
        }
    }
}
