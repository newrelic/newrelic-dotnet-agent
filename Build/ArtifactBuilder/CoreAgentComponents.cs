using System.Collections.Generic;

namespace ArtifactBuilder
{
	public class CoreAgentComponents : AgentComponents
	{
		public CoreAgentComponents(string configuration, string platform, string sourcePath)
			: base(configuration, platform, sourcePath) { }

		protected override void CreateAgentComponents()
		{
			var agentDllsForExtensionDirectory = new List<string>()
			{
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Agent.AttributeFilter.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Agent.Configuration.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Agent.Core.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Agent.LabelsService.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Core.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Parsing.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Trie.dll",
			};

			var storageProviders = new List<string>()
			{
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Providers.Storage.AsyncLocal.dll",
			};

			var wrapperProviders = new List<string>()
			{
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.AspNetCore.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.CustomInstrumentation.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.CustomInstrumentationAsync.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.HttpClient.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.Sql.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.SqlAsync.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.WrapperUtilities.dll",
			};

			var wrapperXmls = new List<string>()
			{
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\NewRelic.Providers.Wrapper.CustomInstrumentation.Instrumentation.xml",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\NewRelic.Providers.Wrapper.SqlAsync.Instrumentation.xml",
			};

			ExtensionXsd = $@"{SourcePath}\New Relic Home {Platform} CoreClr\Extensions\extension.xsd";
			NewRelicXsd = $@"{SourcePath}\New Relic Home {Platform} CoreClr\newrelic.xsd";
			NewRelicConfig = $@"{SourcePath}\New Relic Home {Platform} CoreClr\newrelic.config";

			var root = new List<string>()
			{
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\License.txt",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\NewRelic.Agent.Core.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\NewRelic.Agent.Core.pdb",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\NewRelic.Agent.Extensions.dll",
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\NewRelic.Api.Agent.dll",
				NewRelicConfig,
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\NewRelic.Profiler.dll",
				NewRelicXsd,
				$@"{SourcePath}\New Relic Home {Platform} CoreClr\README.md",
			};

			ExtensionDirectoryComponents = new List<string>();
			ExtensionDirectoryComponents.Add(ExtensionXsd);

			NetstandardExtensionDirectoryComponents = new List<string>();
			NetstandardExtensionDirectoryComponents.AddRange(agentDllsForExtensionDirectory);
			NetstandardExtensionDirectoryComponents.AddRange(storageProviders);
			NetstandardExtensionDirectoryComponents.AddRange(wrapperProviders);

			WrapperXmlFiles = new List<string>();
			WrapperXmlFiles.AddRange(wrapperXmls);

			RootInstallDirectoryComponents = new List<string>();
			RootInstallDirectoryComponents.AddRange(root);

			AgentApiDll = $@"{SourcePath}\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\netstandard2.0\NewRelic.Api.Agent.dll";

			LinuxProfiler = Platform == "x64" 
				? $@"{SourcePath}\New Relic Home x64 CoreClr_Linux\libNewRelicProfiler.so"
				: null;
		}
	}
}
