using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Telerik.JustMock;

namespace CompositeTests.CrossAgentTests.DistributedTracing
{
	[TestFixture]
	public class DistributedTracingCrossAgentTests
	{
		private static CompositeTestAgent _compositeTestAgent;
		private IAgentWrapperApi _agentWrapperApi;
		private static IAdaptiveSampler _adaptiveSampler;

		public static List<TestCaseData> DistributedTraceTestDatas => GetDistributedTraceTestData();

		[SetUp]
		public void Setup()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
			_adaptiveSampler = Mock.Create<IAdaptiveSampler>(Behavior.CallOriginal);
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[TestCaseSource(nameof(DistributedTraceTestDatas))]
		public void DistributedTrace_CrossAgentTests(DistributedTraceTestData testData)
		{
			InitializeSettings(testData);

			MakeTransaction(testData);

			_compositeTestAgent.Harvest();

			ValidateIntrinsics(testData);

			ValidateMetrics(testData);
		}

		private static List<TestCaseData> GetDistributedTraceTestData()
		{
			var testCaseDatas = new List<TestCaseData>();

			var dllPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
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
			var testList = JsonConvert.DeserializeObject<List<DistributedTraceTestData>>(jsonString, settings);

			foreach (var testData in testList)
			{
				var testCase = new TestCaseData(testData);
				testCase.SetName("DistributedTraceCrossAgentTests: " + testData.Name);
				testCaseDatas.Add(testCase);
			}

			return testCaseDatas;
		}

		private static void InitializeSettings(DistributedTraceTestData testData)
		{
			Assert.That(testData.MajorVersionSupported, Is.GreaterThanOrEqualTo(DistributedTracePayload.SupportedMajorVersion));
			Assert.That(testData.MinorVersionSupported, Is.GreaterThanOrEqualTo(DistributedTracePayload.SupportedMinorVersion));

			if (testData.ForceSampledTrue)
			{
				var priority = 1.0f;
				Mock.Arrange(() => _adaptiveSampler.ComputeSampled(ref priority)).IgnoreArguments().Returns(true);
			}

			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			_compositeTestAgent.LocalConfiguration.spanEvents.enabled = testData.SpanEventsEnabled;
			_compositeTestAgent.ServerConfiguration.TrustedAccountKey = testData.TrustedAccountKey;
			_compositeTestAgent.ServerConfiguration.AccountId = testData.AccountId;
			_compositeTestAgent.ServerConfiguration.PrimaryApplicationId = "primaryApplicationId";

			_compositeTestAgent.PushConfiguration();
		}

		void MakeTransaction(DistributedTraceTestData testData)
		{
			var testDataInboundPayloads = MakeHeaders(testData);

			var transaction = testData.WebTransaction ? _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name") : _agentWrapperApi.CreateOtherTransaction("Other", "name");

			using (transaction)
			{
				AcceptPayloads(testDataInboundPayloads, testData);

				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");

				if (testData.RaisesException)
				{
					transaction.NoticeError(new Exception("This is a new exception."));
				}

				testData.OutboundPayloadsSettings?.ForEach(payloadSettings =>
				{
					var payload = _agentWrapperApi.CurrentTransactionWrapperApi.CreateDistributedTracePayload();

					if (testData.OutboundPayloadsSettings != null)
					{
						ValidateOutboundPayload(payloadSettings, payload.Text());
					}
				});
				segment.End();
			}
		}

		List<string> MakeHeaders(DistributedTraceTestData testData)
		{
			List<string> testDataInboundPayloads = new List<string>();

			if (testData.InboundPayloadSettings == null)
			{
				testDataInboundPayloads.Add(null);
			}
			else
			{
				foreach (PayloadSettings payload in testData.InboundPayloadSettings)
				{
					testDataInboundPayloads.Add(Strings.Base64Encode(JsonConvert.SerializeObject(payload)));
				}
			}

			return testDataInboundPayloads;
		}

		private void AcceptPayloads(List<string> testDataInboundPayloads, DistributedTraceTestData testData)
		{
			testDataInboundPayloads.ForEach(serializedPayload =>
			{
				var validEnumValue = Enum.TryParse(testData.TransportType, ignoreCase: false, result: out TransportType transportType);
				if (!validEnumValue)
				{
					transportType = (TransportType) (-1);
				}

				_agentWrapperApi.CurrentTransactionWrapperApi.AcceptDistributedTracePayload(serializedPayload, transportType);
			});
		}

		private void ValidateOutboundPayload(OutboundPayloadSettings payloadSettings, string serializedPayload)
		{
			var jObjectActual = JObject.Parse(serializedPayload);

			var expectedFields = payloadSettings.Exact;
			if (expectedFields != null)
			{
				foreach (var key in expectedFields.Keys)
				{
					var expectedValue = expectedFields[key];
					var actualValue = jObjectActual.SelectToken(key);
					Assert.That(actualValue, Is.EqualTo(expectedValue), $"{key}");
				}
			}

			payloadSettings.Expected?.ForEach(expected =>
			{
				Assert.That(jObjectActual.SelectToken(expected), Is.Not.Null, $"{expected}");
			});

			payloadSettings.Unexpected?.ForEach(unexpected =>
			{
				Assert.That(jObjectActual.SelectToken(unexpected), Is.Null, $"{unexpected}");
			});
		}

		private void ValidateIntrinsics(DistributedTraceTestData testData)
		{
			foreach (var eventType in testData.IntrinsicSettings.TargetEvents)
			{
				switch (eventType)
				{
					case "Transaction":
						var transactionEvent = _compositeTestAgent.TransactionEvents.First();
						ValidateAttributes(transactionEvent.IntrinsicAttributes, testData, testData.IntrinsicSettings.Events?["Transaction"]);
						break;
					case "Span":
						Assert.That(_compositeTestAgent.SpanEvents.Count, Is.EqualTo(2));	// fake parent and first segment
						var spanEvent = _compositeTestAgent.SpanEvents.First();
						ValidateAttributes(spanEvent.IntrinsicAttributes, testData, testData.IntrinsicSettings.Events?["Span"]);
						break;
					case "Error":
						var errorEvent = _compositeTestAgent.ErrorEvents.First();
						ValidateAttributes(errorEvent.IntrinsicAttributes, testData, testData.IntrinsicSettings.Events?["Error"]);
						break;
					default:
						break;
				}
			}
		}

		private void ValidateAttributes(ReadOnlyDictionary<string, object> actualAttributes, DistributedTraceTestData testData, JToken eventSpecificAttributes = null)
		{
			// Common (for all target_events)
			ValidateAttributeSettings(testData.IntrinsicSettings.CommonAttributes, actualAttributes);

			// event-specific attrs
			ValidateAttributeSettings(eventSpecificAttributes?.ToObject<AttributesSettings>(), actualAttributes);
		}

		private void ValidateAttributeSettings(AttributesSettings testDataAttributesSettings, NewRelic.Agent.Core.ReadOnlyDictionary<string, object> actualEventAttributes)
		{
			testDataAttributesSettings?.Exact?.Keys.ToList().ForEach(attr =>
			{
				Assert.That(actualEventAttributes.ContainsKey(attr));
				testDataAttributesSettings.Exact.TryGetValue(attr, out var expectedValue);

				var attrValue = actualEventAttributes[attr];
				var attrType = attrValue.GetType();
				var typedExpectedValue = Convert.ChangeType(expectedValue, attrType);

				Assert.That(typedExpectedValue, Is.EqualTo(attrValue), $"{attr}");
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

		private void ValidateMetrics(DistributedTraceTestData testData)
		{
			var expectedMetrics = new List<ExpectedMetric>();

			// convert json ExpectedMetrics for CompositeTests.MetricAssertions
			foreach (var metric in testData.ExpectedMetrics)
			{
				expectedMetrics.Add(new ExpectedTimeMetric() { Name = metric.Name, CallCount = metric.Count });
			}

			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
		}

		#region DistributedTrace Test Data Classes

		public class DistributedTraceTestData
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
			public int MajorVersionSupported { get; set; }

			[JsonProperty("minor_version")]
			public int MinorVersionSupported { get; set; }

			[JsonProperty("transport_type")]
			public string TransportType { get; set; }

			[JsonProperty("inbound_payloads")]
			public PayloadSettings[] InboundPayloadSettings { get; set; }

			[JsonProperty("outbound_payloads", NullValueHandling = NullValueHandling.Ignore)]
			public OutboundPayloadSettings[] OutboundPayloadsSettings { get; set; }

			[JsonProperty("intrinsics")]
			public IntrinsicsSettings IntrinsicSettings { get; set; }

			[JsonProperty("expected_metrics")]
			public Metric[] ExpectedMetrics { get; set; }
		}

		[JsonObject]
		public class PayloadSettings
		{
			[JsonProperty("v")]
			public int[] Version { get; set; }

			[JsonProperty("d")]
			public PayloadData Data { get; set; }
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

		public class PayloadData
		{
			[JsonProperty("ac")]
			public string AccountId { get; set; }

			[JsonProperty("ap")]
			public string ApplicationId { get; set; }

			[JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
			public string Guid { get; set; }

			[JsonProperty("tx", NullValueHandling = NullValueHandling.Ignore)]
			public string TransactionId { get; set; }

			[JsonProperty("pr", NullValueHandling = NullValueHandling.Ignore)]
			public float Priority { get; set; }

			[JsonProperty("sa", NullValueHandling = NullValueHandling.Ignore)]
			public bool? Sampled { get; set; }

			[JsonProperty("ti")]
			public long Timestamp { get; set; }

			[JsonProperty("tr")]
			public string TraceId { get; set; }

			[JsonProperty("tk", NullValueHandling = NullValueHandling.Ignore)]

			public string TrustKey { get; set; }

			[JsonProperty("ty")]
			public string Type { get; set; }
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
