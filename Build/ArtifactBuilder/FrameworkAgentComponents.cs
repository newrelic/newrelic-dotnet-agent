using System.Collections.Generic;
using System.Linq;

namespace ArtifactBuilder
{
	public class FrameworkAgentComponents : AgentComponents
	{
		public FrameworkAgentComponents(string configuration, string platform, string sourcePath)
			: base(configuration, platform, sourcePath) { }

		protected override string SourceHomeBuilderPath => $@"{SourcePath}\New Relic Home {Platform}";

		protected override List<string> IgnoredHomeBuilderFiles => new List<string>() {
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Core.Instrumentation.xml",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Parsing.Instrumentation.xml",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WrapperUtilities.Instrumentation.xml"
		};

		protected override void CreateAgentComponents()
		{
			var agentDllsForExtensionDirectory = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\JetBrains.Annotations.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Core.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Parsing.dll"
			};

			var storageProviders = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Storage.CallContext.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Storage.HttpContext.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Storage.OperationContext.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Storage.AsyncLocal.dll",
			};

			var wrapperProviders = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Asp35.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Couchbase.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpClient.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpWebRequest.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb26.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Msmq.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Mvc3.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.NServiceBus.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.OpenRasta.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.RabbitMq.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.RestSharp.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Sql.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Wcf3.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebApi1.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebApi2.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebOptimization.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebServices.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.AspNetCore.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Owin.dll"
			};

			var wrapperXmls = new[]
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Couchbase.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpWebRequest.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Msmq.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Mvc3.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.NServiceBus.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.OpenRasta.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Owin.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.RestSharp.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Wcf3.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebApi1.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebApi2.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebOptimization.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebServices.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml",
			};

			ExtensionXsd = $@"{SourceHomeBuilderPath}\Extensions\extension.xsd";
			NewRelicXsd = $@"{SourceHomeBuilderPath}\newrelic.xsd";
			NewRelicConfig = $@"{SourceHomeBuilderPath}\newrelic.config";

			NewRelicLicenseFile = $@"{SourceHomeBuilderPath}\LICENSE.txt";
			NewRelicThirdPartyNoticesFile = $@"{SourceHomeBuilderPath}\THIRD_PARTY_NOTICES.txt";

			WindowsProfiler = $@"{SourceHomeBuilderPath}\NewRelic.Profiler.dll";

			var root = new[]
			{
				$@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.dll",
				$@"{SourceHomeBuilderPath}\NewRelic.Agent.Extensions.dll",
				NewRelicConfig,
				WindowsProfiler,
				NewRelicXsd,
				NewRelicLicenseFile,
				NewRelicThirdPartyNoticesFile
			};

			SetRootInstallDirectoryComponents(root);

			var extensions = agentDllsForExtensionDirectory
				.Concat(storageProviders)
				.Concat(wrapperProviders)
				.Append(ExtensionXsd)
				.ToArray();

			SetExtensionDirectoryComponents(extensions);
			
			SetWrapperXmlFiles(wrapperXmls);

			AgentInfoJson = $@"{SourcePath}\Agent\Miscellaneous\{Platform}\agentinfo.json";

			AgentApiDll = $@"{SourcePath}\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\net45\NewRelic.Api.Agent.dll";

			SetConfigurationComponents(NewRelicXsd , AgentInfoJson);
		}
	}
}
