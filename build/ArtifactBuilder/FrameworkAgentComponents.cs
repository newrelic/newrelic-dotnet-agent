/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;

namespace ArtifactBuilder
{
    public class FrameworkAgentComponents : AgentComponents
    {
        public FrameworkAgentComponents(string configuration, string platform, string repoRootDirectory, string homeRootPath)
            : base(configuration, platform, repoRootDirectory, homeRootPath) { }

        protected override string SourceHomeBuilderPath => $@"{HomeRootPath}\newrelichome_{Platform}";

        protected override List<string> IgnoredHomeBuilderFiles => new List<string>() {
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Agent.AttributeFilter.dll",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Agent.Configuration.dll",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Agent.Core.dll",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Agent.LabelsService.dll",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Core.Instrumentation.xml",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Parsing.Instrumentation.xml",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CustomInstrumentation.Instrumentation.xml",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WrapperUtilities.Instrumentation.xml",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Trie.dll",
            $@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.pdb"
        };

        protected override void CreateAgentComponents()
        {
            var agentDllsForExtensionDirectory = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Core.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Parsing.dll"
            };

            var transactionContextDlls = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.CallStack.AsyncLocal.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.TransactionContext.Asp.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.TransactionContext.Wcf3.dll"
            };

            var wrapperProviders = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Asp35.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Couchbase.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CustomInstrumentation.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CustomInstrumentationAsync.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpClient.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpWebRequest.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Msmq.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Mvc3.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NServiceBus.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.OpenRasta.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Owin.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Owin3.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RabbitMq.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RestSharp.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Sql.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.SqlAsync.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Wcf3.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebApi1.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebApi2.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebOptimization.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebServices.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WrapperUtilities.dll"
            };

            var wrapperXmls = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Couchbase.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpWebRequest.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Msmq.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Mvc3.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NServiceBus.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.OpenRasta.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Owin.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Owin3.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RestSharp.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.SqlAsync.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Wcf3.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebApi1.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebApi2.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebOptimization.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebServices.Instrumentation.xml"
            };

            ExtensionXsd = $@"{SourceHomeBuilderPath}\extensions\extension.xsd";
            NewRelicXsd = $@"{SourceHomeBuilderPath}\newrelic.xsd";
            NewRelicConfig = $@"{SourceHomeBuilderPath}\newrelic.config";

            NewRelicLicenseFile = $@"{SourceHomeBuilderPath}\LICENSE.txt";
            NewRelicThirdPartyNoticesFile = $@"{SourceHomeBuilderPath}\THIRD_PARTY_NOTICES.txt";
            
            var root = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.dll",
                $@"{SourceHomeBuilderPath}\NewRelic.Agent.Extensions.dll",
                NewRelicConfig,
                $@"{SourceHomeBuilderPath}\NewRelic.Profiler.dll",
                NewRelicXsd,
                NewRelicLicenseFile,
                NewRelicThirdPartyNoticesFile
            };

            ExtensionDirectoryComponents = new List<string>();
            ExtensionDirectoryComponents.AddRange(agentDllsForExtensionDirectory);
            ExtensionDirectoryComponents.AddRange(transactionContextDlls);
            ExtensionDirectoryComponents.AddRange(wrapperProviders);
            ExtensionDirectoryComponents.Add(ExtensionXsd);

            WrapperXmlFiles = new List<string>();
            WrapperXmlFiles.AddRange(wrapperXmls);

            RootInstallDirectoryComponents = new List<string>();
            RootInstallDirectoryComponents.AddRange(root);

            AgentApiDll = $@"{SourcePath}\..\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\net35\NewRelic.Api.Agent.dll";
        }
    }
}
