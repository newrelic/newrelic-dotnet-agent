// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RequestHeadersCapture.WCF
{
    public abstract class AllowAllHeadersEnabledTests : AllowAllHeadersDisabledTests
    {
        public AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, HostingModel hostingModel, WCFBindingType bindingType)
            : base(fixture, output, hostingModel, bindingType) { }

        protected override void SetupConfiguration()
        {
            base.SetupConfiguration();

            _fixture.RemoteApplication.NewRelicConfig.SetAllowAllHeaders(true);
        }

        protected override IDictionary<string, string> ExpectedHeaders => new Dictionary<string, string>
        {
            { "request.method", "POST" },
            { "request.headers.referer", "http://example.com/" },
            { "request.headers.accept", "text/html" },
            { "request.headers.content-length", GetExpectedContentLength() },
            { "request.headers.host", $"localhost:{_fixture.RemoteApplication.Port}" },
            { "request.headers.user-agent", "FakeUserAgent" },
            { "request.headers.foo", "bar" },
            { "request.headers.dashes-are-valid", "true" },
            { "request.headers.dashesarevalid", "definitely" }
        };

        protected override IEnumerable<string> UnexpectedHeaders => new[] { "request.headers.was-never-included" };
    }

    #region IIS

    [NetFrameworkTest]
    public class IIS_Basic_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public IIS_Basic_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IIS, WCFBindingType.BasicHttp) { }
    }

    [NetFrameworkTest]
    public class IIS_Web_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public IIS_Web_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IIS, WCFBindingType.WebHttp) { }
    }

    [NetFrameworkTest]
    public class IIS_WS_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public IIS_WS_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IIS, WCFBindingType.WSHttpUnsecure) { }
    }

    #endregion IIS

    #region IISNoAsp

    [NetFrameworkTest]
    public class IISNoAsp_Basic_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public IISNoAsp_Basic_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IISNoAsp, WCFBindingType.BasicHttp) { }
    }

    [NetFrameworkTest]
    public class IISNoAsp_Web_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public IISNoAsp_Web_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IISNoAsp, WCFBindingType.WebHttp) { }
    }


    [NetFrameworkTest]
    public class IISNoAsp_WS_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public IISNoAsp_WS_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.IISNoAsp, WCFBindingType.WSHttp) { }
    }

    #endregion IISNoAsp

    #region Self

    [NetFrameworkTest]
    public class Self_Basic_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public Self_Basic_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.Self, WCFBindingType.BasicHttp) { }
    }

    [NetFrameworkTest]
    public class Self_Web_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public Self_Web_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.Self, WCFBindingType.WebHttp) { }
    }

    [NetFrameworkTest]
    public class Self_WS_AllowAllHeadersEnabledTests : AllowAllHeadersEnabledTests
    {
        public Self_WS_AllowAllHeadersEnabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, HostingModel.Self, WCFBindingType.WSHttp) { }
    }

    #endregion Self

}
#endif
