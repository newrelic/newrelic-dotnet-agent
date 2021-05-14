// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using System;
using Xunit;
using Xunit.Abstractions;
using System.IO;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTests.WCF.Service.IIS.ASPEnabled
{
    public  class WCFServiceTestWCFServiceExternalCallsTests : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureFWLatest>
    {
        public enum TracingTestOption
        {
            CAT,
            DT,
            None
        }

        public enum HostingModel
        {
            Self,
            IIS
        }

        public enum ASPCompatibilityMode
        {
            Enabled,
            Disabled
        }

        protected readonly ConsoleDynamicMethodFixtureFWLatest _fixture;

        protected readonly WCFBindingType _bindingToTest;
        protected string ExpectedTransportType => _bindingToTest == WCFBindingType.NetTcp
            ? "Other"
            : "HTTP";

        protected readonly WCFInvocationMethod[] _serviceInvocationMethodsToTest;
        protected int _countServiceInvocationMethodsToTest => _serviceInvocationMethodsToTest.Length;

        protected readonly WCFInvocationMethod[] _clientInvocationMethodsToTest;
        protected int _countClientInvocationMethodsToTest => _clientInvocationMethodsToTest.Length;

        protected readonly HostingModel _hostingModelOption;
        protected readonly ASPCompatibilityMode _aspCompatibilityOption;

        protected readonly string _relativePath;

        //Helper Functions to obtain data from fixtures
        protected readonly IWCFLogHelpers LogHelpers;

        protected string IISWebAppPublishPath => Path.Combine(_fixture.IntegrationTestAppPath, "WcfAppIisHosted", "Deploy");

        public WCFServiceTestWCFServiceExternalCallsTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) :base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _bindingToTest = WCFBindingType.BasicHttp;
            _hostingModelOption = HostingModel.IIS;
            _aspCompatibilityOption = ASPCompatibilityMode.Enabled;

            _relativePath = $"Test_{_bindingToTest}";

            LogHelpers = new WCFLogHelpers_IISHosted(fixture);

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                    _fixture.RemoteApplication.NewRelicConfig.SetRequestTimeout(TimeSpan.FromSeconds(10));
                    _fixture.RemoteApplication.NewRelicConfig.ForceTransactionTraces();

                    GenerateFixtureCommands();

                    _fixture.SetTimeout(TimeSpan.FromMinutes(5));

                }
            );


            _fixture.Initialize();
        }

        /// <summary>
        /// Generates the console app commands to run based on the requested test pattern
        /// </summary>
        private void GenerateFixtureCommands()
        {

            switch (_hostingModelOption)
            {
                case HostingModel.Self:
                    _fixture.AddCommand($"WCFServiceSelfHosted StartService {_bindingToTest} {_fixture.RemoteApplication.Port} {_relativePath}");
                    break;
                case HostingModel.IIS:
                    _fixture.AddCommand($"WCFServiceIISHosted StartService {IISWebAppPublishPath} {_bindingToTest} {_fixture.RemoteApplication.Port} {_relativePath} {(_aspCompatibilityOption == ASPCompatibilityMode.Enabled).ToString().ToLower()}");
                    break;
            }

            _fixture.AddCommand(GetInitializeClientFixtureCommand());
            _fixture.AddCommand($"WCFClient TellWCFServerToMakeExternalCalls");

            switch (_hostingModelOption)
            {
                case HostingModel.Self:
                    _fixture.AddCommand("WCFServiceSelfHosted StopService");
                    break;
                case HostingModel.IIS:
                    _fixture.AddCommand("WCFServiceIISHosted StopService");
                    break;
            }
        }

        private string GetInitializeClientFixtureCommand()
        {
            switch (_hostingModelOption)
            {
                case HostingModel.Self:
                    return $"WCFClient InitializeClient_SelfHosted {_bindingToTest} {_fixture.RemoteApplication.Port} {_relativePath}";
                case HostingModel.IIS:
                    return $"WCFClient InitializeClient_IISHosted {_bindingToTest} {_fixture.RemoteApplication.Port} {_relativePath}";
                default:
                    throw new Exception($"Hosting Option {_hostingModelOption} is not supported");
            }
        }

        [Fact]
        public void ExternalCallsTests()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"WebTransactionTotalTime/WCF/NewRelic.Agent.IntegrationTests.Shared.Wcf.IWcfService.TAPMakeExternalCalls" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"External/google.com/Stream/GET" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"External/bing.com/Stream/GET" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"External/yahoo.com/Stream/GET" }
            };

            Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues);
        }

    }
}
