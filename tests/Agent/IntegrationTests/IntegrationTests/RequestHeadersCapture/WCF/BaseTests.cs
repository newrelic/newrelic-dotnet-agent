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
    public abstract partial class WCFEmptyTestBase<T> : NewRelicIntegrationTest<T> where T : ConsoleDynamicMethodFixture
    {
        protected virtual IEnumerable<string> UnallowedHeaders => new[]
        {
            "request.headers.cookie",
            "request.headers.authorization",
            "request.headers.proxy-authorization",
            "request.headers.x-forwarded-for"
        };

        protected virtual IDictionary<string, string> ExpectedHeaders => new Dictionary<string, string>
        {
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
                case WCFBindingType.BasicHttp: return "166";
                case WCFBindingType.WebHttp: return "73";
                case WCFBindingType.WSHttp: return _hostingModel == HostingModel.Self ? "6105" : "6119";
                default: return null;
            }
        }

        public virtual void TestHeadersCaptured()
        {
            if (_hostingModel != HostingModel.Self)
            {
                var transactionSample = _logHelpers.TrxSamples_Service.FirstOrDefault(ts => ts.Uri?.Contains($"Test_{_binding}") ?? false);

                Assert.NotNull(transactionSample);
                Assertions.TransactionTraceDoesNotHaveAttributes(UnallowedHeaders, TransactionTraceAttributeType.Agent, transactionSample);
                Assertions.TransactionTraceHasAttributes(ExpectedHeaders, TransactionTraceAttributeType.Agent, transactionSample);
                Assertions.TransactionTraceDoesNotHaveAttributes(UnexpectedHeaders, TransactionTraceAttributeType.Agent, transactionSample);
            }

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
    }

    public abstract partial class WCFEmptyTestBase<T> : NewRelicIntegrationTest<T> where T : ConsoleDynamicMethodFixture
    {
        protected readonly T _fixture;

        protected readonly HostingModel _hostingModel;
        protected readonly WCFBindingType _binding;
        protected readonly IWCFLogHelpers _logHelpers;

        public WCFEmptyTestBase(T fixture, ITestOutputHelper output, HostingModel hostingModelOption, WCFBindingType bindingToTest)
            : base(fixture)
        {
            _hostingModel = hostingModelOption;
            _binding = bindingToTest;

            _logHelpers = hostingModelOption == HostingModel.Self ? (IWCFLogHelpers)new WCFLogHelpers_SelfHosted(fixture) : new WCFLogHelpers_IISHosted(fixture);

            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    SetupConfiguration();
                    AddFixtureCommands();
                }
            );

            _fixture.Initialize();
        }

        protected virtual NewRelicConfigModifier SetupConfiguration()
        {
            var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);

            configModifier.ForceTransactionTraces()
                .EnableSpanEvents(true);

            return configModifier;
        }

        protected virtual void AddFixtureCommands()
        {
            switch (_hostingModel)
            {
                case HostingModel.Self:
                    _fixture.AddCommand($"WCFServiceSelfHosted StartService {_binding} {_fixture.RemoteApplication.Port} Test_{_binding}");
                    _fixture.AddCommand($"WCFClient InitializeClient_SelfHosted {_binding} {_fixture.RemoteApplication.Port} Test_{_binding}");
                    break;
                case HostingModel.IIS:
                case HostingModel.IISNoAsp:
                    _fixture.AddCommand($"WCFServiceIISHosted StartService {Path.Combine(_fixture.IntegrationTestAppPath, "WcfAppIisHosted", "Deploy")} {_binding} {_fixture.RemoteApplication.Port} Test_{_binding} {_hostingModel == HostingModel.IISNoAsp}");
                    _fixture.AddCommand($"WCFClient InitializeClient_IISHosted {_binding} {_fixture.RemoteApplication.Port} Test_{_binding}");
                    break;
            }
        }
    }
}
