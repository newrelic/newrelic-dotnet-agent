/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NUnit.Framework;
using System;
using System.Collections.Generic;
using NewRelic.Core.DistributedTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;

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

        //example ValidTracestate: "33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,dd=YzRiMTIxODk1NmVmZTE4ZQ,44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020";
        private static readonly string ValidTracestate = TrustKey + "@nr=0-" + (int)Type + "-" + AccountId + "-" + AppId + "-" + Guid + "-" + TransactionId + "-1-" + Priority + "-" + Timestamp.ToUnixTimeMilliseconds() + ",dd=YzRiMTIxODk1NmVmZTE4ZQ,44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020";

        private const string NewRelicPayloadHeaderName = "newrelic";

        // v:[2,5]
        private const string NewRelicPayloadWithUnsupportedVersion = "{ \"v\":[2,5],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
        // ti:0
        private const string NewRelicPayloadWithInvalidTimestamp = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
        // missing tx: AND id:
        private const string NewRelicPayloadUntraceable = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\"}}";

        #region NewRelic Payload

        [Test]
        public void AcceptDistributedTraceHeadersHydratesValidNewRelicPayload()
        {
            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(BuildSampleDistributedTracePayload());

            var headers = new Dictionary<string, string>()
            {
                { NewRelicPayloadHeaderName, encodedPayload }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(Type, tracingState.Type);
            Assert.AreEqual(AccountId, tracingState.AccountId);
            Assert.AreEqual(AppId, tracingState.AppId);
            Assert.AreEqual(Guid, tracingState.Guid);
            Assert.AreEqual(TraceId, tracingState.TraceId);
            Assert.AreEqual(Priority, tracingState.Priority);
            Assert.AreEqual(Sampled, tracingState.Sampled);
            Assert.AreEqual(TransactionId, tracingState.TransactionId);
            Assert.IsTrue(tracingState.Timestamp != default, $"Timestamp should not be {(DateTime)default}");
            Assert.IsTrue(tracingState.TransportDuration > TimeSpan.Zero, $"TransportDuration should not be Zero");
        }

        [Test]
        public void AcceptDistributedTraceHeadersPopulatesErrorsIfNull()
        {
            var headers = new Dictionary<string, string>()
            {
                { NewRelicPayloadHeaderName, null }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.TraceId);
            Assert.IsNull(tracingState.TransactionId);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.Priority);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.NullPayload), "TracingState IngestErrors should contain NullPayload");
        }

        [Test]
        public void AcceptDistributedTraceHeadersPopulatesErrorsIfUnsupportedVersion()
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadWithUnsupportedVersion);

            var headers = new Dictionary<string, string>()
            {
                { NewRelicPayloadHeaderName, encodedPayload }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.TraceId);
            Assert.IsNull(tracingState.TransactionId);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.Priority);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.Version), "TracingState IngestErrors should contain Version error.");
        }

        [Test]
        public void AcceptDistributedTraceHeadersPopulatesErrorsIfInvalidTimestamp()
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadWithInvalidTimestamp);

            var headers = new Dictionary<string, string>()
            {
                { NewRelicPayloadHeaderName, encodedPayload }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.TraceId);
            Assert.IsNull(tracingState.TransactionId);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.Priority);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
        }

        [Test]
        public void AcceptDistributedTraceHeadersPopulatesErrorsIfNotTraceable()
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadUntraceable);

            var headers = new Dictionary<string, string>()
            {
                { NewRelicPayloadHeaderName, encodedPayload }
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.Other, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.TraceId);
            Assert.IsNull(tracingState.TransactionId);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.Priority);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
        }

        [Test]
        public void AcceptDistributedTracePayloadHydratesValidNewRelicPayload()
        {
            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(BuildSampleDistributedTracePayload());
            var tracingState = TracingState.AcceptDistributedTracePayload(encodedPayload, TransportType.Other, TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(Type, tracingState.Type);
            Assert.AreEqual(AccountId, tracingState.AccountId);
            Assert.AreEqual(AppId, tracingState.AppId);
            Assert.AreEqual(Guid, tracingState.Guid);
            Assert.AreEqual(TraceId, tracingState.TraceId);
            Assert.AreEqual(Priority, tracingState.Priority);
            Assert.AreEqual(Sampled, tracingState.Sampled);
            Assert.AreEqual(TransactionId, tracingState.TransactionId);
            Assert.IsTrue(tracingState.Timestamp != default, $"Timestamp should not be {(DateTime)default}");
            Assert.IsTrue(tracingState.TransportDuration > TimeSpan.Zero, $"TransportDuration should not be Zero");
        }

        [Test]
        public void AcceptDistributedTracePayloadPopulatesErrorsIfNull()
        {
            string _nullPayload = null;
            var tracingState = TracingState.AcceptDistributedTracePayload(_nullPayload, TransportType.IronMQ, TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.TraceId);
            Assert.IsNull(tracingState.TransactionId);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.Priority);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.NullPayload), "TracingState IngestErrors should contain NullPayload");
        }

        [Test]
        public void AcceptDistributedTracePayloadPopulatesErrorsIfUnsupportedVersion()
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadWithUnsupportedVersion);
            var tracingState = TracingState.AcceptDistributedTracePayload(encodedPayload, TransportType.Other, TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.TraceId);
            Assert.IsNull(tracingState.TransactionId);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.Priority);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.Version), "TracingState IngestErrors should contain Version error.");
        }

        [Test]
        public void AcceptDistributedTracePayloadPopulatesErrorsIfInvalidTimestamp()
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadWithInvalidTimestamp);
            var tracingState = TracingState.AcceptDistributedTracePayload(encodedPayload, TransportType.Other, TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.TraceId);
            Assert.IsNull(tracingState.TransactionId);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.Priority);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
        }

        [Test]
        public void AcceptDistributedTracePayloadPopulatesErrorsIfNotTraceable()
        {
            var encodedPayload = Strings.Base64Encode(NewRelicPayloadUntraceable);
            var tracingState = TracingState.AcceptDistributedTracePayload(encodedPayload, TransportType.Other, TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.TraceId);
            Assert.IsNull(tracingState.TransactionId);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.Priority);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
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

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.AreEqual(DistributedTracingParentType.App, tracingState.Type);
            Assert.AreEqual(AccountId, tracingState.AccountId);
            Assert.AreEqual(AppId, tracingState.AppId);
            Assert.AreEqual(Guid, tracingState.Guid);
            Assert.AreEqual(TraceId, tracingState.TraceId);
            Assert.AreEqual(Priority, tracingState.Priority);
            Assert.AreEqual(Sampled, tracingState.Sampled);
            Assert.AreEqual(TransactionId, tracingState.TransactionId);
            Assert.AreEqual(ParentId, tracingState.ParentId);
            Assert.IsTrue(tracingState.Timestamp != default, $"Timestamp should not be {(DateTime)default}");
            Assert.IsTrue(tracingState.TransportDuration > TimeSpan.Zero, $"TransportDuration should not be Zero");
        }

        [Test]
        public void AcceptDistributedTraceHeadersHydratesValidW3CTraceContext_WithoutAValidNREntry()
        {
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", "aa=1,bb=2" },
            };

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.IsNull(tracingState.AccountId);
            Assert.IsNull(tracingState.AppId);
            Assert.IsNull(tracingState.Guid);
            Assert.IsNull(tracingState.Priority);
            Assert.IsNull(tracingState.Sampled);
            Assert.IsNull(tracingState.TransactionId);
            Assert.AreEqual(string.Join(",", tracingState.VendorStateEntries), "aa=1,bb=2");
            Assert.AreEqual(TraceId, tracingState.TraceId);
            Assert.AreEqual(ParentId, tracingState.ParentId);
            Assert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            Assert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            Assert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");
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

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.IsNotNull(tracingState);
            Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.TraceParentParseException), "TracingState IngestErrors should contain TraceParentParseException.");
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
            var headerValues = new List<string>();

            foreach (var item in carrier)
            {
                if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    headerValues.Add(item.Value);
                }
            }

            return headerValues;
        }

        #endregion helpers
    }
}
