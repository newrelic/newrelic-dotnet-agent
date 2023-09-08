// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Agent.IntegrationTests.WCF;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RequestHeadersCapture.WCF
{
    public abstract class AllowAllHeadersDisabledTests : WCFEmptyTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AllowAllHeadersDisabledTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, HostingModel hostingModel, WCFBindingType bindingType)
            : base(fixture, output, hostingModel, bindingType) { }

        protected virtual IEnumerable<string> UnallowedHeaders => new[]
        {
            "request.headers.cookie",
            "request.headers.authorization",
            "request.headers.proxy-authorization",
            "request.headers.x-forwarded-for"
        };

        protected virtual IDictionary<string, string> ExpectedHeaders => new Dictionary<string, string>
        {
            { "request.method", "POST" },
            { "request.headers.referer", "http://example.com/" },
            { "request.headers.accept", "text/html" },
            { "request.headers.content-length", GetExpectedContentLength() },
            { "request.headers.host", $"localhost:{_fixture.RemoteApplication.Port}" },
            { "request.headers.user-agent", "FakeUserAgent" },
        };

        protected virtual IEnumerable<string> UnexpectedHeaders => new[]
        {
            "request.headers.foo",
            "request.headers.dashes-are-valid",
            "request.headers.dashesarevalid"
        };

        protected string GetExpectedContentLength()
        {
            switch (_binding)
            {
                case WCFBindingType.BasicHttp: return "168";
                case WCFBindingType.WebHttp: return "75";
                case WCFBindingType.WSHttpUnsecure: return "573";
                case WCFBindingType.WSHttp: return _hostingModel == HostingModel.Self ? "6105" : "6119";
                default: return null;
            }
        }

        [Fact]
        public virtual void TestHeadersCaptured()
        {
            var transactionSample = _logHelpers.TrxSamples_Service.FirstOrDefault(ts => ts.Uri?.Contains($"Test_{_binding}") ?? false);

            Assert.NotNull(transactionSample);
            Assertions.TransactionTraceDoesNotHaveAttributes(UnallowedHeaders, TransactionTraceAttributeType.Agent, transactionSample);
            Assertions.TransactionTraceHasAttributes(ExpectedHeaders, TransactionTraceAttributeType.Agent, transactionSample);
            Assertions.TransactionTraceDoesNotHaveAttributes(UnexpectedHeaders, TransactionTraceAttributeType.Agent, transactionSample);

            var transactionEvent = _logHelpers.TrxEvents_Service.FirstOrDefault(te => te.AgentAttributes?["request.uri"]?.ToString()?.Contains($"Test_{_binding}") ?? false);

            Assert.NotNull(transactionEvent);
            Assertions.TransactionEventDoesNotHaveAttributes(UnallowedHeaders, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventHasAttributes(ExpectedHeaders, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventDoesNotHaveAttributes(UnexpectedHeaders, TransactionEventAttributeType.Agent, transactionEvent);

            var spanEvent = _logHelpers.SpanEvents_Service.FirstOrDefault(se => se.AgentAttributes?["request.uri"]?.ToString()?.Contains($"Test_{_binding}") ?? false);

            Assert.NotNull(spanEvent);
            Assertions.SpanEventDoesNotHaveAttributes(UnallowedHeaders, SpanEventAttributeType.Agent, spanEvent);
            Assertions.SpanEventHasAttributes(ExpectedHeaders, SpanEventAttributeType.Agent, spanEvent);
            Assertions.SpanEventDoesNotHaveAttributes(UnexpectedHeaders, SpanEventAttributeType.Agent, spanEvent);
        }

        protected override void SetupConfiguration()
        {
            base.SetupConfiguration();

            _fixture.RemoteApplication.NewRelicConfig.SetAllowAllHeaders(false);
        }

        protected override void AddFixtureCommands()
        {
            base.AddFixtureCommands();

            _fixture.AddCommand($"WCFClient GetDataWithHeaders");
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
            : base(fixture, output, HostingModel.IIS, WCFBindingType.WSHttpUnsecure) { }
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
#endif
