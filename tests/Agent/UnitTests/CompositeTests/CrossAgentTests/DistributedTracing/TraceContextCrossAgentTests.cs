// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Telerik.JustMock;

namespace CompositeTests.CrossAgentTests.DistributedTracing
{
    [TestFixture]
    public class TraceContextCrossAgentTests
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;
        private static IAdaptiveSampler _adaptiveSampler;

        private static List<TestCaseData> TraceContextTestCaseData => GetTraceContextTestData();

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
            _adaptiveSampler = Mock.Create<IAdaptiveSampler>(Behavior.CallOriginal);
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [TestCaseSource(nameof(TraceContextTestCaseData))]
        public void TraceContext_CrossAgentTests(TraceContextTestData testData)
        {
            InitializeSettings(testData);

            MakeTransaction(testData);

            _compositeTestAgent.Harvest();

            ValidateIntrinsics(testData);

            ValidateMetrics(testData);
        }

        private static List<TestCaseData> GetTraceContextTestData()
        {
            var testCaseData = new List<TestCaseData>();

            string location = Assembly.GetExecutingAssembly().GetLocation();
            var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
            var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "DistributedTracing", "trace_context.json");
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
            var testList = JsonConvert.DeserializeObject<List<TraceContextTestData>>(jsonString, settings);

            foreach (var testData in testList)
            {
                var testCase = new TestCaseData(testData);
                testCase.SetName("TraceContextCrossAgentTests: " + testData.Name);
                testCaseData.Add(testCase);
            }

            return testCaseData;
        }

        private static void InitializeSettings(TraceContextTestData testData)
        {
            if (testData.ForceSampledTrue)
            {
                var priority = 1.0f;
                Mock.Arrange(() => _adaptiveSampler.ComputeSampled(ref priority)).IgnoreArguments().Returns(true);
            }

            _compositeTestAgent.LocalConfiguration.spanEvents.enabled = testData.SpanEventsEnabled;
            var defaultTransactionEventsEnabled = _compositeTestAgent.LocalConfiguration.transactionEvents.enabled;
            _compositeTestAgent.LocalConfiguration.transactionEvents.enabled =
                testData.TransactionEventsEnabled.HasValue ?
                    testData.TransactionEventsEnabled.Value :
                    defaultTransactionEventsEnabled;
            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = testData.TrustedAccountKey;
            _compositeTestAgent.ServerConfiguration.AccountId = testData.AccountId;
            _compositeTestAgent.ServerConfiguration.PrimaryApplicationId = "primaryApplicationId";

            _compositeTestAgent.PushConfiguration();
        }

        void MakeTransaction(TraceContextTestData testData)
        {
            var testDataInboundHeaderSets = MakeHeaders(testData);

            var transaction = _agent.CreateTransaction(
                isWeb: testData.WebTransaction,
                category: testData.WebTransaction ? "Action" : "Other",
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            testDataInboundHeaderSets?.ForEach
                (headerSet =>
                {
                    AcceptPayloads(headerSet, testData);
                });

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

        List<IEnumerable<KeyValuePair<string, string>>> MakeHeaders(TraceContextTestData testData)
        {
            List<IEnumerable<KeyValuePair<string, string>>> inboundHeaderSets = new List<IEnumerable<KeyValuePair<string, string>>>();

            testData.InboundHeaders?.ToList().ForEach
                (headerSet =>
                {
                    var ingestHeaderSet = new List<KeyValuePair<string, string>>();

                    headerSet.Headers?.ToList().ForEach(header =>
                    {

                        if (header.Key.Equals("newrelic"))
                        {
                            var settingPayload = header.Value.ToObject<string>();
                            ingestHeaderSet.Add(new KeyValuePair<string, string>(header.Key, settingPayload));
                        }
                        else
                        {
                            ingestHeaderSet.Add(new KeyValuePair<string, string>(header.Key, header.Value.ToString()));
                        }
                    });

                    inboundHeaderSets.Add(ingestHeaderSet);
                });

            if (inboundHeaderSets.Count == 0)
            {
                // test requires AcceptPayloads to be called even if there are no headers
                inboundHeaderSets.Add(new List<KeyValuePair<string, string>>());
            }

            return inboundHeaderSets;
        }

        private void AcceptPayloads(IEnumerable<KeyValuePair<string, string>> testDataInboundHeaders, TraceContextTestData testData)
        {
            var isValidEnumValue = Enum.TryParse(testData.TransportType, ignoreCase: false, result: out TransportType transportType);
            if (!isValidEnumValue)
            {
                transportType = (TransportType)(-1);
            }

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(testDataInboundHeaders, GetHeaderValue, transportType);

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
            JObject newrelicJson = null;
            JObject traceparentJson = null;
            JObject tracestateJson = null;

            if (actualOutboundHeaders.ContainsKey("newrelic"))
            {
                newrelicHeaderValue = JObject.Parse(Strings.Base64Decode(actualOutboundHeaders["newrelic"]));

                newrelicJson = new JObject
                {
                    {"newrelic", newrelicHeaderValue}
                };
            }
            if (actualOutboundHeaders.ContainsKey("traceparent"))
            {
                var fields = actualOutboundHeaders["traceparent"].Split('-');
                traceparentJson = new JObject
                {
                    {"traceparent", new JObject
                        {
                            {"version", fields[0] },
                            {"trace_id", fields[1] },
                            {"parent_id", fields[2] },
                            {"trace_flags", fields[3] }
                        }
                    }
                };
            }
            if (actualOutboundHeaders.ContainsKey("tracestate"))
            {
                var tracestate = W3CTracestate.GetW3CTracestateFromHeaders(new string[] { actualOutboundHeaders["tracestate"] }, trustedAccountKey);
                var headerValue = actualOutboundHeaders["tracestate"];
                var tenantId = headerValue.Substring(0, headerValue.IndexOf('@'));
                tracestateJson = new JObject
                {
                    {"tracestate", new JObject
                        {
                            {"version", tracestate.Version },
                            {"parent_type", (int)tracestate.ParentType },
                            {"parent_account_id", tracestate.AccountId },
                            {"parent_application_id", tracestate.AppId },
                            {"span_id", tracestate.SpanId },
                            {"transaction_id", tracestate.TransactionId },
                            {"sampled", tracestate.Sampled },
                            {"priority", string.Format("{0:0.######}", tracestate.Priority) }, // cheating here: priority is stored as float, which may show in scientific notation; this formatting is performed when creating a new tracestate header in the agent so it will not be transmitted in scientific notation
							{"timestamp", tracestate.Timestamp },
                            {"tenant_id",  tenantId },
                            {"vendors", new JArray(tracestate.VendorstateEntries.Select(vse => vse.Split('=')[0]).ToList()) }
                        }
                    }
                };
            }

            JToken actualValue = null;

            var exactFields = payloadSettings.Exact;
            if (exactFields != null)
            {
                foreach (var key in exactFields.Keys)
                {
                    var expectedValue = exactFields[key];

                    switch (key.Substring(0, key.IndexOf('.')))
                    {
                        case "newrelic":
                            actualValue = newrelicJson.SelectToken(key);
                            break;
                        case "traceparent":
                            actualValue = traceparentJson.SelectToken(key);
                            break;
                        case "tracestate":
                            actualValue = tracestateJson.SelectToken(key);
                            break;
                        default:
                            break;
                    }

                    Assert.That(actualValue.IsEqualTo(expectedValue), $"{key}, expected: {expectedValue}, actual: {actualValue}");
                }
            }

            payloadSettings.Expected?.ToList().ForEach(expected =>
            {
                switch (expected.Substring(0, expected.IndexOf('.')))
                {
                    case "newrelic":
                        Assert.That(newrelicJson.SelectToken(expected), Is.Not.Null, $"Missing expected: {expected}");
                        break;
                    case "traceparent":
                        Assert.That(traceparentJson.SelectToken(expected), Is.Not.Null, $"Missing expected: {expected}");
                        break;
                    case "tracestate":
                        Assert.That(tracestateJson.SelectToken(expected), Is.Not.Null, $"Missing expected: {expected}");
                        break;
                    default:
                        break;
                }
            });

            payloadSettings.Unexpected?.ToList().ForEach(unexpected =>
            {
                switch (unexpected.Substring(0, unexpected.IndexOf('.')))
                {
                    case "newrelic":
                        Assert.That(newrelicJson.SelectToken(unexpected), Is.Null, $"Unexpected exists: {unexpected}");
                        break;
                    case "traceparent":
                        Assert.That(traceparentJson.SelectToken(unexpected), Is.Empty, $"Unexpected exists: {unexpected}");
                        break;
                    case "tracestate":
                        Assert.That(tracestateJson.SelectToken(unexpected), Is.Empty, $"Unexpected exists: {unexpected}");
                        break;
                    default:
                        break;
                }
            });

            var notequalFields = payloadSettings.Notequal;
            if (notequalFields != null)
            {
                foreach (var key in notequalFields.Keys)
                {
                    var notValue = notequalFields[key];

                    switch (key.Substring(0, key.IndexOf('.')))
                    {
                        case "newrelic":
                            actualValue = newrelicJson.SelectToken(key);
                            break;
                        case "traceparent":
                            actualValue = traceparentJson.SelectToken(key);
                            break;
                        case "tracestate":
                            actualValue = tracestateJson.SelectToken(key);
                            break;
                        default:
                            break;
                    }

                    Assert.That(actualValue.IsNotEqualTo(notValue), $"Expected not equal {key}, but was equal {notValue}");
                }
            }

            if (payloadSettings.Vendors != null)
            {
                JArray actualVendors = (JArray)tracestateJson["tracestate"]["vendors"];

                Assert.That(JToken.DeepEquals(actualVendors, payloadSettings.Vendors), $"Expected vendors {payloadSettings.Vendors}, actual: {actualVendors}");
            }
        }

        private void ValidateIntrinsics(TraceContextTestData testData)
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

        private void ValidateAttributes(IDictionary<string, object> actualAttributes, TraceContextTestData testData, JToken eventSpecificAttributes = null)
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

        private void ValidateMetrics(TraceContextTestData testData)
        {
            var expectedMetrics = new List<ExpectedMetric>();

            // convert json ExpectedMetrics for CompositeTests.MetricAssertions
            foreach (var metric in testData.ExpectedMetrics)
            {
                expectedMetrics.Add(new ExpectedTimeMetric() { Name = metric.Name, CallCount = metric.Count });
            }

            MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
        }

        #region Trace Context Test Data Classes

        public class TraceContextTestData
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

            [JsonProperty("transaction_events_enabled")]
            public bool? TransactionEventsEnabled { get; set; }

            [JsonProperty("transport_type")]
            public string TransportType { get; set; }

            [JsonProperty("inbound_headers")]
            public HeaderSettings[] InboundHeaders { get; set; }

            [JsonProperty("outbound_payloads", NullValueHandling = NullValueHandling.Ignore)]
            public OutboundPayloadSettings[] OutboundPayloadsSettings { get; set; }

            [JsonProperty("intrinsics")]
            public IntrinsicsSettings IntrinsicSettings { get; set; }

            [JsonProperty("expected_metrics")]
            public Metric[] ExpectedMetrics { get; set; }
        }

        public class HeaderSettings
        {
            [JsonExtensionData]
            public IDictionary<string, JToken> Headers { get; set; }
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

            [JsonProperty("notequal", NullValueHandling = NullValueHandling.Ignore)]
            public IDictionary<string, JToken> Notequal { get; set; }

            [JsonProperty("vendors", NullValueHandling = NullValueHandling.Ignore)]
            public JArray Vendors { get; set; }
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
