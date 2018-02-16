using System;
using System.Collections.Generic;
using System.IO;

namespace ArtifactBuilder
{
    public class FrameworkAgentComponents
    {
        public FrameworkAgentComponents(string configuration, string platform, string sourcePath)
        {
            Configuration = configuration;
            Platform = platform;
            SourcePath = $@"{sourcePath}\Agent";
            CreateAgentComponents();
        }

        public string Configuration { get; }
        public string Platform { get; }
        public string SourcePath { get; }
        public List<string> ExtensionDirectoryComponents { get; private set; }
        public List<string> WrapperXmlFiles { get; private set; }
        public List<string> RootInstallDirectoryComponents { get; private set; }
        public string AgentApiDll;
        private string ExtensionXsd;
        
        public string Version
        {
            get
            {
                try
                {
                    return System.Diagnostics.FileVersionInfo.GetVersionInfo(AgentApiDll).FileVersion;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public List<string> GetMissingComponents()
        {
            var missingComponents = new List<string>();

            foreach (var item in RootInstallDirectoryComponents)
            {
                if (!File.Exists(item)) missingComponents.Add(item);
            }

            foreach (var item in ExtensionDirectoryComponents)
            {
                if (!File.Exists(item)) missingComponents.Add(item);
            }

            foreach (var item in WrapperXmlFiles)
            {
                if (!File.Exists(item)) missingComponents.Add(item);
            }

            if (!File.Exists(AgentApiDll)) missingComponents.Add(AgentApiDll);
            if (!File.Exists(ExtensionXsd)) missingComponents.Add(ExtensionXsd);

            return missingComponents;
        }

        private void CreateAgentComponents()
        {
            var agentDllsForExtensionDirectory = new List<string>()
            {
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\JetBrains.Annotations.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Agent.AttributeFilter.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Agent.Configuration.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Agent.Core.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Agent.LabelsService.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Core.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Parsing.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Trie.dll"
            };

            var storageProviders = new List<string>()
            {
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Storage.CallContext.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Storage.HttpContext.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Storage.OperationContext.dll"
            };

            var wrapperProviders = new List<string>()
            {
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Asp35.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Couchbase.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.CustomInstrumentation.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.CustomInstrumentationAsync.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.HttpClient.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.HttpWebRequest.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.MongoDb.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Msmq.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Mvc3.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.NServiceBus.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.OpenRasta.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Owin.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Owin3.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.RabbitMq.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.RestSharp.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Sql.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.SqlAsync.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Wcf3.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WebApi1.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WebApi2.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WebOptimization.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WebServices.dll",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WrapperUtilities.dll"
            };

            var wrapperXmls = new List<string>()
            {
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Couchbase.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.CustomInstrumentation.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.HttpWebRequest.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.MongoDb.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Msmq.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Mvc3.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.NServiceBus.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.OpenRasta.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Owin.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Owin3.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.RestSharp.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.SqlAsync.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.Wcf3.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WebApi1.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WebApi2.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WebOptimization.Instrumentation.xml",
                $@"{SourcePath}\New Relic Home {Platform}\Extensions\NewRelic.Providers.Wrapper.WebServices.Instrumentation.xml",
                //$@"{SourcePath}\New Relic Home {Platform}\NewRelic.Providers.Wrapper.WrapperUtilities.Instrumentation.xml"
            };

            ExtensionXsd = $@"{SourcePath}\New Relic Home {Platform}\Extensions\extension.xsd";

            var root = new List<string>()
            {
                $@"{SourcePath}\New Relic Home {Platform}\NewRelic.Agent.Core.dll",
                //$@"{SourcePath}\New Relic Home {Platform}\NewRelic.Agent.Core.pdb",
                $@"{SourcePath}\New Relic Home {Platform}\NewRelic.Agent.Extensions.dll",
                $@"{SourcePath}\New Relic Home {Platform}\newrelic.config",
                $@"{SourcePath}\New Relic Home {Platform}\NewRelic.Profiler.dll",
                $@"{SourcePath}\New Relic Home {Platform}\newrelic.xsd"
            };

            ExtensionDirectoryComponents = new List<string>();
            ExtensionDirectoryComponents.AddRange(agentDllsForExtensionDirectory);
            ExtensionDirectoryComponents.AddRange(storageProviders);
            ExtensionDirectoryComponents.AddRange(wrapperProviders);
            ExtensionDirectoryComponents.Add(ExtensionXsd);

            WrapperXmlFiles = new List<string>();
            WrapperXmlFiles.AddRange(wrapperXmls);

            RootInstallDirectoryComponents = new List<string>();
            RootInstallDirectoryComponents.AddRange(root);

            AgentApiDll = $@"{SourcePath}\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\net45\NewRelic.Api.Agent.dll";
        }
    }
}
