using System.Collections.Generic;
using System.Linq;

namespace ArtifactBuilder
{
    public class CoreAgentComponents : AgentComponents
    {
        public CoreAgentComponents(string configuration, string platform, string repoRootDirectory, string homeRootPath)
            : base(configuration, platform, repoRootDirectory, homeRootPath) { }

        protected override string SourceHomeBuilderPath => $@"{HomeRootPath}\newrelichome_{Platform}_coreclr";

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
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpClient.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb26.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RabbitMq.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Sql.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.dll",
            };

            var wrapperXmls = new[]
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CosmosDb.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml",
            };

            ExtensionXsd = $@"{SourceHomeBuilderPath}\extensions\extension.xsd";
            NewRelicXsd = $@"{SourceHomeBuilderPath}\newrelic.xsd";
            NewRelicConfig = $@"{SourceHomeBuilderPath}\newrelic.config";

            NewRelicLicenseFile = $@"{SourceHomeBuilderPath}\LICENSE.txt";
            NewRelicThirdPartyNoticesFile = $@"{SourceHomeBuilderPath}\THIRD_PARTY_NOTICES.txt";

            WindowsProfiler = $@"{SourceHomeBuilderPath}\NewRelic.Profiler.dll";

            GRPCExtensionsLibWindows = new[]
            {
                $@"{SourceHomeBuilderPath}\grpc_csharp_ext.x64.dll",
                $@"{SourceHomeBuilderPath}\grpc_csharp_ext.x86.dll"
            };

            var root = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.dll",
                $@"{SourceHomeBuilderPath}\NewRelic.Agent.Extensions.dll",
                $@"{SourceHomeBuilderPath}\NewRelic.Api.Agent.dll",
                NewRelicConfig,
                WindowsProfiler,
                NewRelicXsd,
                NewRelicLicenseFile,
                NewRelicThirdPartyNoticesFile,
                $@"{SourceHomeBuilderPath}\README.md"
            };

            root.AddRange(GRPCExtensionsLibWindows);

            SetRootInstallDirectoryComponents(root.ToArray());

            var extensions = agentDllsForExtensionDirectory
                .Concat(storageProviders)
                .Concat(wrapperProviders)
                .Append(ExtensionXsd)
                .ToArray();

            SetExtensionDirectoryComponents(extensions);

            SetWrapperXmlFiles(wrapperXmls);

            AgentApiDll = $@"{SourcePath}\..\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\netstandard2.0\NewRelic.Api.Agent.dll";

            LinuxProfiler = Platform == "x64"
                ? $@"{HomeRootPath}\newrelichome_x64_coreclr_linux\libNewRelicProfiler.so"
                : null;

            GRPCExtensionsLibLinux = new[]
            {
                $@"{HomeRootPath}\newrelichome_x64_coreclr_linux\libgrpc_csharp_ext.x64.so"
            };

            AgentInfoJson = $@"{SourcePath}\..\src\Agent\Miscellaneous\{Platform}\agentinfo.json";

            SetConfigurationComponents(NewRelicXsd, AgentInfoJson);
        }
    }
}
