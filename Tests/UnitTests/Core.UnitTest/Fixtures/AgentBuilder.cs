using NewRelic.Agent.Core.Config;
using System;
using System.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Fixtures
{
	public class AgentBuilder
	{
	   
		public static AgentBuilder Agent() {
			return new AgentBuilder();
		}

		//Builds from internal means
		public static IAgent Build()
		{
			return Build(false);  
		}

		//Builds from internal means
		public static IAgent Build(bool shouldConnect)
		{
			return Mock.Create<IAgent>();
		}

		//Builds from external source
		private static configuration NewAgentConfig()
		{
			return NewAgentConfig(false);
		}

		private static configuration NewAgentConfig(bool connectStaging)
		{

			configuration config = new configuration();
			config.rootAgentEnabled = true;
			config.threadProfilingEnabled = false;
			config.log = new configurationLog();
			config.log.level = "DEBUG";
			config.application = new configurationApplication();
			config.application.name = new List<String> { "FryTests" };
			config.service = new configurationService();
			config.requestParameters = new configurationRequestParameters { enabled = false };
			config.transactionTracer = new configurationTransactionTracer { recordSql = configurationTransactionTracerRecordSql.obfuscated };

			if (connectStaging)
			{
				config.service.host = "staging-collector.newrelic.com";
				config.service.licenseKey = "b25fd3ca20fe323a9a7c4a092e48d62dc64cc61d";
			}
			else
			{
				config.service.licenseKey = "";
			}
			config.Initialize("<configuration/>", "AgentTestHardCoded");
			return config;
		}


	}
}
