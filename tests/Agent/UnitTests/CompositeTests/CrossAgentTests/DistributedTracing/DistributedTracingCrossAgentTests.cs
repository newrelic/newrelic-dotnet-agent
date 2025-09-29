// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using Telerik.JustMock;

namespace CompositeTests.CrossAgentTests.DistributedTracing
{
    [TestFixture]
    public class DistributedTracingCrossAgentTests
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;
        private static ISampler _sampler;

        private static List<TestCaseData> DistributedTracingTestCaseData => GetDistributedTracingTestData();

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
            _sampler = Mock.Create<ISampler>(Behavior.CallOriginal);
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [TestCaseSource(nameof(DistributedTracingTestCaseData))]
        public void DistributedTracing_CrossAgentTests(DistributedTracingTestData testData)
        {
            InitializeSettings(testData);

            MakeTransaction(testData);

            _compositeTestAgent.Harvest();

            ValidateIntrinsics(testData);

            ValidateMetrics(testData);
        }

        private static List<TestCaseData> GetDistributedTracingTestData()
        {
            var testCaseData = new List<TestCaseData>();

            string location = Assembly.GetExecutingAssembly().GetLocation();
            var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
            var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "DistributedTracing", "distributed_tracing.json");
            var jsonString = File.ReadAllText(jsonPath);

            var settings = new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                }
            };
            var testList = JsonConvert.DeserializeObject<List<DistributedTracingTestData>>(jsonString, settings);

            foreach (var testData in testList)
            {
                var testCase = new TestCaseData(testData);
                testCase.SetName("DistributedTracingCrossAgentTests: " + testData.Name);
                testCaseData.Add(testCase);
            }

            return testCaseData;
        }

        private static void InitializeSettings(DistributedTracingTestData testData)
        {
            if (testData.ForceSampledTrue)
            {
                var priority = 1.0f;
                Mock.Arrange(() => _sampler.ShouldSample(Arg.IsAny<ISamplingParameters>()))
                    .Returns(new SamplingResult(true, priority));
            }

            _compositeTestAgent.LocalConfiguration.spanEvents.enabled = testData.SpanEventsEnabled;
            var defaultTransactionEventsEnabled = _compositeTestAgent.LocalConfiguration.transactionEvents.enabled;
            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = testData.TrustedAccountKey;
            _compositeTestAgent.ServerConfiguration.AccountId = testData.AccountId;
            _compositeTestAgent.ServerConfiguration.PrimaryApplicationId = "primaryApplicationId";

            _compositeTestAgent.PushConfiguration();
        }

        void MakeTransaction(DistributedTracingTestData testData)
        {
            var testDataInboundHeaders = MakeHeaders(testData);

            var transaction = _agent.CreateTransaction(
                isWeb: testData.WebTransaction,
                category: testData.WebTransaction ? "Action" : "Other",
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            AcceptPayloads(testDataInboundHeaders, testData);

            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");

            if (testData.RaisesException)
            {
                transaction.NoticeError(new Exception("This is a new exception."));
            }

            Dictionary<string, string> insertedHeaders = new Dictionary<string, string>();
            var setHeaders = new Action<Dictionary<string, string>, string, string>((carrier, key, value) =>
            {
                carrier.Add(key, value);
            });

            testData.OutboundPayloadsSettings?.ToList().ForEach(payloadSettings =>
            {
                _agent.CurrentTransaction.InsertDistributedTraceHeaders(insertedHeaders, setHeaders);

                if (testData.OutboundPayloadsSettings != null)
                {
                    ValidateOutboundHeaders(payloadSettings, insertedHeaders, testData.TrustedAccountKey);
                }

                insertedHeaders.Clear();
            });

            segment.End();
            transaction.End();
        }

        List<IEnumerable<KeyValuePair<string, string>>> MakeHeaders(DistributedTracingTestData testData)
        {
            List<IEnumerable<KeyValuePair<string, string>>> payloads = new List<IEnumerable<KeyValuePair<string, string>>>();

            if (testData.InboundPayloads == null)
            {
                payloads.Add(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Newrelic", null) });
                return payloads;
            }

            testData.InboundPayloads?.ToList().ForEach
                (payloadSetting =>
                {
                    string payload = JsonConvert.SerializeObject(payloadSetting.Payload);

                    payloads.Add(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Newrelic", payload) });
                });

            return payloads;
        }

        private void AcceptPayloads(List<IEnumerable<KeyValuePair<string, string>>> testDataInboundHeaders, DistributedTracingTestData testData)
        {
            var isValidEnumValue = Enum.TryParse(testData.TransportType, ignoreCase: false, result: out TransportType transportType);
            if (!isValidEnumValue)
            {
                transportType = (TransportType)(-1);
            }

            foreach (var headers in testDataInboundHeaders)
            {
                _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, transportType);
            }

            IEnumerable<string> GetHeaderValue(IEnumerable<KeyValuePair<string, string>> carrier, string key)
            {
                List<string> results = new List<string>();
                foreach (KeyValuePair<string, string> header in carrier)
                {
                    if (header.Key == key)
                        results.Add(header.Value);
                }

                return results.Any() ? results : null;
            }
        }

        private void ValidateOutboundHeaders(OutboundPayloadSettings payloadSettings, Dictionary<string, string> actualOutboundHeaders, string trustedAccountKey)
        {
            JObject newrelicHeaderValue = null;

            if (actualOutboundHeaders.ContainsKey("newrelic"))
            {
                newrelicHeaderValue = JObject.Parse(Strings.Base64Decode(actualOutboundHeaders["newrelic"]));
            }

            JToken actualValue = null;

            var exactFields = payloadSettings.Exact;
            if (exactFields != null)
            {
                foreach (var key in exactFields.Keys)
                {
                    var expectedValue = exactFields[key];
                    actualValue = newrelicHeaderValue.SelectToken(key);

                    Assert.That(actualValue.IsEqualTo(expectedValue), $"{key}, expected: {expectedValue}, actual: {actualValue}");
                }
            }

            payloadSettings.Expected?.ToList().ForEach(expected =>
            {
                Assert.That(newrelicHeaderValue.SelectToken(expected), Is.Not.Null, $"Missing expected: {expected}");
            });

            payloadSettings.Unexpected?.ToList().ForEach(unexpected =>
            {
                Assert.That(newrelicHeaderValue.SelectToken(unexpected), Is.Null, $"Unexpected exists: {unexpected}");
            });
        }

        private void ValidateIntrinsics(DistributedTracingTestData testData)
        {
            if (testData.IntrinsicSettings != null)
            {
                foreach (var eventType in testData.IntrinsicSettings?.TargetEvents)
                {
                    switch (eventType)
                    {
                        case "Transaction":
                            var transactionEvent = _compositeTestAgent.TransactionEvents?.First();
                            var txAttrs = testData.IntrinsicSettings.Events != null ?
                                    testData.IntrinsicSettings.Events.ContainsKey("Transaction") ?
                                        testData.IntrinsicSettings.Events?["Transaction"] :
                                        null : null;
                            ValidateAttributes(transactionEvent.IntrinsicAttributes(), testData, txAttrs);
                            break;
                        case "Span":
                            Assert.That(_compositeTestAgent.SpanEvents, Has.Count.EqualTo(2));
                            var spanEvent = _compositeTestAgent.SpanEvents.First();
                            ValidateAttributes(spanEvent.IntrinsicAttributes(), testData, testData.IntrinsicSettings.Events?["Span"]);
                            break;
                        case "Error":
                            var errorEvent = _compositeTestAgent.ErrorEvents.First();
                            ValidateAttributes(errorEvent.IntrinsicAttributes(), testData, testData.IntrinsicSettings.Events?["Error"]);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void ValidateAttributes(IDictionary<string, object> actualAttributes, DistributedTracingTestData testData, JToken eventSpecificAttributes = null)
        {
            // Common (for all target_events)
            ValidateAttributeSettings(testData.IntrinsicSettings.CommonAttributes, actualAttributes);

            // event-specific attrs
            ValidateAttributeSettings(eventSpecificAttributes?.ToObject<AttributesSettings>(), actualAttributes);
        }

        private void ValidateAttributeSettings(AttributesSettings testDataAttributesSettings, IDictionary<string, object> actualEventAttributes)
        {
            testDataAttributesSettings?.Exact?.Keys.ToList().ForEach(attr =>
            {
                Assert.That(actualEventAttributes.ContainsKey(attr), $"Exact attribute not present: {attr}");
                testDataAttributesSettings.Exact.TryGetValue(attr, out var expectedValue);

                var attrValue = actualEventAttributes[attr];
                var attrType = attrValue.GetType();
                var typedExpectedValue = Convert.ChangeType(expectedValue, attrType);

                Assert.That(AttributeComparer.IsEqualTo(typedExpectedValue, attrValue), $"{attr}, expected: {typedExpectedValue}, actual: {attrValue}");
            });

            testDataAttributesSettings?.Expected?.ToList().ForEach(attr =>
            {
                Assert.That(actualEventAttributes.ContainsKey(attr), $"{attr}");
            });

            testDataAttributesSettings?.Unexpected?.ToList().ForEach(attr =>
            {
                Assert.That(!actualEventAttributes.ContainsKey(attr), $"{attr}");
            });
        }

        private void ValidateMetrics(DistributedTracingTestData testData)
        {
            var expectedMetrics = new List<ExpectedMetric>();

            // convert json ExpectedMetrics for CompositeTests.MetricAssertions
            foreach (var metric in testData.ExpectedMetrics)
            {
                expectedMetrics.Add(new ExpectedTimeMetric() { Name = metric.Name, CallCount = metric.Count });
            }

            MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
        }

        #region Distributed Tracing Test Data Classes

        public class DistributedTracingTestData
        {
            [JsonProperty("test_name")]
            public string Name { get; set; }

            [JsonProperty("trusted_account_key", NullValueHandling = NullValueHandling.Ignore)]
            public string TrustedAccountKey { get; set; }

            [JsonProperty("account_id")]
            public string AccountId { get; set; }

            [JsonProperty("web_transaction")]
            public bool WebTransaction { get; set; }

            [JsonProperty("raises_exception")]
            public bool RaisesException { get; set; }

            [JsonProperty("force_sampled_true")]
            public bool ForceSampledTrue { get; set; }

            [JsonProperty("span_events_enabled")]
            public bool SpanEventsEnabled { get; set; }

            [JsonProperty("major_version")]
            public int MajorVersion { get; set; }

            [JsonProperty("minor_version")]
            public int MinorVersion { get; set; }

            [JsonProperty("transport_type")]
            public string TransportType { get; set; }

            [JsonProperty("inbound_payloads")]
            public InboundPayloadSettings[] InboundPayloads { get; set; }

            [JsonProperty("outbound_payloads", NullValueHandling = NullValueHandling.Ignore)]
            public OutboundPayloadSettings[] OutboundPayloadsSettings { get; set; }

            [JsonProperty("intrinsics")]
            public IntrinsicsSettings IntrinsicSettings { get; set; }

            [JsonProperty("expected_metrics")]
            public Metric[] ExpectedMetrics { get; set; }
        }

        public class InboundPayloadSettings
        {
            [JsonExtensionData]
            public IDictionary<string, JToken> Payload { get; set; }
        }

        [JsonObject]
        public class OutboundPayloadSettings
        {
            [JsonProperty("exact")]
            public IDictionary<string, JToken> Exact { get; set; }

            [JsonProperty("expected", NullValueHandling = NullValueHandling.Ignore)]
            public string[] Expected { get; set; }

            [JsonProperty("unexpected", NullValueHandling = NullValueHandling.Ignore)]
            public string[] Unexpected { get; set; }
        }

        public class IntrinsicsSettings
        {
            [JsonProperty("target_events")]
            public string[] TargetEvents { get; set; }

            [JsonProperty("common", NullValueHandling = NullValueHandling.Ignore)]
            public AttributesSettings CommonAttributes { get; set; }

            [JsonExtensionData]
            public IDictionary<string, JToken> Events { get; set; }
        }

        public class AttributesSettings
        {
            [JsonProperty("exact")]
            public Dictionary<string, object> Exact { get; set; }

            [JsonProperty("expected", NullValueHandling = NullValueHandling.Ignore)]
            public string[] Expected { get; set; }

            [JsonProperty("unexpected", NullValueHandling = NullValueHandling.Ignore)]
            public string[] Unexpected { get; set; }
        }

        [JsonConverter(typeof(MetricJsonConverter))]
        public class Metric
        {
            public string Name;
            public int Count;
        }

        public class MetricJsonConverter : JsonConverter
        {
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.StartArray)
                {
                    JArray metric = JArray.Load(reader);

                    if (metric != null)
                    {
                        var name = (string)metric[0];
                        var count = (int)metric[1];
                        return new Metric { Name = name, Count = count };
                    }
                }
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Metric);
            }
            public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
            {
                throw new System.NotImplementedException();
            }
        }

        #endregion
    }
}
