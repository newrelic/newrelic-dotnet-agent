// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transactions
{
    public class TestImmutableTransactionMetadata : IImmutableTransactionMetadata
    {
        public string RequestMethod { get; }

        public string Uri { get; }
        public string OriginalUri { get; }
        public string ReferrerUri { get; }

        public TimeSpan? QueueTime { get; }

        public int? HttpResponseStatusCode { get; }

        public AttributeValueCollection UserAndRequestAttributes { get; }

        public IEnumerable<string> CrossApplicationAlternatePathHashes { get; }

        public string CrossApplicationReferrerTransactionGuid { get; }

        public string CrossApplicationReferrerPathHash { get; }

        public string CrossApplicationPathHash { get; }

        public string CrossApplicationReferrerProcessId { get; }
        public string CrossApplicationReferrerTripId { get; }

        public float CrossApplicationResponseTimeInSeconds { get; }

        public bool HasOutgoingTraceHeaders { get; }

        public int? HttpResponseSubStatusCode { get; }

        public string SyntheticsResourceId { get; }
        public string SyntheticsJobId { get; }
        public string SyntheticsMonitorId { get; }
        public bool IsSynthetics { get; }
        public bool HasCatResponseHeaders { get; }
        public float Priority { get; }

        public bool IsLlmTransaction { get; }

        public IReadOnlyTransactionErrorState ReadOnlyTransactionErrorState { get; }

        public TestImmutableTransactionMetadata(
            string uri,
            string originalUri,
            string referrerUri,
            TimeSpan? queueTime,
            AttributeValueCollection userAndRequestAttributes,
            ITransactionErrorState transactionErrorState,
            int? httpResponseStatusCode,
            int? httpResponseSubStatusCode,
            string crossApplicationReferrerPathHash,
            string crossApplicationPathHash,
            IEnumerable<string> crossApplicationPathHashes,
            string crossApplicationReferrerTransactionGuid,
            string crossApplicationReferrerProcessId,
            string crossApplicationReferrerTripId,
            float crossApplicationResponseTimeInSeconds,
            bool hasOutgoingTraceHeaders,
            string syntheticsResourceId,
            string syntheticsJobId,
            string syntheticsMonitorId,
            bool isSynthetics,
            bool hasCatResponseHeaders,
            float priority)
        {
            Uri = uri;
            OriginalUri = originalUri;
            ReferrerUri = referrerUri;
            QueueTime = queueTime;

            UserAndRequestAttributes = userAndRequestAttributes;

            ReadOnlyTransactionErrorState = transactionErrorState;

            HttpResponseStatusCode = httpResponseStatusCode;
            HttpResponseSubStatusCode = httpResponseSubStatusCode;
            CrossApplicationReferrerPathHash = crossApplicationReferrerPathHash;
            CrossApplicationPathHash = crossApplicationPathHash;
            CrossApplicationAlternatePathHashes = crossApplicationPathHashes.ToList();
            CrossApplicationReferrerTransactionGuid = crossApplicationReferrerTransactionGuid;
            CrossApplicationReferrerProcessId = crossApplicationReferrerProcessId;
            CrossApplicationReferrerTripId = crossApplicationReferrerTripId;
            CrossApplicationResponseTimeInSeconds = crossApplicationResponseTimeInSeconds;
            HasOutgoingTraceHeaders = hasOutgoingTraceHeaders;
            SyntheticsResourceId = syntheticsResourceId;
            SyntheticsJobId = syntheticsJobId;
            SyntheticsMonitorId = syntheticsMonitorId;
            IsSynthetics = isSynthetics;
            HasCatResponseHeaders = hasCatResponseHeaders;
            Priority = priority;
        }
    }


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

        private string _distributedTraceGuid;
        private string _distributedTraceTraceId;
        private bool _distributedTraceSampled = false;
        private bool _hasIncomingDistributedTracePayload;

        public ImmutableTransactionBuilder WithDistributedTracing(string distributedTraceGuid, string distributedTraceTraceId, bool distributedTraceSampled, bool hasIncomingDistributedTracePayload)
        {
            _distributedTraceGuid = distributedTraceGuid;
            _distributedTraceTraceId = distributedTraceTraceId;
            _distributedTraceSampled = distributedTraceSampled;
            _hasIncomingDistributedTracePayload = hasIncomingDistributedTracePayload;
            return this;
        }

        private DistributedTracing.ITracingState _tracingState;

        public ImmutableTransactionBuilder WithW3CTracing(string guid, string parentId, List<string> vendorStateEntries)
        {
            _tracingState = Mock.Create<DistributedTracing.ITracingState>();

            Mock.Arrange(() => _tracingState.Guid).Returns(guid);
            Mock.Arrange(() => _tracingState.ParentId).Returns(parentId);
            Mock.Arrange(() => _tracingState.VendorStateEntries).Returns(vendorStateEntries);
            Mock.Arrange(() => _tracingState.HasDataForParentAttributes).Returns(true);
            Mock.Arrange(() => _tracingState.Timestamp).Returns(DateTime.UtcNow);
            Mock.Arrange(() => _tracingState.TransportDuration).Returns(TimeSpan.FromMilliseconds(1));
            Mock.Arrange(() => _tracingState.AccountId).Returns("accountId");
            Mock.Arrange(() => _tracingState.AppId).Returns("appId");
            Mock.Arrange(() => _tracingState.TransportType).Returns(Extensions.Providers.Wrapper.TransportType.Kafka);

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
        private List<Segment> _segments = new List<Segment>() { SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(TimeSpan.Zero, TimeSpan.Zero, 0, null, new MethodCallData("typeName", "methodName", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "MyMockedRootNode", false) };

        public ImmutableTransactionBuilder WithSegments(List<Segment> segments)
        {
            _segments = segments;
            return this;
        }

        public ImmutableTransactionBuilder WithExceptionFromSegment(Segment segmentWithError)
        {
            _transactionErrorState.AddExceptionData(segmentWithError.ErrorData);
            _transactionErrorState.TrySetSpanIdForErrorData(segmentWithError.ErrorData, segmentWithError.SpanId);
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

        private ITransactionErrorState _transactionErrorState = new TransactionErrorState();

        public ImmutableTransaction Build()
        {
            var metadata = new TestImmutableTransactionMetadata(
                uri: "uri",
                originalUri: "originalUri",
                referrerUri: "referrerUri",
                queueTime: new TimeSpan(1),
                userAndRequestAttributes: new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes),
                transactionErrorState: _transactionErrorState,
                httpResponseStatusCode: 200,
                httpResponseSubStatusCode: 201,
                crossApplicationReferrerPathHash: _crossApplicationReferrerPathHash,
                crossApplicationPathHash: _crossApplicationPathHash,
                crossApplicationPathHashes: new List<string>(),
                crossApplicationReferrerTransactionGuid: _crossApplicationReferrerTransactionGuid,
                crossApplicationReferrerProcessId: _crossApplicationReferrerProcessId,
                crossApplicationReferrerTripId: _crossApplicationReferrerTripId,
                crossApplicationResponseTimeInSeconds: _crossApplicationResponseTimeInSeconds,
                hasOutgoingTraceHeaders: false,
                syntheticsResourceId: "syntheticsResourceId",
                syntheticsJobId: "syntheticsJobId",
                syntheticsMonitorId: "syntheticsMonitorId",
                isSynthetics: false,
                hasCatResponseHeaders: false,
                priority: _priority);;

            var attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            return new ImmutableTransaction(_transactionName, _segments, metadata, _startTime, _duration, _responseTime, _transactionGuid, true, true, false, 0.5f, _distributedTraceSampled, _distributedTraceTraceId, _tracingState, attribDefSvc.AttributeDefs);
        }
    }
}
