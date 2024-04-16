using System.Collections.Generic;
using System.Linq;

namespace ArtifactBuilder
{
    public class CoreAgentComponents : AgentComponents
    {
        public CoreAgentComponents(string configuration, string platform, string repoRootDirectory, string homeRootPath)
            : base(configuration, platform, repoRootDirectory, homeRootPath) { }

        protected override string SourceHomeBuilderPath
        {
            get
            {
                // arm64 is only supported on linux, so this is a special case
                if (Platform == "arm64")
                {
                    return $@"{HomeRootPath}\newrelichome_arm64_coreclr_linux";
                }
                return $@"{HomeRootPath}\newrelichome_{Platform}_coreclr";
            }
        }

        protected override List<string> IgnoredHomeBuilderFiles => new List<string>() {
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Core.Instrumentation.xml",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Parsing.Instrumentation.xml"
        };

        protected override void CreateAgentComponents()
        {
            var agentDllsForExtensionDirectory = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Core.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Parsing.dll"
            };

            var storageProviders = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Storage.AsyncLocal.dll",
            };

            var wrapperProviders = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNetCore.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CosmosDb.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Elasticsearch.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpClient.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Log4NetLogging.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.SerilogLogging.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NLogLogging.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MicrosoftExtensionsLogging.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb26.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RabbitMq.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Sql.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis2Plus.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NServiceBus.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MassTransit.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MassTransitLegacy.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Kafka.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNetCore6Plus.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Bedrock.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AwsLambda.dll",
            };

            var wrapperXmls = new[]
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CosmosDb.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Elasticsearch.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Log4NetLogging.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.SerilogLogging.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NLogLogging.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MicrosoftExtensionsLogging.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis2Plus.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NServiceBus.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MassTransit.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MassTransitLegacy.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Kafka.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNetCore6Plus.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Bedrock.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AwsLambda.Instrumentation.xml",
            };

            ExtensionXsd = $@"{SourceHomeBuilderPath}\extensions\extension.xsd";
            NewRelicXsd = $@"{SourceHomeBuilderPath}\newrelic.xsd";
            NewRelicConfig = $@"{SourceHomeBuilderPath}\newrelic.config";

            NewRelicLicenseFile = $@"{SourceHomeBuilderPath}\LICENSE.txt";
            NewRelicThirdPartyNoticesFile = $@"{SourceHomeBuilderPath}\THIRD_PARTY_NOTICES.txt";

            var installRootFiles = new List<string>()
            {
                NewRelicLicenseFile,
                NewRelicThirdPartyNoticesFile,
                $@"{SourceHomeBuilderPath}\README.md"
            };

            SetRootInstallDirectoryComponents(installRootFiles.ToArray());

            WindowsProfiler = null;
            if (Platform != "arm64")
            {
                WindowsProfiler = $@"{SourceHomeBuilderPath}\NewRelic.Profiler.dll";
            }

            var agentHomeDirFiles = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.dll",
                $@"{SourceHomeBuilderPath}\NewRelic.Agent.Extensions.dll",
                $@"{SourceHomeBuilderPath}\NewRelic.Api.Agent.dll",
                NewRelicConfig,
                NewRelicXsd
            };
            
            if (!string.IsNullOrWhiteSpace(WindowsProfiler))
            {
                agentHomeDirFiles.Add(WindowsProfiler);
            }

            SetAgentHomeDirComponents(agentHomeDirFiles.ToArray());

            var extensions = agentDllsForExtensionDirectory
               .Concat(storageProviders)
               .Concat(wrapperProviders)
               .Append(ExtensionXsd)
               .ToArray();
            SetExtensionDirectoryComponents(extensions);

            SetWrapperXmlFiles(wrapperXmls);

            AgentApiDll = $@"{SourcePath}\..\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\netstandard2.0\NewRelic.Api.Agent.dll";

            LinuxProfiler = null;
            if (Platform == "x64") 
            {
                LinuxProfiler = $@"{HomeRootPath}\newrelichome_x64_coreclr_linux\libNewRelicProfiler.so";
            } 
            else if (Platform == "arm64") 
            {
                LinuxProfiler = $@"{HomeRootPath}\newrelichome_arm64_coreclr_linux\libNewRelicProfiler.so";
            }

            var configurationComponents = new List<string> { NewRelicXsd };
            if (Platform != "arm64")
            {
                AgentInfoJson = $@"{SourcePath}\..\src\Agent\Miscellaneous\{Platform}\agentinfo.json";
                configurationComponents.Add(AgentInfoJson);
            }

            SetConfigurationComponents(configurationComponents.ToArray());
        }
    }
}
