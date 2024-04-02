using System.Collections.Generic;
using System.Linq;

namespace ArtifactBuilder
{
    public class FrameworkAgentComponents : AgentComponents
    {
        public FrameworkAgentComponents(string configuration, string platform, string repoRootDirectory, string homeRootPath)
            : base(configuration, platform, repoRootDirectory, homeRootPath) { }

        protected override string SourceHomeBuilderPath => $@"{HomeRootPath}\newrelichome_{Platform}";

        protected override List<string> IgnoredHomeBuilderFiles => new List<string>() {
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Core.Instrumentation.xml",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Parsing.Instrumentation.xml",
            $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WrapperUtilities.Instrumentation.xml"
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
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Storage.CallContext.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Storage.HttpContext.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Storage.OperationContext.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Storage.AsyncLocal.dll",
            };

            var wrapperProviders = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNet.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CosmosDb.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Couchbase.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Elasticsearch.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpClient.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpWebRequest.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Log4NetLogging.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.SerilogLogging.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NLogLogging.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MicrosoftExtensionsLogging.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb26.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Msmq.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Mvc3.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NServiceBus.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.OpenRasta.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RabbitMq.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RestSharp.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Sql.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis2Plus.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Wcf3.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebApi1.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebApi2.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebOptimization.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebServices.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNetCore.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Owin.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MassTransit.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MassTransitLegacy.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Kafka.dll",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Bedrock.dll",
            };

            var wrapperXmls = new[]
            {
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNet.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.CosmosDb.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Couchbase.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Elasticsearch.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.HttpWebRequest.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Log4NetLogging.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.SerilogLogging.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NLogLogging.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MicrosoftExtensionsLogging.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Msmq.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Mvc3.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.NServiceBus.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.OpenRasta.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Owin.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.RestSharp.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.StackExchangeRedis2Plus.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Wcf3.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebApi1.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebApi2.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebOptimization.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.WebServices.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MassTransit.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.MassTransitLegacy.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Kafka.Instrumentation.xml",
                $@"{SourceHomeBuilderPath}\extensions\NewRelic.Providers.Wrapper.Bedrock.Instrumentation.xml",
            };

            ExtensionXsd = $@"{SourceHomeBuilderPath}\extensions\extension.xsd";
            NewRelicXsd = $@"{SourceHomeBuilderPath}\newrelic.xsd";
            NewRelicConfig = $@"{SourceHomeBuilderPath}\newrelic.config";

            NewRelicLicenseFile = $@"{SourceHomeBuilderPath}\LICENSE.txt";
            NewRelicThirdPartyNoticesFile = $@"{SourceHomeBuilderPath}\THIRD_PARTY_NOTICES.txt";

            WindowsProfiler = $@"{SourceHomeBuilderPath}\NewRelic.Profiler.dll";

            GRPCExtensionsLibWindows = new[]
                {   $@"{SourceHomeBuilderPath}\grpc_csharp_ext.x86.dll",
                    $@"{SourceHomeBuilderPath}\grpc_csharp_ext.x64.dll"
                };

            var installRootFiles = new List<string>()
            {
                NewRelicLicenseFile,
                NewRelicThirdPartyNoticesFile,
            };

            SetRootInstallDirectoryComponents(installRootFiles.ToArray());

            var agentHomeDirFiles = new List<string>()
            {
                $@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.dll",
                $@"{SourceHomeBuilderPath}\NewRelic.Agent.Extensions.dll",
                WindowsProfiler,
                NewRelicConfig,
                NewRelicXsd
            };

            agentHomeDirFiles.AddRange(GRPCExtensionsLibWindows);


            SetAgentHomeDirComponents(agentHomeDirFiles.ToArray());

            var extensions = agentDllsForExtensionDirectory
                .Concat(storageProviders)
                .Concat(wrapperProviders)
                .Append(ExtensionXsd)
                .ToArray();

            SetExtensionDirectoryComponents(extensions);

            SetWrapperXmlFiles(wrapperXmls);

            AgentInfoJson = $@"{SourcePath}\..\src\Agent\Miscellaneous\{Platform}\agentinfo.json";

            AgentApiDll = $@"{SourcePath}\..\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\net462\NewRelic.Api.Agent.dll";

            SetConfigurationComponents(NewRelicXsd, AgentInfoJson);
        }
    }
}
