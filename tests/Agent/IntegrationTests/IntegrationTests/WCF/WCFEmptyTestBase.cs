// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.IO;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.WCF
{
    public abstract partial class WCFEmptyTestBase<T> : NewRelicIntegrationTest<T> where T : ConsoleDynamicMethodFixture
    {
        protected readonly T _fixture;
        protected readonly HostingModel _hostingModel;
        protected readonly WCFBindingType _binding;
        protected readonly IWCFLogHelpers _logHelpers;
        protected readonly string _relativePath;

        protected string IISWebAppPublishPath => Path.Combine(_fixture.IntegrationTestAppPath, "WcfAppIisHosted", "Deploy");

        public WCFEmptyTestBase(T fixture, ITestOutputHelper output, HostingModel hostingModelOption, WCFBindingType bindingToTest)
            : base(fixture)
        {
            _hostingModel = hostingModelOption;
            _binding = bindingToTest;
            _relativePath = $"Test_{_binding}";

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

        protected virtual void SetupConfiguration()
        {
            _fixture.RemoteApplication.NewRelicConfig.ForceTransactionTraces();
            _fixture.RemoteApplication.NewRelicConfig.EnableSpanEvents(true);
        }

        protected virtual void AddFixtureCommands()
        {
            switch (_hostingModel)
            {
                case HostingModel.Self:
                    _fixture.AddCommand($"WCFServiceSelfHosted StartService {_binding} {_fixture.RemoteApplication.Port} {_relativePath}");
                    _fixture.AddCommand($"WCFClient InitializeClient_SelfHosted {_binding} {_fixture.RemoteApplication.Port} {_relativePath}");
                    break;
                case HostingModel.IIS:
                case HostingModel.IISNoAsp:
                    _fixture.AddCommand($"WCFServiceIISHosted StartService {IISWebAppPublishPath} {_binding} {_fixture.RemoteApplication.Port} {_relativePath} {_hostingModel != HostingModel.IISNoAsp}");
                    _fixture.AddCommand($"WCFClient InitializeClient_IISHosted {_binding} {_fixture.RemoteApplication.Port} {_relativePath}");
                    break;
            }
        }
    }
}
#endif
