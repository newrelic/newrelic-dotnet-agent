using NUnit.Framework;
using System;
using System.Collections.Generic;
using NewRelic.Core.DistributedTracing;
using NewRelic.Agent.Core.Utilities;
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
		private const string Guid = "guid";
		private const string TraceId = "traceId";
		private const string TrustKey = "trustKey";
		private const float Priority = .65f;
		private const bool Sampled = true;
		private static DateTime Timestamp = DateTime.UtcNow;
		private const string TransactionId = "transactionId";

		[Test]
		public void AcceptDistributedTraceHeadersHydratesValidNewRelicPayload()
		{
			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(BuildSampleDistributedTracePayload());
			var headers = new Dictionary<string, string>()
			{
				{ "newrelic", encodedPayload }
			};

			var getHeader = new Func<string, IList<string>>((key) =>
			{
				string value;
				headers.TryGetValue(key.ToLowerInvariant(), out value);
				return string.IsNullOrEmpty(value) ? null : new List<string> { value };
			});

			var tracingState = TracingState.AcceptDistributedTraceHeaders(getHeader, TransportType.AMQP, TrustKey);

			Assert.IsNotNull(tracingState);
			Assert.AreEqual(Type, tracingState.Type);
			Assert.AreEqual(AccountId, tracingState.AccountId);
			Assert.AreEqual(AppId, tracingState.AppId);
			Assert.AreEqual(Guid, tracingState.Guid);
			Assert.AreEqual(TraceId, tracingState.TraceId);
			Assert.AreEqual(Priority, tracingState.Priority);
			Assert.AreEqual(Sampled, tracingState.Sampled);
			Assert.AreEqual(TransactionId, tracingState.TransactionId);
		}

		[Test]
		public void AcceptDistributedTracePayloadHydratesValidNewRelicPayload()
		{
			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(BuildSampleDistributedTracePayload());
			var tracingState = TracingState.AcceptDistributedTracePayload(encodedPayload, TransportType.Other, TrustKey);

			Assert.IsNotNull(tracingState);
			Assert.AreEqual(Type, tracingState.Type);
			Assert.AreEqual(AccountId, tracingState.AccountId);
			Assert.AreEqual(AppId, tracingState.AppId);
			Assert.AreEqual(Guid, tracingState.Guid);
			Assert.AreEqual(TraceId, tracingState.TraceId);
			Assert.AreEqual(Priority, tracingState.Priority);
			Assert.AreEqual(Sampled, tracingState.Sampled);
			Assert.AreEqual(TransactionId, tracingState.TransactionId);
		}

		[Test]
		public void AcceptDistributedTracePayloadPopulatesErrorsIfNull()
		{
			string _nullPayload = null;
			var tracingState = TracingState.AcceptDistributedTracePayload(_nullPayload, TransportType.IronMQ, TrustKey);

			Assert.IsNotNull(tracingState);
			Assert.AreEqual(DistributedTracingParentType.None, tracingState.Type);
			Assert.IsNull(tracingState.AppId);
			Assert.IsNull(tracingState.AccountId);
			Assert.IsNull(tracingState.Guid);
			Assert.IsNull(tracingState.TraceId);
			Assert.IsNull(tracingState.TransactionId);
			Assert.IsNull(tracingState.Sampled);
			Assert.IsNull(tracingState.Priority);

			Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.NullPayload), "TracingState IngestErrors should contain NullPayload");
		}

		[Test]
		public void AcceptDistributedTracePayloadPopulatesErrorsIfUnsupportedVersion()
		{
			// v:[2,5]
			var serializedUnencodedPayload = "{ \"v\":[2,5],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
			var encodedPayload = Strings.Base64Encode(serializedUnencodedPayload);
			var tracingState = TracingState.AcceptDistributedTracePayload(encodedPayload, TransportType.Other, TrustKey);

			Assert.IsNotNull(tracingState);
			Assert.AreEqual(DistributedTracingParentType.None, tracingState.Type);
			Assert.IsNull(tracingState.AppId);
			Assert.IsNull(tracingState.AccountId);
			Assert.IsNull(tracingState.Guid);
			Assert.IsNull(tracingState.TraceId);
			Assert.IsNull(tracingState.TransactionId);
			Assert.IsNull(tracingState.Sampled);
			Assert.IsNull(tracingState.Priority);

			Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.Version), "TracingState IngestErrors should contain Version error.");
		}

		[Test]
		public void AcceptDistributedTracePayloadPopulatesErrorsIfInvalidTimestamp()
		{
			// ti:0
			var serializedUnencodedPayload = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
			var encodedPayload = Strings.Base64Encode(serializedUnencodedPayload);
			var tracingState = TracingState.AcceptDistributedTracePayload(encodedPayload, TransportType.Other, TrustKey);

			Assert.IsNotNull(tracingState);
			Assert.AreEqual(DistributedTracingParentType.None, tracingState.Type);
			Assert.IsNull(tracingState.AppId);
			Assert.IsNull(tracingState.AccountId);
			Assert.IsNull(tracingState.Guid);
			Assert.IsNull(tracingState.TraceId);
			Assert.IsNull(tracingState.TransactionId);
			Assert.IsNull(tracingState.Sampled);
			Assert.IsNull(tracingState.Priority);

			Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
		}

		[Test]
		public void AcceptDistributedTracePayloadPopulatesErrorsIfNotTraceable()
		{
			// missing tx: AND id:
			var serializedUnencodedPayload = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\"}}";
			var encodedPayload = Strings.Base64Encode(serializedUnencodedPayload);
			var tracingState = TracingState.AcceptDistributedTracePayload(encodedPayload, TransportType.Other, TrustKey);

			Assert.IsNotNull(tracingState);
			Assert.AreEqual(DistributedTracingParentType.None, tracingState.Type);
			Assert.IsNull(tracingState.AppId);
			Assert.IsNull(tracingState.AccountId);
			Assert.IsNull(tracingState.Guid);
			Assert.IsNull(tracingState.TraceId);
			Assert.IsNull(tracingState.TransactionId);
			Assert.IsNull(tracingState.Sampled);
			Assert.IsNull(tracingState.Priority);

			Assert.IsTrue(tracingState.IngestErrors.Contains(IngestErrorType.ParseException), "TracingState IngestErrors should contain ParseException.");
		}

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

#endregion helpers
	}
}
