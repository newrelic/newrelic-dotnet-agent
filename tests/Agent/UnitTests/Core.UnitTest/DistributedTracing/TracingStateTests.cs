// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DistributedTracing
{
    [TestFixture]
    public class TracingStateTests
    {
        private ISamplerService _samplerService;
        private const DistributedTracingParentType Type = DistributedTracingParentType.App;
        private const string AccountId = "accountId";
        private const string AppId = "appId";
        private const string Guid = "5569065a5b1313bd";
        private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
        private const string ParentId = "ad6b7169203331bb";
        private const string TrustKey = "33";
        private const string TransactionId = "transactionId";
        private const float Priority = .65f;
        private const bool Sampled = true;
        private static DateTime Timestamp = DateTime.UtcNow;

        private const string ValidTraceparent = "00-" + TraceId + "-" + ParentId + "-01";
        private const string ValidTraceparentNotSampled = "00-" + TraceId + "-" + ParentId + "-00";

        //example ValidTracestate: "33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,dd=YzRiMTIxODk1NmVmZTE4ZQ,44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020";
        private static readonly string ValidTracestate = TrustKey + "@nr=0-" + (int)Type + "-" + AccountId + "-" + AppId + "-" + Guid + "-" + TransactionId + "-1-" + Priority + "-" + Timestamp.ToUnixTimeMilliseconds() + ",dd=YzRiMTIxODk1NmVmZTE4ZQ,44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020";


        // v:[2,5]
        private const string NewRelicPayloadWithUnsupportedVersion = "{ \"v\":[2,5],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
        // ti:0
        private const string NewRelicPayloadWithInvalidTimestamp = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
        // missing tx: AND id:
        private const string NewRelicPayloadUntraceable = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\"}}";

        [SetUp]
        public void Setup()
        {
            _samplerService = Mock.Create<ISamplerService>();
            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.Root))
                .Returns(new AdaptiveSampler(1, 1, 1, false)); // Using a simple sampler for testing
            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentSampled))
                .Returns((ISampler)null);
            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentNotSampled))
                .Returns((ISampler)null);

        }

        #region NewRelic Payload

        [TestCase(Constants.DistributedTracePayloadKeyAllLower)]
        [TestCase(Constants.DistributedTracePayloadKeyAllUpper)]
        [TestCase(Constants.DistributedTracePayloadKeySingleUpper)]
        public void AcceptDistributedTraceHeadersHydratesValidNewRelicPayload(string headerName)
        {
            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(BuildSampleDistributedTracePayload());

            var headers = new Dictionary<string, string>()
            {
                { headerName, encodedPayload }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            Assert.That(tracingState, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tracingState.Type, Is.EqualTo(Type));
                Assert.That(tracingState.AccountId, Is.EqualTo(AccountId));
                Assert.That(tracingState.AppId, Is.EqualTo(AppId));
                Assert.That(tracingState.Guid, Is.EqualTo(Guid));
                Assert.That(tracingState.TraceId, Is.EqualTo(TraceId));
                Assert.That(tracingState.Priority, Is.EqualTo(Priority));
                Assert.That(tracingState.Sampled, Is.EqualTo(Sampled));
                Assert.That(tracingState.TransactionId, Is.EqualTo(TransactionId));
                Assert.That(tracingState.Timestamp, Is.Not.EqualTo(default(DateTime)), $"Timestamp should not be {default(DateTime)}");
                Assert.That(tracingState.TransportDuration, Is.GreaterThan(TimeSpan.Zero), $"TransportDuration should not be Zero");
            });
        }

        [TestCase(Constants.DistributedTracePayloadKeyAllLower)]
        [TestCase(Constants.DistributedTracePayloadKeyAllUpper)]
        [TestCase(Constants.DistributedTracePayloadKeySingleUpper)]
        public void AcceptDistributedTraceHeadersPopulatesErrorsIfNull(string headerName)
        {
            var headers = new Dictionary<string, string>()
            {
                { headerName, null }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            Assert.That(tracingState, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tracingState.Type, Is.EqualTo(DistributedTracingParentType.Unknown));
                Assert.That(tracingState.AppId, Is.Null);
                Assert.That(tracingState.AccountId, Is.Null);
                Assert.That(tracingState.Guid, Is.Null);
                Assert.That(tracingState.TraceId, Is.Null);
                Assert.That(tracingState.TransactionId, Is.Null);
                Assert.That(tracingState.Sampled, Is.Null);
                Assert.That(tracingState.Priority, Is.Null);
                Assert.That(tracingState.Timestamp, Is.EqualTo(default(DateTime)), $"Timestamp: expected {default(DateTime)}, actual: {tracingState.Timestamp}");
                Assert.That(tracingState.TransportDuration, Is.EqualTo(TimeSpan.Zero), $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

                Assert.That(tracingState.IngestErrors, Does.Contain(IngestErrorType.NullPayload), "TracingState IngestErrors should contain NullPayload");
            });
        }

        [TestCase(Constants.DistributedTracePayloadKeyAllLower)]
        [TestCase(Constants.DistributedTracePayloadKeyAllUpper)]
        [TestCase(Constants.DistributedTracePayloadKeySingleUpper)]
        public void AcceptDistributedTraceHeadersPopulatesErrorsIfUnsupportedVersion(string headerName)
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadWithUnsupportedVersion);

            var headers = new Dictionary<string, string>()
            {
                { headerName, encodedPayload }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            Assert.That(tracingState, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tracingState.Type, Is.EqualTo(DistributedTracingParentType.Unknown));
                Assert.That(tracingState.AppId, Is.Null);
                Assert.That(tracingState.AccountId, Is.Null);
                Assert.That(tracingState.Guid, Is.Null);
                Assert.That(tracingState.TraceId, Is.Null);
                Assert.That(tracingState.TransactionId, Is.Null);
                Assert.That(tracingState.Sampled, Is.Null);
                Assert.That(tracingState.Priority, Is.Null);
                Assert.That(tracingState.Timestamp, Is.EqualTo(default(DateTime)), $"Timestamp: expected {default(DateTime)}, actual: {tracingState.Timestamp}");
                Assert.That(tracingState.TransportDuration, Is.EqualTo(TimeSpan.Zero), $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

                Assert.That(tracingState.IngestErrors, Does.Contain(IngestErrorType.Version), "TracingState IngestErrors should contain Version error.");
            });
        }

        [TestCase(Constants.DistributedTracePayloadKeyAllLower)]
        [TestCase(Constants.DistributedTracePayloadKeyAllUpper)]
        [TestCase(Constants.DistributedTracePayloadKeySingleUpper)]
        public void AcceptDistributedTraceHeadersPopulatesErrorsIfInvalidTimestamp(string headerName)
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadWithInvalidTimestamp);

            var headers = new Dictionary<string, string>()
            {
                { headerName, encodedPayload }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            Assert.That(tracingState, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tracingState.Type, Is.EqualTo(DistributedTracingParentType.Unknown));
                Assert.That(tracingState.AppId, Is.Null);
                Assert.That(tracingState.AccountId, Is.Null);
                Assert.That(tracingState.Guid, Is.Null);
                Assert.That(tracingState.TraceId, Is.Null);
                Assert.That(tracingState.TransactionId, Is.Null);
                Assert.That(tracingState.Sampled, Is.Null);
                Assert.That(tracingState.Priority, Is.Null);
                Assert.That(tracingState.Timestamp, Is.EqualTo(default(DateTime)), $"Timestamp: expected {default(DateTime)}, actual: {tracingState.Timestamp}");
                Assert.That(tracingState.TransportDuration, Is.EqualTo(TimeSpan.Zero), $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

                Assert.That(tracingState.IngestErrors, Does.Contain(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
            });
        }

        [TestCase(Constants.DistributedTracePayloadKeyAllLower)]
        [TestCase(Constants.DistributedTracePayloadKeyAllUpper)]
        [TestCase(Constants.DistributedTracePayloadKeySingleUpper)]
        public void AcceptDistributedTraceHeadersPopulatesErrorsIfNotTraceable(string headerName)
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadUntraceable);

            var headers = new Dictionary<string, string>()
            {
                { headerName, encodedPayload }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.Other,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            Assert.That(tracingState, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tracingState.Type, Is.EqualTo(DistributedTracingParentType.Unknown));
                Assert.That(tracingState.AppId, Is.Null);
                Assert.That(tracingState.AccountId, Is.Null);
                Assert.That(tracingState.Guid, Is.Null);
                Assert.That(tracingState.TraceId, Is.Null);
                Assert.That(tracingState.TransactionId, Is.Null);
                Assert.That(tracingState.Sampled, Is.Null);
                Assert.That(tracingState.Priority, Is.Null);
                Assert.That(tracingState.Timestamp, Is.EqualTo(default(DateTime)), $"Timestamp: expected {default(DateTime)}, actual: {tracingState.Timestamp}");
                Assert.That(tracingState.TransportDuration, Is.EqualTo(TimeSpan.Zero), $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

                Assert.That(tracingState.IngestErrors, Does.Contain(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
            });
        }

        #endregion

        #region Trace-Context

        [Test]
        public void AcceptDistributedTraceHeadersHydratesValidW3CTraceContext()
        {
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", ValidTracestate },
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            Assert.That(tracingState, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tracingState.Type, Is.EqualTo(DistributedTracingParentType.App));
                Assert.That(tracingState.AccountId, Is.EqualTo(AccountId));
                Assert.That(tracingState.AppId, Is.EqualTo(AppId));
                Assert.That(tracingState.Guid, Is.EqualTo(Guid));
                Assert.That(tracingState.TraceId, Is.EqualTo(TraceId));
                Assert.That(tracingState.Priority, Is.EqualTo(Priority));
                Assert.That(tracingState.Sampled, Is.EqualTo(Sampled));
                Assert.That(tracingState.TransactionId, Is.EqualTo(TransactionId));
                Assert.That(tracingState.ParentId, Is.EqualTo(ParentId));
                Assert.That(tracingState.Timestamp, Is.Not.EqualTo(default(DateTime)), $"Timestamp should not be {default(DateTime)}");
                Assert.That(tracingState.TransportDuration, Is.GreaterThan(TimeSpan.Zero), $"TransportDuration should not be Zero");
            });
        }

        [Test]
        public void AcceptDistributedTraceHeadersHydratesValidW3CTraceContext_WithoutAValidNREntry()
        {
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", "aa=1,bb=2" },
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            Assert.That(tracingState, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tracingState.AccountId, Is.Null);
                Assert.That(tracingState.AppId, Is.Null);
                Assert.That(tracingState.Guid, Is.Null);
                Assert.That(tracingState.Priority, Is.Null);
                Assert.That(tracingState.Sampled, Is.Null);
                Assert.That(tracingState.TransactionId, Is.Null);
                Assert.That(string.Join(",", tracingState.VendorStateEntries), Is.EqualTo("aa=1,bb=2"));
                Assert.That(tracingState.TraceId, Is.EqualTo(TraceId));
                Assert.That(tracingState.ParentId, Is.EqualTo(ParentId));
                Assert.That(tracingState.Type, Is.EqualTo(DistributedTracingParentType.Unknown));
                Assert.That(tracingState.Timestamp, Is.EqualTo(default(DateTime)), $"Timestamp: expected {default(DateTime)}, actual: {tracingState.Timestamp}");
                Assert.That(tracingState.TransportDuration, Is.EqualTo(TimeSpan.Zero), $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");
            });
        }

        [Test]
        public void AcceptDistributedTraceHeadersPopulateErrorIfTraceParentNotParsable()
        {
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", "abc" }, //invalid traceparent
				{ "tracestate", ValidTracestate },
            };

            var getHeader = new Func<string, IList<string>>((key) =>
            {
                string value;
                headers.TryGetValue(key.ToLowerInvariant(), out value);
                return string.IsNullOrEmpty(value) ? null : new List<string> { value };
            });

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            Assert.That(tracingState, Is.Not.Null);
            Assert.That(tracingState.IngestErrors, Does.Contain(IngestErrorType.TraceParentParseException), "TracingState IngestErrors should contain TraceParentParseException.");
        }

        [TestCase(true, true, SamplerType.AlwaysOn, SamplerType.Adaptive, true, 2.0f, null, TestName = "TraceParentSampled_AlwaysOn")]
        [TestCase(true, true, SamplerType.AlwaysOff, SamplerType.Adaptive, false, 0f, null, TestName = "TraceParentSampled_AlwaysOff")]
        [TestCase(true, true, SamplerType.Adaptive, SamplerType.Adaptive, true, 0.65f, null, TestName = "TraceParentSampled_Adaptive")]
        [TestCase(true, false, SamplerType.Adaptive, SamplerType.AlwaysOn, true, 2.0f, null, TestName = "TraceParentNotSampled_AlwaysOn")]
        [TestCase(true, false, SamplerType.Adaptive, SamplerType.AlwaysOff, false, 0f, null, TestName = "TraceParentNotSampled_AlwaysOff")]
        [TestCase(true, false, SamplerType.Adaptive, SamplerType.Adaptive, true, 0.65f, null, TestName = "TraceParentNotSampled_Adaptive")]
        [TestCase(false, false, SamplerType.Adaptive, SamplerType.Adaptive, null, null, null, TestName = "TraceParentNotValid")]
        // TraceIdRatioBased (ratio = 1.0 -> always sample & boost priority)
        [TestCase(true, true, SamplerType.TraceIdRatioBased, SamplerType.Adaptive, true, 1.65f, 1.0f, TestName = "TraceParentSampled_RatioSampler_AlwaysSample")]
        [TestCase(true, false, SamplerType.Adaptive, SamplerType.TraceIdRatioBased, true, 1.65f, 1.0f, TestName = "TraceParentNotSampled_RatioSampler_AlwaysSample")]
        // TraceIdRatioBased (ratio = 0.0 -> never sample & no priority boost)
        [TestCase(true, true, SamplerType.TraceIdRatioBased, SamplerType.Adaptive, false, 0.65f, 0.0f, TestName = "TraceParentSampled_RatioSampler_NeverSample")]
        [TestCase(true, false, SamplerType.Adaptive, SamplerType.TraceIdRatioBased, false, 0.65f, 0.0f, TestName = "TraceParentNotSampled_RatioSampler_NeverSample")]
        public void Sampled_TestMatrix(
            bool traceParentValid,
            bool traceParentSampled,
            SamplerType remoteParentSampledSamplerType,
            SamplerType remoteParentNotSampledSamplerType,
            bool? expectedSampled,
            float? expectedPriority,
            float? ratio // only used when a TraceIdRatioBased sampler is supplied
            )
        {
            // Arrange
            var traceparent = traceParentValid
                ? (traceParentSampled ? ValidTraceparent : ValidTraceparentNotSampled)
                : null;
            var tracestate = ValidTracestate;

            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentSampled))
                .Returns(() => GetSampler(remoteParentSampledSamplerType, ratio));
            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentNotSampled))
                .Returns(() => GetSampler(remoteParentNotSampledSamplerType, ratio));

            var headers = new Dictionary<string, string>();
            if (traceparent != null)
            {
                headers["traceparent"] = traceparent;
            }
            if (tracestate != null)
            {
                headers["tracestate"] = tracestate;
            }

            // Act
            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow,
                _samplerService);

            // Assert
            Assert.That(tracingState.Sampled, Is.EqualTo(expectedSampled));
            Assert.That(tracingState.Priority, Is.EqualTo(expectedPriority));

            ISampler GetSampler(SamplerType behavior, float? r)
            {
                return behavior switch
                {
                    SamplerType.Adaptive => null,
                    SamplerType.Default => null,
                    SamplerType.AlwaysOn => AlwaysOnSampler.Instance,
                    SamplerType.AlwaysOff => AlwaysOffSampler.Instance,
                    SamplerType.TraceIdRatioBased => new TraceIdRatioSampler(r ?? 0.5f),
                    _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null)
                };
            }
        }

        [Test]
        public void Sampled_UsesTraceContextSampledValue_WhenBehaviorIsDefault()
        {
            var headers = new Dictionary<string, string>
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", ValidTracestate }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow, _samplerService);

            Assert.That(tracingState.Sampled, Is.EqualTo(Sampled), "Sampled should use the value from the trace context when behavior is 'default'.");
        }

        [Test]
        public void AcceptDistributedTraceHeaders_AppliesTraceIdSampleRatioCorrectly()
        {
            // Arrange
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", ValidTracestate },
            };

            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentSampled))
                .Returns(new TraceIdRatioSampler(1.0f));


            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            // Assert
            Assert.That(tracingState.Sampled, Is.True, "Sampled should be true when sampleRatio is 1.0");
            Assert.That(tracingState.Priority, Is.EqualTo(Priority + 1.0f), "Priority should be boosted when sampleRatio is 1.0");
        }

        [Test]
        public void AcceptDistributedTraceHeaders_AppliesTraceIdSampleRatio_SampledFalse()
        {
            // Arrange
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", ValidTracestate },
            };
            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentSampled))
                .Returns(new TraceIdRatioSampler(0.0f));

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)), _samplerService);

            // Assert
            Assert.That(tracingState.Sampled, Is.EqualTo(false), "Sampled should be false when sampleRatio is 0");
            Assert.That(tracingState.Priority, Is.EqualTo(Priority), "Priority should use the tracestate priority value when sampleRatio is 0");
        }

        [TestCase("0000000000000001", 0.01f, true, true, TestName = "TraceIdRatioBased_LowValue_AlwaysSamples_Ratio001_ParentSampled")]
        [TestCase("0000000000000001", 0.25f, true, true, TestName = "TraceIdRatioBased_LowValue_AlwaysSamples_Ratio025_ParentNotSampled")]
        [TestCase("3fffffffffffffff", 0.75f, true, true, TestName = "TraceIdRatioBased_MidValue_Sample_Ratio075_ParentSampled")]
        // Use boundary value 0x4000... (>= computed upperBound for ratio 0.50) to ensure NOT sampled
        [TestCase("4000000000000000", 0.50f, false, false, TestName = "TraceIdRatioBased_MidValue_NoSample_Ratio050_ParentNotSampled")]
        [TestCase("3fffffffffffffff", 0.25f, false, true, TestName = "TraceIdRatioBased_MidValue_NoSample_Ratio025_ParentSampled")]
        [TestCase("7fffffffffffffff", 0.99f, false, true, TestName = "TraceIdRatioBased_HighValue_NoSample_Ratio099_ParentSampled")]
        [TestCase("7fffffffffffffff", 0.75f, false, false, TestName = "TraceIdRatioBased_HighValue_NoSample_Ratio075_ParentNotSampled")]
        public void Probabilistic_TraceIdRatioBased_Sampling_Deterministic(string first16Hex, float ratio, bool expectedSampled, bool parentSampledFlag)
        {
            // Build a deterministic trace id (32 hex chars) using supplied high/low prefix + zeros for remaining 16 chars
            var fullTraceId = first16Hex + "0000000000000000";
            Assert.That(fullTraceId.Length, Is.EqualTo(32), "TraceId must be 32 hex chars");

            // Construct a traceparent with the supplied sampled flag
            var traceparent = $"00-{fullTraceId}-{ParentId}-{(parentSampledFlag ? "01" : "00")}";

            // Use existing valid tracestate (priority & other intrinsic values)
            var headers = new Dictionary<string, string>
            {
                { "traceparent", traceparent },
                { "tracestate", ValidTracestate }
            };

            // Configure sampler service so that only the relevant sampler level uses the ratio sampler
            if (parentSampledFlag)
            {
                Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentSampled))
                    .Returns(new TraceIdRatioSampler(ratio));
                Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentNotSampled))
                    .Returns((ISampler)null);
            }
            else
            {
                Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentSampled))
                    .Returns((ISampler)null);
                Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.RemoteParentNotSampled))
                    .Returns(new TraceIdRatioSampler(ratio));
            }

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.HTTP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.AddMilliseconds(1),
                _samplerService);

            // Expected priority: base priority (.65) + 1.0f if sampled (ratio sampler boosts by 1.0)
            var expectedPriority = expectedSampled ? Priority + 1.0f : Priority;

            Assert.Multiple(() =>
            {
                Assert.That(tracingState.TraceId, Is.EqualTo(fullTraceId));
                Assert.That(tracingState.ParentId, Is.EqualTo(ParentId));
                Assert.That(tracingState.Sampled, Is.EqualTo(expectedSampled));
                Assert.That(tracingState.Priority, Is.EqualTo(expectedPriority));
            });
        }
        #endregion

        #region helpers
        private static DistributedTracePayload BuildSampleDistributedTracePayload()
        {
            return DistributedTracePayload.TryBuildOutgoingPayload(
                Type.ToString(),
                AccountId,
                AppId,
                Guid,
                TraceId,
                TrustKey,
                Priority,
                Sampled,
                Timestamp,
                TransactionId);
        }

        private static IEnumerable<string> GetHeader(Dictionary<string, string> carrier, string key)
        {
            return carrier.ContainsKey(key) ? new List<string>() { carrier[key] } : new List<string>();
        }

        #endregion helpers
    }
}
