// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DistributedTracing
{
    [TestFixture]
    public class TracingStateTests
    {
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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

            Assert.That(tracingState, Is.Not.Null);
            Assert.That(tracingState.IngestErrors, Does.Contain(IngestErrorType.TraceParentParseException), "TracingState IngestErrors should contain TraceParentParseException.");
        }

        [TestCase(true, true, RemoteParentSampledBehavior.AlwaysOn, RemoteParentSampledBehavior.Default, true, 2.0f, TestName = "TraceParentSampled_AlwaysOn")]
        [TestCase(true, true, RemoteParentSampledBehavior.AlwaysOff, RemoteParentSampledBehavior.Default, false, 0f, TestName = "TraceParentSampled_AlwaysOff")]
        [TestCase(true, true, RemoteParentSampledBehavior.Default, RemoteParentSampledBehavior.Default, true, 0.65f, TestName = "TraceParentSampled_Default")]
        [TestCase(true, false, RemoteParentSampledBehavior.Default, RemoteParentSampledBehavior.AlwaysOn, true, 2.0f, TestName = "TraceParentNotSampled_AlwaysOn")]
        [TestCase(true, false, RemoteParentSampledBehavior.Default, RemoteParentSampledBehavior.AlwaysOff, false, 0f, TestName = "TraceParentNotSampled_AlwaysOff")]
        [TestCase(true, false, RemoteParentSampledBehavior.Default, RemoteParentSampledBehavior.Default, true, 0.65f, TestName = "TraceParentNotSampled_Default")]
        [TestCase(false, false, RemoteParentSampledBehavior.Default, RemoteParentSampledBehavior.Default, null, null, TestName = "TraceParentNotValid")]
        public void Sampled_TestMatrix(
            bool traceParentValid,
            bool traceParentSampled,
            RemoteParentSampledBehavior remoteParentSampledBehavior,
            RemoteParentSampledBehavior remoteParentNotSampledBehavior,
            bool? expectedSampled, float? expectedPriority)
        {
            // Arrange
            var traceparent = traceParentValid ? traceParentSampled ? ValidTraceparent : ValidTraceparentNotSampled : null;
            var tracestate = ValidTracestate;

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
                remoteParentSampledBehavior: remoteParentSampledBehavior,
                remoteParentNotSampledBehavior: remoteParentNotSampledBehavior, traceIdSampleRatio: null);

            // Assert
            Assert.That(tracingState.Sampled, Is.EqualTo(expectedSampled));
            Assert.That(tracingState.Priority, Is.EqualTo(expectedPriority));
        }

        [Test]
        public void Sampled_ThrowsException_WhenInvalidRemoteParentSampledBehavior()
        {
            var headers = new Dictionary<string, string>
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", ValidTracestate }
            };

            Assert.Throws<ArgumentException>(() =>
            {
                TracingState.AcceptDistributedTraceHeaders(
                    carrier: headers,
                    getter: GetHeader,
                    transportType: TransportType.AMQP,
                    agentTrustKey: TrustKey,
                    transactionStartTime: DateTime.UtcNow,
                    remoteParentSampledBehavior: (RemoteParentSampledBehavior)999, // Invalid enum value
                    remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);
            });
        }

        [Test]
        public void Sampled_ThrowsException_WhenInvalidRemoteParentNotSampledBehavior()
        {
            var headers = new Dictionary<string, string>
            {
                { "traceparent", ValidTraceparentNotSampled },
                { "tracestate", ValidTracestate }
            };

            Assert.Throws<ArgumentException>(() =>
            {
                TracingState.AcceptDistributedTraceHeaders(
                    carrier: headers,
                    getter: GetHeader,
                    transportType: TransportType.AMQP,
                    agentTrustKey: TrustKey,
                    transactionStartTime: DateTime.UtcNow,
                    remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                    remoteParentNotSampledBehavior: (RemoteParentSampledBehavior)999, traceIdSampleRatio: null // Invalid enum value
                );
            });
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
                transactionStartTime: DateTime.UtcNow,
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

            Assert.That(tracingState.Sampled, Is.EqualTo(Sampled), "Sampled should use the value from the trace context when behavior is 'default'.");
        }

        [Test]
        public void AcceptDistributedTraceHeaders_AppliesTraceIdSampleRatio_WhenNonNull()
        {
            // Arrange
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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: 1.0f);

            // Assert
            Assert.That(tracingState.Sampled, Is.True, "Sampled should be true when sampleRatio is 1.0");
            Assert.That(tracingState.Priority, Is.EqualTo(Priority + 1.0f), "Priority should be boosted when sampleRatio is 1.0");
        }

        [Test]
        public void AcceptDistributedTraceHeaders_DoesNotApplyTraceIdSampleRatio_WhenNull()
        {
            // Arrange
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
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: null);

            // Assert
            Assert.That(tracingState.Sampled, Is.EqualTo(Sampled), "Sampled should use the tracestate sampled value when sampleRatio is null");
            Assert.That(tracingState.Priority, Is.EqualTo(Priority), "Priority should use the tracestate priority value when sampleRatio is null");
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

            var tracingState = TracingState.AcceptDistributedTraceHeaders(
                carrier: headers,
                getter: GetHeader,
                transportType: TransportType.AMQP,
                agentTrustKey: TrustKey,
                transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)),
                remoteParentSampledBehavior: RemoteParentSampledBehavior.Default,
                remoteParentNotSampledBehavior: RemoteParentSampledBehavior.Default, traceIdSampleRatio: 0.0f);

            // Assert
            Assert.That(tracingState.Sampled, Is.EqualTo(false), "Sampled should be false when sampleRatio is 0");
            Assert.That(tracingState.Priority, Is.EqualTo(Priority), "Priority should use the tracestate priority value when sampleRatio is 0");
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
