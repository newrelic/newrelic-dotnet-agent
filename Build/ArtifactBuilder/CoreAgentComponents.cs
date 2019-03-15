using System.Collections.Generic;

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
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Core.dll",
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Parsing.dll"
			};

			var storageProviders = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Providers.Storage.AsyncLocal.dll",
			};

			var wrapperProviders = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.AspNetCore.dll",
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.CustomInstrumentation.dll",
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.CustomInstrumentationAsync.dll",
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.HttpClient.dll",
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.MongoDb26.dll",
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.Sql.dll",
				$@"{SourceHomeBuilderPath}\Extensions\netstandard2.0\NewRelic.Providers.Wrapper.SqlAsync.dll"
			};

			var wrapperXmls = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.SqlAsync.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml",
			};

			ExtensionXsd = $@"{SourceHomeBuilderPath}\Extensions\extension.xsd";
			NewRelicXsd = $@"{SourceHomeBuilderPath}\newrelic.xsd";
			NewRelicConfig = $@"{SourceHomeBuilderPath}\newrelic.config";

			var root = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\License.txt",
				$@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.dll",
				$@"{SourceHomeBuilderPath}\NewRelic.Agent.Extensions.dll",
				$@"{SourceHomeBuilderPath}\NewRelic.Api.Agent.dll",
				NewRelicConfig,
				$@"{SourceHomeBuilderPath}\NewRelic.Profiler.dll",
				NewRelicXsd,
				$@"{SourceHomeBuilderPath}\README.md",
			};

			ExtensionDirectoryComponents = new List<string>();
			ExtensionDirectoryComponents.Add(ExtensionXsd);

			NetstandardExtensionDirectoryComponents = new List<string>();
			NetstandardExtensionDirectoryComponents.AddRange(agentDllsForExtensionDirectory);
			NetstandardExtensionDirectoryComponents.AddRange(storageProviders);
			NetstandardExtensionDirectoryComponents.AddRange(wrapperProviders);
 			
			// This empty list is necessary to prevent a null reference exception
			Net46ExtensionDirectoryComponents = new List<string>();

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
