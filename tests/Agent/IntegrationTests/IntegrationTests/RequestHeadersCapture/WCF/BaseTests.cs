// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Agent.IntegrationTests.WCF;
using Xunit.Abstractions;
using static NewRelic.Agent.IntegrationTests.WCF.WCFTestBase;

namespace NewRelic.Agent.IntegrationTests.RequestHeadersCapture.WCF
{
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

            _fixture.SetTimeout(TimeSpan.FromMinutes(5));

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
                    _fixture.AddCommand($"WCFServiceIISHosted StartService {Path.Combine(_fixture.IntegrationTestAppPath, "WcfAppIisHosted", "Deploy")} {_binding} {_fixture.RemoteApplication.Port} Test_{_binding} {_hostingModel != HostingModel.IISNoAsp}");
                    _fixture.AddCommand($"WCFClient InitializeClient_IISHosted {_binding} {_fixture.RemoteApplication.Port} Test_{_binding}");
                    break;
            }
        }
    }
}
