// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Telerik.JustMock;
using JsonSerializer = NewRelic.Agent.Core.DataTransport.JsonSerializer;

namespace CompositeTests.CrossAgentTests.SecurityPolicies
{
    [TestFixture]
    public class SecurityPoliciesCrossAgentTests
    {
        private ICollectorWire _collectorWire;
        private static CompositeTestAgent _compositeTestAgent;
        private string _connectRawData;
        private ConnectionHandler _connectionHandler;
        private bool _receivedSecurityPoliciesException;

        public static List<TestCaseData> SecurityPoliciesTestDatas => GetSecurityPoliciesTestData();

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent();
            var environment = _compositeTestAgent.Container.Resolve<IEnvironment>();
            var collectorWireFactory = Mock.Create<ICollectorWireFactory>();
            _collectorWire = Mock.Create<ICollectorWire>();
            var systemInfo = Mock.Create<ISystemInfo>();
            var processStatic = Mock.Create<IProcessStatic>();
            var configurationService = Mock.Create<IConfigurationService>();
            var agentEnvironment = new NewRelic.Agent.Core.Environment(systemInfo, processStatic, configurationService);
            var agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            Mock.Arrange(() => collectorWireFactory.GetCollectorWire(null, Arg.IsAny<IAgentHealthReporter>())).IgnoreArguments().Returns(_collectorWire);
            Mock.Arrange(() => environment.GetEnvironmentVariable("NEW_RELIC_SECURITY_POLICIES_TOKEN")).Returns("ffff-fbff-ffff-ffff");

            _connectRawData = string.Empty;
            _receivedSecurityPoliciesException = false;

            _connectionHandler = new ConnectionHandler(new JsonSerializer(), collectorWireFactory, Mock.Create<IProcessStatic>(), Mock.Create<IDnsStatic>(),
                Mock.Create<ILabelsService>(), agentEnvironment, systemInfo, agentHealthReporter, Mock.Create<IEnvironment>());
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [TestCaseSource(nameof(SecurityPoliciesTestDatas))]
        public async Task SecurityPolicies_CrossAgentTests(SecurityPoliciesTestData testData)
        {
            InitializeStartingPolicySettings(testData);
            InitializePreconnectResponse(testData);
            InitializeConnectResponse();

            await RunConnectProcessAsync();

            ValidateShutdownSignal(testData);
            ValidatePoliciesSentToConnect(testData);
            ValidateEndingPolicies(testData);
        }

        private static List<TestCaseData> GetSecurityPoliciesTestData()
        {
            var testCaseDatas = new List<TestCaseData>();

            var dllPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "SecurityPolicies", "security_policies.json");
            var jsonString = File.ReadAllText(jsonPath);
            var testList = JsonConvert.DeserializeObject<List<SecurityPoliciesTestData>>(jsonString);

            // We want to filter out the tests that don't apply to our agent.
            var filteredTestList = testList.Where(td => !td.RequiredFeatures.Any(f => f.Equals("message_parameters") || f.Equals("job_arguments")));
            foreach (var testData in filteredTestList)
            {
                var testCase = new TestCaseData(new object[] { testData });
                testCase.SetName("SecurityPoliciesCrossAgentTests: " + testData.Name);
                testCaseDatas.Add(testCase);
            }

            return testCaseDatas;
        }

        private static void InitializeStartingPolicySettings(SecurityPoliciesTestData testData)
        {
            if (testData.StartingPolicySettings.AllowRawExceptionMessages != null)
            {
                _compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = !testData.StartingPolicySettings.AllowRawExceptionMessages.Enabled;
            }

            if (testData.StartingPolicySettings.AttributesInclude != null)
            {
                _compositeTestAgent.LocalConfiguration.attributes.enabled = true;
                _compositeTestAgent.LocalConfiguration.attributes.include = new List<string> { "attribute1", "attribute2" };
            }

            if (testData.StartingPolicySettings.CustomEvents != null)
            {
                _compositeTestAgent.LocalConfiguration.customEvents.enabled = testData.StartingPolicySettings.CustomEvents.Enabled;
            }

            if (testData.StartingPolicySettings.CustomInstrumentationEditor != null)
            {
                _compositeTestAgent.LocalConfiguration.customInstrumentationEditor.enabled = testData.StartingPolicySettings.CustomInstrumentationEditor.Enabled;
            }

            if (testData.StartingPolicySettings.CustomParameters != null)
            {
                _compositeTestAgent.LocalConfiguration.customParameters.enabled = testData.StartingPolicySettings.CustomParameters.Enabled;
            }

            if (testData.StartingPolicySettings.RecordSql != null)
            {
                _compositeTestAgent.LocalConfiguration.transactionTracer.recordSql =
                    testData.StartingPolicySettings.RecordSql.Enabled ? configurationTransactionTracerRecordSql.obfuscated : configurationTransactionTracerRecordSql.off;
            }

            _compositeTestAgent.PushConfiguration();
        }

        private void InitializePreconnectResponse(SecurityPoliciesTestData testData)
        {
            var securityPolicies = JsonConvert.SerializeObject(testData.SecurityPolicies);

            Mock.Arrange(() => _collectorWire.SendDataAsync("preconnect", Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>(), Arg.IsAny<Guid>()))
                .ReturnsAsync("{'return_value': { 'redirect_host': '', 'security_policies': " + securityPolicies + "}}");
        }

        private void InitializeConnectResponse()
        {
            var jsonString = JsonConvert.SerializeObject(_compositeTestAgent.ServerConfiguration);
            Mock.Arrange(() => _collectorWire.SendDataAsync("connect", Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>(), Arg.IsAny<Guid>()))
                .ReturnsAsync((Func<string, ConnectionInfo, string, string>)((method, connectionInfo, serializedData) =>
                {
                    _connectRawData = serializedData;
                    return $"{{'return_value': {jsonString} }}";
                }));
        }

        private async Task RunConnectProcessAsync()
        {
            try
            {
                await _connectionHandler.ConnectAsync();
            }
            catch (SecurityPoliciesValidationException)
            {
                _receivedSecurityPoliciesException = true;
            }
        }

        private void ValidateShutdownSignal(SecurityPoliciesTestData testData)
        {
            Assert.AreEqual(testData.ShouldShutdown, _receivedSecurityPoliciesException);
        }

        private void ValidatePoliciesSentToConnect(SecurityPoliciesTestData testData)
        {
            if (testData.ShouldShutdown)
            {
                return;
            }

            var securityPoliciesSentToConnectApi = JArray.Parse(_connectRawData)[0]["security_policies"];
            Assert.NotNull(securityPoliciesSentToConnectApi);

            ValidatePoliciesNotInConnect(testData.PoliciesToValidateNotSentToConnect, securityPoliciesSentToConnectApi);

            ValidateExpectedConnectPolicies(testData.ExpectedConnectPolicies, securityPoliciesSentToConnectApi);
        }

        private static void ValidatePoliciesNotInConnect(IEnumerable<string> excludedConnectPolicies, JToken securityPoliciesSentToConnectApi)
        {
            var sentPolicyNames = securityPoliciesSentToConnectApi.Children<JProperty>().Select(y => y.Name).ToList();
            foreach (var unexpectedPolicy in excludedConnectPolicies)
            {
                var foundPolicyThatShouldBeExcluded = sentPolicyNames.Contains(unexpectedPolicy);
                Assert.IsFalse(foundPolicyThatShouldBeExcluded, $"Found a policy that should be excluded in the list sent to connect: {unexpectedPolicy}");
            }
        }

        private static void ValidateExpectedConnectPolicies(PolicySettings expectedConnectPolicies, JToken securityPoliciesSentToConnectApi)
        {
            if (expectedConnectPolicies.AllowRawExceptionMessages != null)
            {
                Assert.AreEqual(expectedConnectPolicies.AllowRawExceptionMessages.Enabled, securityPoliciesSentToConnectApi["allow_raw_exception_messages"]["enabled"].Value<bool>());
            }

            if (expectedConnectPolicies.AttributesInclude != null)
            {
                Assert.AreEqual(expectedConnectPolicies.AttributesInclude.Enabled, securityPoliciesSentToConnectApi["attributes_include"]["enabled"].Value<bool>());
            }

            if (expectedConnectPolicies.CustomEvents != null)
            {
                Assert.AreEqual(expectedConnectPolicies.CustomEvents.Enabled, securityPoliciesSentToConnectApi["custom_events"]["enabled"].Value<bool>());
            }

            if (expectedConnectPolicies.CustomInstrumentationEditor != null)
            {
                Assert.AreEqual(expectedConnectPolicies.CustomInstrumentationEditor.Enabled, securityPoliciesSentToConnectApi["custom_instrumentation_editor"]["enabled"].Value<bool>());
            }

            if (expectedConnectPolicies.CustomParameters != null)
            {
                Assert.AreEqual(expectedConnectPolicies.CustomParameters.Enabled, securityPoliciesSentToConnectApi["custom_parameters"]["enabled"].Value<bool>());
            }

            if (expectedConnectPolicies.RecordSql != null)
            {
                Assert.AreEqual(expectedConnectPolicies.RecordSql.Enabled, securityPoliciesSentToConnectApi["record_sql"]["enabled"].Value<bool>());
            }
        }

        private static void ValidateEndingPolicies(SecurityPoliciesTestData testData)
        {
            if (testData.ShouldShutdown)
            {
                return;
            }

            var configurationService = _compositeTestAgent.Container.Resolve<IConfigurationService>();
            var config = configurationService.Configuration;

            VerifyPoliciesMatch(testData.EndingPolicySettings.AllowRawExceptionMessages, !config.StripExceptionMessages);
            VerifyPoliciesMatch(testData.EndingPolicySettings.CustomEvents, config.CustomEventsEnabled);
            VerifyPoliciesMatch(testData.EndingPolicySettings.CustomInstrumentationEditor, config.CustomInstrumentationEditorEnabled);
            VerifyPoliciesMatch(testData.EndingPolicySettings.CustomParameters, config.CaptureCustomParameters);

            if (testData.EndingPolicySettings.AttributesInclude != null)
            {
                if (testData.EndingPolicySettings.AttributesInclude.Enabled)
                {
                    Assert.IsNotEmpty(config.CaptureAttributesIncludes);
                }
                else
                {
                    Assert.IsEmpty(config.CaptureAttributesIncludes);
                }
            }

            if (testData.EndingPolicySettings.RecordSql != null)
            {
                var expectedRecordSqlSetting = testData.EndingPolicySettings.RecordSql.Enabled ? DefaultConfiguration.ObfuscatedStringValue : DefaultConfiguration.OffStringValue;
                Assert.AreEqual(expectedRecordSqlSetting, config.TransactionTracerRecordSql);
            }
        }

        private static void VerifyPoliciesMatch(SecurityPolicyState expectedPolicyState, bool receivedEnabledState)
        {
            if (expectedPolicyState != null)
            {
                Assert.AreEqual(expectedPolicyState.Enabled, receivedEnabledState);
            }
        }

        #region SecurityPolicies Test Data Classes

        public class SecurityPoliciesTestData
        {
            public string Name { get; set; }
            [JsonProperty("required_features")]
            public string[] RequiredFeatures { get; set; }
            [JsonProperty("starting_policy_settings")]
            public PolicySettings StartingPolicySettings { get; set; }
            [JsonProperty("security_policies")]
            public SecurityPolicies SecurityPolicies { get; set; }
            [JsonProperty("expected_connect_policies")]
            public PolicySettings ExpectedConnectPolicies { get; set; }
            [JsonProperty("validate_policies_not_in_connect")]
            public string[] PoliciesToValidateNotSentToConnect { get; set; }
            [JsonProperty("ending_policy_settings")]
            public PolicySettings EndingPolicySettings { get; set; }
            [JsonProperty("should_log")]
            public bool ShouldLog { get; set; }
            [JsonProperty("should_shutdown")]
            public bool ShouldShutdown { get; set; }
        }

        public class PolicySettings
        {
            [JsonProperty("record_sql")]
            public SecurityPolicyState RecordSql { get; set; }

            [JsonProperty("attributes_include")]
            public SecurityPolicyState AttributesInclude { get; set; }

            [JsonProperty("allow_raw_exception_messages")]
            public SecurityPolicyState AllowRawExceptionMessages { get; set; }

            [JsonProperty("custom_events")]
            public SecurityPolicyState CustomEvents { get; set; }

            [JsonProperty("custom_parameters")]
            public SecurityPolicyState CustomParameters { get; set; }

            [JsonProperty("custom_instrumentation_editor")]
            public SecurityPolicyState CustomInstrumentationEditor { get; set; }

            [JsonProperty("message_parameters")]
            public SecurityPolicyState MessageParameters { get; set; }

            [JsonProperty("job_arguments")]
            public SecurityPolicyState JobArguments { get; set; }
        }

        public class SecurityPolicies
        {
            [JsonProperty("record_sql", NullValueHandling = NullValueHandling.Ignore)]
            public SecurityPolicyState RecordSql { get; set; }

            [JsonProperty("attributes_include", NullValueHandling = NullValueHandling.Ignore)]
            public SecurityPolicyState AttributesInclude { get; set; }

            [JsonProperty("allow_raw_exception_messages", NullValueHandling = NullValueHandling.Ignore)]
            public SecurityPolicyState AllowRawExceptionMessages { get; set; }

            [JsonProperty("custom_events", NullValueHandling = NullValueHandling.Ignore)]
            public SecurityPolicyState CustomEvents { get; set; }

            [JsonProperty("custom_parameters", NullValueHandling = NullValueHandling.Ignore)]
            public SecurityPolicyState CustomParameters { get; set; }

            [JsonProperty("custom_instrumentation_editor", NullValueHandling = NullValueHandling.Ignore)]
            public SecurityPolicyState CustomInstrumentationEditor { get; set; }

            [JsonProperty("message_parameters", NullValueHandling = NullValueHandling.Ignore)]
            public SecurityPolicyState MessageParameters { get; set; }

            [JsonProperty("job_arguments", NullValueHandling = NullValueHandling.Ignore)]
            public SecurityPolicyState JobArguments { get; set; }

            /// <summary>
            /// Any policy that doesn't match one of the "known" policies defined via a JsonProperty attribute will be placed
            /// into this UnexpectedPolicies dictionary. The JsonExtensionData attribute automatically facilitates this behavior
            /// during deserialization. We can use this property to validate that we did not send "unknown" policies to the call
            /// to connect. This also allows us to re-serialize this object so that it can be used as part of the response from
            /// the call to preconnect.
            /// </summary>
            [JsonExtensionData]
            public IDictionary<string, JToken> UnexpectedPolicies { get; set; }
        }

        #endregion
    }
}
