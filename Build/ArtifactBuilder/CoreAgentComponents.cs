using System.Collections.Generic;
using System.Linq;

namespace ArtifactBuilder
{
	public class CoreAgentComponents : AgentComponents
	{
		public CoreAgentComponents(string configuration, string platform, string sourcePath)
			: base(configuration, platform, sourcePath) { }

		protected override string SourceHomeBuilderPath => $@"{SourcePath}\New Relic Home {Platform} CoreClr";

		protected override List<string> IgnoredHomeBuilderFiles => new List<string>() {
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Core.Instrumentation.xml",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Parsing.Instrumentation.xml"
		};

		protected override void CreateAgentComponents()
		{
			var agentDllsForExtensionDirectory = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Core.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Parsing.dll"
			};

			var storageProviders = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Storage.AsyncLocal.dll",
			};

			var wrapperProviders = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.AspNetCore.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpClient.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb26.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Sql.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.dll",
			};

			var wrapperXmls = new[]
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml",
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
				$@"{SourceHomeBuilderPath}\NewRelic.Api.Agent.dll",
				NewRelicConfig,
				WindowsProfiler,
				NewRelicXsd,
				NewRelicLicenseFile,
				NewRelicThirdPartyNoticesFile,
				$@"{SourceHomeBuilderPath}\README.md",
			};

			SetRootInstallDirectoryComponents(root);

			var extensions = agentDllsForExtensionDirectory
				.Concat(storageProviders)
				.Concat(wrapperProviders)
				.Append(ExtensionXsd)
				.ToArray();

			SetExtensionDirectoryComponents(extensions);
			
			SetWrapperXmlFiles(wrapperXmls);

			AgentApiDll = $@"{SourcePath}\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\netstandard2.0\NewRelic.Api.Agent.dll";

			LinuxProfiler = Platform == "x64" 
				? $@"{SourcePath}\New Relic Home x64 CoreClr_Linux\libNewRelicProfiler.so"
				: null;

			AgentInfoJson = $@"{SourcePath}\Agent\Miscellaneous\{Platform}\agentinfo.json";

			SetConfigurationComponents(NewRelicXsd, AgentInfoJson);
		}
	}
}
