// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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

        // v:[2,5]
        private const string NewRelicPayloadWithUnsupportedVersion = "{ \"v\":[2,5],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
        // ti:0
        private const string NewRelicPayloadWithInvalidTimestamp = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
        // missing tx: AND id:
        private const string NewRelicPayloadUntraceable = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\"}}";

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

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            ClassicAssert.IsNotNull(tracingState);
            ClassicAssert.AreEqual(Type, tracingState.Type);
            ClassicAssert.AreEqual(AccountId, tracingState.AccountId);
            ClassicAssert.AreEqual(AppId, tracingState.AppId);
            ClassicAssert.AreEqual(Guid, tracingState.Guid);
            ClassicAssert.AreEqual(TraceId, tracingState.TraceId);
            ClassicAssert.AreEqual(Priority, tracingState.Priority);
            ClassicAssert.AreEqual(Sampled, tracingState.Sampled);
            ClassicAssert.AreEqual(TransactionId, tracingState.TransactionId);
            ClassicAssert.IsTrue(tracingState.Timestamp != default, $"Timestamp should not be {(DateTime)default}");
            ClassicAssert.IsTrue(tracingState.TransportDuration > TimeSpan.Zero, $"TransportDuration should not be Zero");
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

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            ClassicAssert.IsNotNull(tracingState);
            ClassicAssert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            ClassicAssert.IsNull(tracingState.AppId);
            ClassicAssert.IsNull(tracingState.AccountId);
            ClassicAssert.IsNull(tracingState.Guid);
            ClassicAssert.IsNull(tracingState.TraceId);
            ClassicAssert.IsNull(tracingState.TransactionId);
            ClassicAssert.IsNull(tracingState.Sampled);
            ClassicAssert.IsNull(tracingState.Priority);
            ClassicAssert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            ClassicAssert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            ClassicAssert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.NullPayload), "TracingState IngestErrors should contain NullPayload");
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

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            ClassicAssert.IsNotNull(tracingState);
            ClassicAssert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            ClassicAssert.IsNull(tracingState.AppId);
            ClassicAssert.IsNull(tracingState.AccountId);
            ClassicAssert.IsNull(tracingState.Guid);
            ClassicAssert.IsNull(tracingState.TraceId);
            ClassicAssert.IsNull(tracingState.TransactionId);
            ClassicAssert.IsNull(tracingState.Sampled);
            ClassicAssert.IsNull(tracingState.Priority);
            ClassicAssert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            ClassicAssert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            ClassicAssert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.Version), "TracingState IngestErrors should contain Version error.");
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

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.AMQP, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            ClassicAssert.IsNotNull(tracingState);
            ClassicAssert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            ClassicAssert.IsNull(tracingState.AppId);
            ClassicAssert.IsNull(tracingState.AccountId);
            ClassicAssert.IsNull(tracingState.Guid);
            ClassicAssert.IsNull(tracingState.TraceId);
            ClassicAssert.IsNull(tracingState.TransactionId);
            ClassicAssert.IsNull(tracingState.Sampled);
            ClassicAssert.IsNull(tracingState.Priority);
            ClassicAssert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            ClassicAssert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            ClassicAssert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
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

            var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier: headers, getter: GetHeader, transportType: TransportType.Other, agentTrustKey: TrustKey, transactionStartTime: DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(1)));

            ClassicAssert.IsNotNull(tracingState);
            ClassicAssert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            ClassicAssert.IsNull(tracingState.AppId);
            ClassicAssert.IsNull(tracingState.AccountId);
            ClassicAssert.IsNull(tracingState.Guid);
            ClassicAssert.IsNull(tracingState.TraceId);
            ClassicAssert.IsNull(tracingState.TransactionId);
            ClassicAssert.IsNull(tracingState.Sampled);
            ClassicAssert.IsNull(tracingState.Priority);
            ClassicAssert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            ClassicAssert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");

            ClassicAssert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
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

            ClassicAssert.IsNotNull(tracingState);
            ClassicAssert.AreEqual(DistributedTracingParentType.App, tracingState.Type);
            ClassicAssert.AreEqual(AccountId, tracingState.AccountId);
            ClassicAssert.AreEqual(AppId, tracingState.AppId);
            ClassicAssert.AreEqual(Guid, tracingState.Guid);
            ClassicAssert.AreEqual(TraceId, tracingState.TraceId);
            ClassicAssert.AreEqual(Priority, tracingState.Priority);
            ClassicAssert.AreEqual(Sampled, tracingState.Sampled);
            ClassicAssert.AreEqual(TransactionId, tracingState.TransactionId);
            ClassicAssert.AreEqual(ParentId, tracingState.ParentId);
            ClassicAssert.IsTrue(tracingState.Timestamp != default, $"Timestamp should not be {(DateTime)default}");
            ClassicAssert.IsTrue(tracingState.TransportDuration > TimeSpan.Zero, $"TransportDuration should not be Zero");
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

            ClassicAssert.IsNotNull(tracingState);
            ClassicAssert.IsNull(tracingState.AccountId);
            ClassicAssert.IsNull(tracingState.AppId);
            ClassicAssert.IsNull(tracingState.Guid);
            ClassicAssert.IsNull(tracingState.Priority);
            ClassicAssert.IsNull(tracingState.Sampled);
            ClassicAssert.IsNull(tracingState.TransactionId);
            ClassicAssert.AreEqual(string.Join(",", tracingState.VendorStateEntries), "aa=1,bb=2");
            ClassicAssert.AreEqual(TraceId, tracingState.TraceId);
            ClassicAssert.AreEqual(ParentId, tracingState.ParentId);
            ClassicAssert.AreEqual(DistributedTracingParentType.Unknown, tracingState.Type);
            ClassicAssert.AreEqual((DateTime)default, tracingState.Timestamp, $"Timestamp: expected {(DateTime)default}, actual: {tracingState.Timestamp}");
            ClassicAssert.AreEqual(TimeSpan.Zero, tracingState.TransportDuration, $"TransportDuration: expected {TimeSpan.Zero}, actual: {tracingState.TransportDuration}");
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

            ClassicAssert.IsNotNull(tracingState);
            ClassicAssert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.TraceParentParseException), "TracingState IngestErrors should contain TraceParentParseException.");
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
