using System.Collections.Generic;

namespace ArtifactBuilder
{
	public class FrameworkAgentComponents : AgentComponents
	{
		public FrameworkAgentComponents(string configuration, string platform, string sourcePath)
			: base(configuration, platform, sourcePath) { }

		protected override string SourceHomeBuilderPath => $@"{SourcePath}\New Relic Home {Platform}";

		protected override List<string> IgnoredHomeBuilderFiles => new List<string>() {
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Agent.AttributeFilter.dll",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Agent.Configuration.dll",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Agent.Core.dll",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Agent.LabelsService.dll",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Core.Instrumentation.xml",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Parsing.Instrumentation.xml",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.CustomInstrumentation.Instrumentation.xml",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WrapperUtilities.Instrumentation.xml",
			$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Trie.dll",
			$@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.pdb"
		};

		protected override void CreateAgentComponents()
		{
			var agentDllsForExtensionDirectory = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\JetBrains.Annotations.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Core.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Parsing.dll"
			};

			var transactionContextDlls = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.CallStack.AsyncLocal.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.TransactionContext.Asp.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.TransactionContext.Wcf3.dll"
			};

			var wrapperProviders = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Asp35.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Couchbase.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.CustomInstrumentation.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.CustomInstrumentationAsync.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpClient.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpWebRequest.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Msmq.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Mvc3.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.NServiceBus.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.OpenRasta.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Owin.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Owin3.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.RabbitMq.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.RestSharp.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Sql.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.SqlAsync.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Wcf3.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebApi1.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebApi2.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebOptimization.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebServices.dll",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WrapperUtilities.dll"
			};

			var wrapperXmls = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Couchbase.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.HttpWebRequest.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.MongoDb.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Msmq.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Mvc3.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.NServiceBus.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.OpenRasta.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Owin.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Owin3.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.RestSharp.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.ScriptHandlerFactory.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.ServiceStackRedis.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.SqlAsync.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.Wcf3.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebApi1.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebApi2.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebOptimization.Instrumentation.xml",
				$@"{SourceHomeBuilderPath}\Extensions\NewRelic.Providers.Wrapper.WebServices.Instrumentation.xml"
			};

			ExtensionXsd = $@"{SourceHomeBuilderPath}\Extensions\extension.xsd";
			NewRelicXsd = $@"{SourceHomeBuilderPath}\newrelic.xsd";
			NewRelicConfig = $@"{SourceHomeBuilderPath}\newrelic.config";
			
			var root = new List<string>()
			{
				$@"{SourceHomeBuilderPath}\NewRelic.Agent.Core.dll",
				$@"{SourceHomeBuilderPath}\NewRelic.Agent.Extensions.dll",
				NewRelicConfig,
				$@"{SourceHomeBuilderPath}\NewRelic.Profiler.dll",
				NewRelicXsd
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

			AgentApiDll = $@"{SourcePath}\_build\AnyCPU-{Configuration}\NewRelic.Api.Agent\net35\NewRelic.Api.Agent.dll";
		}
	}
}
