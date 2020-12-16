// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries
{
    [Library]
    public class ConfigBuilderDeadlock
    {
        [LibraryMethod]
        public void Run()
        {
            // This application has been conifgured in the app.config to use the MyConfigBuilder ConfigurationBuilder implementation.
            // ConfigurationBuilder was introduced in .NET Framework 4.7.1: https://docs.microsoft.com/en-us/dotnet/api/system.configuration.configurationbuilder

            // It is common for ConfigurationBuilders to make HTTP calls to retrieve configuration values from an external source.
            // The Azure Key Vault ConfigurationBuilder is an example.

            // This application validates that a deadlock does not occur when using a ConfigurationBuilder that makes an HTTP call.
            // Upon accessing AppSettings below, the initialization of MyConfigBuilder starts. It subsequently makes an HTTP call.
            // This call is instrumented by the agent, but it no longer causes the managed agent to initialize. This is because during
            // the managed agent initialization we also access AppSettings for things like 'NewRelic.AgentEnabled'. This second nested
            // attempt to read from AppSettings causes a deadlock if the managed agent initialization is started.

            SetupAppConfig();
            var value = ConfigurationManager.AppSettings["Key"];
            Console.WriteLine($"Key={value}");
        }

        [NewRelic.Api.Agent.Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DoTransaction()
        {
            // This method is invoked from the MyConfigBuilder to demonstrate that it's not just HTTP calls that can cause a deadlock.
            // The deadlock can potentially occur when any instrumented method gets invoked during agent initialization.
            // The NEW_RELIC_DELAY_AGENT_INIT_METHOD_LIST environment variable can be used to add additional methods for which we should delay initialization.
        }

        static void SetupAppConfig()
        {
            const string configBuilderConfig = @"
				<configSections>
					<section name=""configBuilders"" type=""System.Configuration.ConfigurationBuildersSection, System.Configuration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" restartOnExternalChanges=""false"" requirePermission=""false""/>
				</configSections>
				<configBuilders>
					<builders>
						<add name=""MyConfigBuilder"" type=""ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.MyConfigBuilder, ConsoleMultiFunctionApplicationFW"" />
					</builders>
				</configBuilders>
				<appSettings configBuilders=""MyConfigBuilder"">
					<add key=""Key"" value="""" />
			";

            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var filePath = config.FilePath;
            var contents = File.ReadAllText(filePath);
            contents = contents.Replace("<appSettings>", configBuilderConfig);
            File.WriteAllText(filePath, contents);

            ConfigurationManager.RefreshSection("configSections");
            ConfigurationManager.RefreshSection("configBuilders");
            ConfigurationManager.RefreshSection("appSettings");
        }
    }

    public class MyConfigBuilder : ConfigurationBuilder
    {
        private readonly IDictionary _configuration = new Dictionary<string, string>
        {
            { "Key", "Value" }
        };

        public override XmlNode ProcessRawXml(XmlNode rawXml)
        {
            Console.WriteLine("Start DoTransaction");
            Task.Run(() => ConfigBuilderDeadlock.DoTransaction()).Wait();
            Console.WriteLine("End DoTransaction");

            using (var client = new HttpClient())
            {
                Console.WriteLine("Starting HTTP request");
                var result = client.GetStringAsync("https://www.newrelic.com").Result;
                Console.WriteLine("HTTP request complete");
            }

            foreach (DictionaryEntry item in _configuration)
            {
                var pair = (Key: item.Key.ToString(), Value: item.Value.ToString());
                if (rawXml.HasChildNodes && rawXml.SelectSingleNode($"add[@key='{pair.Key}']") != null)
                {
                    rawXml.SelectSingleNode($"add[@key='{pair.Key}']").Attributes["value"].Value = pair.Value;
                }
            }

            return rawXml;
        }

        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            return base.ProcessConfigurationSection(configSection);
        }
    }
}
