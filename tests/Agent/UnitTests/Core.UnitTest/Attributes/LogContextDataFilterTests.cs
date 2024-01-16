// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework.Internal;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using NewRelic.Agent.Core.Configuration;
using NewRelic.SystemInterfaces.Web;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration.UnitTest;
using NUnit.Framework.Constraints;

namespace NewRelic.Agent.Core.Attributes.Tests
{
    [TestFixture]
    public class LogContextDataFilterTests
    {
        private IConfiguration _configuration;
        private IConfigurationService _configurationService;

        private configuration _localConfig;
        private ServerConfiguration _serverConfig;
        private RunTimeConfiguration _runTimeConfiguration;
        private SecurityPoliciesConfiguration _securityPoliciesConfiguration;

        private IEnvironment _environment;
        private IHttpRuntimeStatic _httpRuntimeStatic;
        private IProcessStatic _processStatic;
        private IConfigurationManagerStatic _configurationManagerStatic;
        private IDnsStatic _dnsStatic;

        private Dictionary<string, object> _unfilteredContextData =
            new Dictionary<string, object>()
            {
                { "key1", "value1" },
                { "key2", 1 }
            };

        [SetUp]
        public void SetUp()
        {
            _environment = Mock.Create<IEnvironment>();

            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>()))
                .Returns(null as string);

            _processStatic = Mock.Create<IProcessStatic>();
            _httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
            _configurationManagerStatic = new ConfigurationManagerStaticMock();
            _dnsStatic = Mock.Create<IDnsStatic>();
            _securityPoliciesConfiguration = new SecurityPoliciesConfiguration();

            _runTimeConfiguration = new RunTimeConfiguration();
            _serverConfig = new ServerConfiguration();
            _localConfig = new configuration();

            _configurationService = Mock.Create<IConfigurationService>();

            UpdateConfig();
        }

        //        Inlcudes     Excludes     Expected
        [TestCase("",          "",          "key1,key2", TestName = "Empty include and exclude")]
        [TestCase("key1",      "",          "key1",      TestName = "Explicit include, empty exclude")]
        [TestCase("key1,key2", "key2",      "key1",      TestName = "Explicit include and exclude")]
        [TestCase("",          "key1",      "key2",      TestName = "Empty include, explicit exclude")]
        [TestCase("",          "key1,key2", "",          TestName = "Explicitly exclude all")]
        [TestCase("key*",      "",          "key1,key2", TestName = "Wildcard include, empty exclude")]
        [TestCase("",          "key*",      "",          TestName = "Empty include, wildcard exclude")]
        [TestCase("key1",      "key*",      "key1",      TestName = "More-specific explicit include overrides wildcard exclude")]
        [TestCase("key3",      "",          "",          TestName = "Explicit include of non-existent key")]
        [TestCase("",          "key3",      "key1,key2", TestName = "Explicit exclude of non-existent key")]
        [TestCase("Key1,Key2", "",          "",          TestName = "Includes are case-sensitive")]
        [TestCase("",          "Key1,Key2", "key1,key2", TestName = "Excludes are case-sensitive")]
        [TestCase("*",         "",          "key1,key2", TestName = "Include everything with wildcard")]
        [TestCase("",          "*",         "",          TestName = "Exclude everything with wildcard")]
        [TestCase("*",         "*",         "",          TestName = "Exclude wins over include")]
        public void FilterLogContextData(string includeList, string excludeList, string expectedAttributeNames)
        {
            _localConfig.applicationLogging.forwarding.contextData.include = includeList;
            _localConfig.applicationLogging.forwarding.contextData.exclude = excludeList;
            UpdateConfig();

            var filter = new LogContextDataFilter(_configurationService);
            var filteredData = filter.FilterLogContextData(_unfilteredContextData);

            ClassicAssert.AreEqual(expectedAttributeNames, string.Join(",", filteredData.Keys.ToList()));
        }

        [Test]
        public void HandlesConfigurationUpdates()
        {
            // Configure once

            _localConfig.applicationLogging.forwarding.contextData.include = "key1,key2";
            _localConfig.applicationLogging.forwarding.contextData.exclude = "";
            UpdateConfig();

            var filter = new LogContextDataFilter(_configurationService);
            var filteredData = filter.FilterLogContextData(_unfilteredContextData);

            ClassicAssert.AreEqual("key1,key2", string.Join(",", filteredData.Keys.ToList()));

            // Update config

            _localConfig.applicationLogging.forwarding.contextData.include = "";
            _localConfig.applicationLogging.forwarding.contextData.exclude = "key1,key2";
            UpdateConfig();

            filteredData = filter.FilterLogContextData(_unfilteredContextData);
            ClassicAssert.AreEqual("", string.Join(",", filteredData.Keys.ToList()));
        }

        [Test]
        public void MaxClusionCacheSizeExceededLogsWarning()
        {
            // Create context data with >1000 unique key names
            var unfilteredContextData = new Dictionary<string, object>();
            for (var i = 1; i <=1001; i++)
            {
                unfilteredContextData[$"key{i}"] = $"value{i}";
            }

            using (var logging = new TestUtilities.Logging())
            {
                var filter = new LogContextDataFilter(_configurationService);

                var filteredData = filter.FilterLogContextData(unfilteredContextData);

                ClassicAssert.IsTrue(logging.HasMessageThatContains("LogContextDataFilter: max #"));
            }

        }

        [TestCase("abc", true, "abc", false, 3)]
        [TestCase("ab*", true, "ab", true, 2)]
        [TestCase("creditCard", false, "creditCard", false, 10)]
        [TestCase("*", false, "", true, 0)]
        public void LogContextDataFilterRule(string inputRuleText, bool isInclude, string expectedRuleText, bool isWildcard, int specificity)
        {
            var rule = new LogContextDataFilterRule(inputRuleText, isInclude);
            ClassicAssert.AreEqual(isInclude, rule.Include);
            ClassicAssert.AreEqual(expectedRuleText, rule.Text);
            ClassicAssert.AreEqual(isWildcard, rule.IsWildCard);
            ClassicAssert.AreEqual(specificity, rule.Specificity);

        }
        private void UpdateConfig()
        {
            _configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfiguration, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local));
        }

    }
}
