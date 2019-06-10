using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Transactions
{
	public class ImmutableTransactionBuilder
	{
		private ITransactionName _transactionName = TransactionName.ForWebTransaction("foo", "bar");

		public ImmutableTransactionBuilder IsWebTransaction(string category, string name)
		{
			_transactionName = TransactionName.ForWebTransaction(category, name);
			return this;
		}

		public ImmutableTransactionBuilder IsOtherTransaction(string category, string name)
		{
			_transactionName = TransactionName.ForOtherTransaction(category, name);
			return this;
		}

		private float _priority = 0.5f;

		public ImmutableTransactionBuilder WithPriority(float priority)
		{
			_priority = priority;
			return this;
		}

		private ConcurrentDictionary<string, object> _userErrorAttributes = new ConcurrentDictionary<string, object>();

		public ImmutableTransactionBuilder WithUserErrorAttribute(string attributeKey, object attributeValue)
		{
			_userErrorAttributes.TryAdd(attributeKey, attributeValue);
			return this;
		}

		private string _distributedTraceGuid;
		private string _distributedTraceTraceId;
		private bool _distributedTraceSampled;
		private bool _hasIncomingDistributedTracePayload;

		public ImmutableTransactionBuilder WithDistributedTracing(string distributedTraceGuid, string distributedTraceTraceId, bool distributedTraceSampled, bool hasIncomingDistributedTracePayload)
		{
			_distributedTraceGuid = distributedTraceGuid;
			_distributedTraceTraceId = distributedTraceTraceId;
			_distributedTraceSampled = distributedTraceSampled;
			_hasIncomingDistributedTracePayload = hasIncomingDistributedTracePayload;
			return this;
		}

		private DateTime _startTime = new DateTime(2018, 7, 18, 7, 0, 0, DateTimeKind.Utc); // unixtime = 1531897200000

		public ImmutableTransactionBuilder WithStartTime(DateTime startTime)
		{
			_startTime = startTime;
			return this;
		}

		private string _transactionGuid = GuidGenerator.GenerateNewRelicGuid();

		public ImmutableTransactionBuilder WithTransactionGuid(string transactionGuid)
		{
			_transactionGuid = transactionGuid;
			return this;
		}

		//Transactions should always have a root segment
		private List<Segment> _segments = new List<Segment>() { SimpleSegmentDataTests.createSimpleSegmentBuilder(TimeSpan.Zero, TimeSpan.Zero, 0, null, null, Enumerable.Empty<KeyValuePair<string, object>>(), "MyMockedRootNode", false) };

		public ImmutableTransactionBuilder WithSegments(List<Segment> segments)
		{
			_segments = segments;
			return this;
		}

		private TimeSpan _duration = TimeSpan.FromSeconds(1);

		public ImmutableTransactionBuilder WithDuration(TimeSpan duration)
		{
			_duration = duration;
			return this;
		}

		private TimeSpan? _responseTime = null;

		public ImmutableTransactionBuilder WithResponseTime(TimeSpan responseTime)
		{
			_responseTime = responseTime;
			return this;
		}

		public ImmutableTransactionBuilder WithNoResponseTime()
		{
			_responseTime = null;
			return this;
		}

		private string _crossApplicationReferrerPathHash;
		private string _crossApplicationPathHash;
		private List<string> _crossApplicationPathHashes = new List<string>();
		private string _crossApplicationReferrerTransactionGuid;
		private string _crossApplicationReferrerProcessId;
		private string _crossApplicationReferrerTripId;
		private float _crossApplicationResponseTimeInSeconds;

		public ImmutableTransactionBuilder WithCrossApplicationData(string crossApplicationReferrerPathHash = "crossApplicationReferrerPathHash", string crossApplicationPathHash = "crossApplicationPathHash", List<string> crossApplicationPathHashes = null, string crossApplicationReferrerTransactionGuid = "crossApplicationReferrerTransactionGuid", string crossApplicationReferrerProcessId = "crossApplicationReferrerProcessId", string crossApplicationReferrerTripId = "crossApplicationReferrerTripId", float crossApplicationResponseTimeInSeconds = 0)
		{
			_crossApplicationReferrerPathHash = crossApplicationReferrerPathHash;
			_crossApplicationPathHash = crossApplicationPathHash;
			_crossApplicationPathHashes = crossApplicationPathHashes ?? new List<string>();
			_crossApplicationReferrerTransactionGuid = crossApplicationReferrerTransactionGuid;
			_crossApplicationReferrerProcessId = crossApplicationReferrerProcessId;
			_crossApplicationReferrerTripId = crossApplicationReferrerTripId;
			_crossApplicationResponseTimeInSeconds = crossApplicationResponseTimeInSeconds;
			return this;
		}

		public ImmutableTransaction Build()
		{
			var metadata = new ImmutableTransactionMetadata(
				uri: "uri",
				originalUri: "originalUri",
				referrerUri: "referrerUri",
				queueTime: new TimeSpan(1),
				requestParameters: new ConcurrentDictionary<string, string>(),
				userAttributes: new ConcurrentDictionary<string, object>(),
				userErrorAttributes: _userErrorAttributes,
				httpResponseStatusCode: 200,
				httpResponseSubStatusCode: 201,
				transactionExceptionDatas: new List<ErrorData>(),
				customErrorDatas: new List<ErrorData>(),
				crossApplicationReferrerPathHash: _crossApplicationReferrerPathHash,
				crossApplicationPathHash: _crossApplicationPathHash,
				crossApplicationPathHashes: new List<string>(),
				crossApplicationReferrerTransactionGuid: _crossApplicationReferrerTransactionGuid,
				crossApplicationReferrerProcessId: _crossApplicationReferrerProcessId,
				crossApplicationReferrerTripId: _crossApplicationReferrerTripId,
				crossApplicationResponseTimeInSeconds: _crossApplicationResponseTimeInSeconds,
				distributedTraceType: "distributedTraceType",
				distributedTraceAppId: "distributedTraceApp",
				distributedTraceAccountId: "distributedTraceAccount",
				distributedTraceTransportType: "distributedTraceTransportType",
				distributedTraceGuid: _distributedTraceGuid,
				distributedTraceTransportDuration: TimeSpan.MinValue,
				distributedTraceTraceId: _distributedTraceTraceId,
				distributedTraceTransactionId: "distributedTransactionId",
				distributedTraceTrustKey: "distributedTraceTrustKey",
				distributedTraceSampled: _distributedTraceSampled,
				hasOutgoingDistributedTracePayload: false,
				hasIncomingDistributedTracePayload: _hasIncomingDistributedTracePayload,
				syntheticsResourceId: "syntheticsResourceId",
				syntheticsJobId: "syntheticsJobId",
				syntheticsMonitorId: "syntheticsMonitorId",
				isSynthetics: false,
				hasCatResponseHeaders: false,
				priority: _priority);

			return new ImmutableTransaction(_transactionName, _segments, metadata, _startTime, _duration, _responseTime, _transactionGuid, true, true,
				false, SqlObfuscator.GetObfuscatingSqlObfuscator());
		}
	}
}
