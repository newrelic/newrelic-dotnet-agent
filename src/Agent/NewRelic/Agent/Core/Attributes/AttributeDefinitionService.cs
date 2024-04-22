// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using NewRelic.Core;
using NewRelic.Parsing;
using NewRelic.Core.CodeAttributes;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Events;
using System.Linq;
using System;
using NewRelic.Core.DistributedTracing;

namespace NewRelic.Agent.Core.Attributes
{
    public interface IAttributeDefinitionService : IDisposable
    {
        IAttributeDefinitions AttributeDefs { get; }
    }

    public interface IAttributeDefinitions
    {
        AttributeDefinition<string, string> ApdexPerfZone { get; }
        AttributeDefinition<string, string> BrowserTripId { get; }
        AttributeDefinition<IEnumerable<string>, string> CatAlternativePathHashes { get; }
        AttributeDefinition<string, string> CatNrPathHash { get; }
        AttributeDefinition<string, string> CatNrTripId { get; }
        AttributeDefinition<string, string> CatPathHash { get; }
        AttributeDefinition<string, string> CatReferringPathHash { get; }
        AttributeDefinition<string, string> CatReferringTransactionGuidForEvents { get; }
        AttributeDefinition<string, string> CatReferringTransactionGuidForTraces { get; }
        AttributeDefinition<string, string> ClientCrossProcessId { get; }
        AttributeDefinition<string, string> CodeFunction { get; }
        AttributeDefinition<string, string> CodeNamespace { get; }
        AttributeDefinition<string, string> Component { get; }
        AttributeDefinition<TimeSpan, double> CpuTime { get; }
        AttributeDefinition<string, string> CustomEventType { get; }
        AttributeDefinition<long, double> DatabaseCallCount { get; }
        AttributeDefinition<float, double> DatabaseDuration { get; }
        AttributeDefinition<string, string> DbCollection { get; }
        AttributeDefinition<string, string> DbInstance { get; }
        AttributeDefinition<string, string> DbOperation { get; }
        AttributeDefinition<string, string> DbServerAddress { get; }
        AttributeDefinition<long, long> DbServerPort { get; }
        AttributeDefinition<string, string> DbStatement { get; }
        AttributeDefinition<string, string> DbSystem { get; }
        AttributeDefinition<string, string> DistributedTraceId { get; }
        AttributeDefinition<TimeSpan, double> Duration { get; }
        AttributeDefinition<bool, bool> IsErrorExpected { get; }
        AttributeDefinition<bool, bool> SpanIsErrorExpected { get; }
        AttributeDefinition<string, string> ErrorClass { get; }
        AttributeDefinition<string, string> ErrorDotMessage { get; }
        AttributeDefinition<string, string> ErrorMessage { get; }
        AttributeDefinition<string, string> ErrorType { get; }
        AttributeDefinition<string, string> ErrorEventSpanId { get; }
        AttributeDefinition<string, string> ErrorGroup { get; }
        AttributeDefinition<string, string> EndUserId { get; }
        AttributeDefinition<float, double> ExternalCallCount { get; }
        AttributeDefinition<float, double> ExternalDuration { get; }
        AttributeDefinition<string, string> Guid { get; }
        AttributeDefinition<string, string> HostDisplayName { get; }
        AttributeDefinition<string, string> HttpMethod { get; }
        AttributeDefinition<long?, long> HttpStatusCode { get; }
        AttributeDefinition<Uri, string> HttpUrl { get; }
        AttributeDefinition<bool, bool> IsError { get; }
        AttributeDefinition<string, string> NameForSpan { get; }
        AttributeDefinition<bool, bool> NrEntryPoint { get; }
        AttributeDefinition<string, string> NrGuid { get; }
        AttributeDefinition<string, string> OriginalUrl { get; }
        AttributeDefinition<string, string> ParentAccount { get; }
        AttributeDefinition<string, string> ParentAccountForSpan { get; }
        AttributeDefinition<string, string> ParentApp { get; }
        AttributeDefinition<string, string> ParentAppForSpan { get; }
        AttributeDefinition<string, string> ParentId { get; }
        AttributeDefinition<string, string> ParentSpanId { get; }
        AttributeDefinition<TimeSpan, double> ParentTransportDuration { get; }
        AttributeDefinition<TimeSpan, double> ParentTransportDurationForSpan { get; }
        AttributeDefinition<TransportType, string> ParentTransportType { get; }
        AttributeDefinition<TransportType, string> ParentTransportTypeForSpan { get; }
        AttributeDefinition<TypeAttributeValue, string> ParentType { get; }
        AttributeDefinition<TypeAttributeValue, string> ParentTypeForSpan { get; }
        AttributeDefinition<DistributedTracingParentType, string> ParentTypeForDistributedTracing { get; }
        AttributeDefinition<DistributedTracingParentType, string> ParentTypeForDistributedTracingForSpan { get; }
        AttributeDefinition<string, string> PeerAddress { get; }
        AttributeDefinition<string, string> PeerHostname { get; }
        AttributeDefinition<float, double> Priority { get; }
        AttributeDefinition<TimeSpan?, double> QueueDuration { get; }
        AttributeDefinition<TimeSpan?, string> QueueWaitTime { get; }
        AttributeDefinition<string, string> RequestReferrer { get; }
        AttributeDefinition<string, string> RequestMethod { get; }
        AttributeDefinition<string, string> RequestUri { get; }
        AttributeDefinition<int?, string> ResponseStatus { get; }
        AttributeDefinition<bool, bool> Sampled { get; }
        AttributeDefinition<string, string> ServerAddress { get; }
        AttributeDefinition<long, long> ServerPort { get; }
        AttributeDefinition<SpanCategory, string> SpanCategory { get; }
        AttributeDefinition<string, string> SpanErrorClass { get; }
        AttributeDefinition<string, string> SpanErrorMessage { get; }
        AttributeDefinition<string, string> SpanId { get; }
        AttributeDefinition<string, string> SpanKind { get; }
        AttributeDefinition<string, string> SyntheticsJobId { get; }
        AttributeDefinition<string, string> SyntheticsJobIdForTraces { get; }
        AttributeDefinition<string, string> SyntheticsMonitorId { get; }
        AttributeDefinition<string, string> SyntheticsMonitorIdForTraces { get; }
        AttributeDefinition<string, string> SyntheticsResourceId { get; }
        AttributeDefinition<string, string> SyntheticsResourceIdForTraces { get; }
        AttributeDefinition<long, long> ThreadId { get; }
        AttributeDefinition<DateTime, long> Timestamp { get; }
        AttributeDefinition<DateTime, long> TimestampForError { get; }
        AttributeDefinition<TimeSpan, double> TotalTime { get; }
        AttributeDefinition<string, string> TransactionId { get; }
        AttributeDefinition<IEnumerable<string>, string> TracingVendors { get; }
        AttributeDefinition<string, string> TransactionName { get; }
        AttributeDefinition<string, string> TransactionNameForSpan { get; }
        AttributeDefinition<string, string> TransactionNameForError { get; }
        AttributeDefinition<string, string> TripId { get; }
        AttributeDefinition<string, string> TrustedParentId { get; }
        AttributeDefinition<TimeSpan, double> WebDuration { get; }

        AttributeDefinition<object, object> GetCustomAttributeForCustomEvent(string name);
        AttributeDefinition<object, object> GetCustomAttributeForError(string name);
        AttributeDefinition<object, object> GetCustomAttributeForSpan(string name);
        AttributeDefinition<object, object> GetCustomAttributeForTransaction(string name);

        AttributeDefinition<object, object> GetLambdaAttribute(string name);

        AttributeDefinition<string, string> GetRequestParameterAttribute(string paramName);

        AttributeDefinition<string, string> GetRequestHeadersAttribute(string paramName);

        AttributeDefinition<TypeAttributeValue, string> GetTypeAttribute(TypeAttributeValue destination);

        AttributeDefinition<bool, bool> LlmTransaction { get; }
    }


    public class AttributeDefinitionService : ConfigurationBasedService, IAttributeDefinitionService
    {
        public IAttributeDefinitions AttributeDefs { get; private set; }

        private readonly Func<IAttributeFilter, IAttributeDefinitions> _attribDefinitionsFactory;

        public AttributeDefinitionService(Func<IAttributeFilter, IAttributeDefinitions> attribDefinitionFactory)
        {
            _attribDefinitionsFactory = attribDefinitionFactory;
            ResetAttributeDefinitions();
        }

        private void ResetAttributeDefinitions()
        {
            var filterSettings = new AttributeFilter.Settings(_configuration);
            var filter = new AttributeFilter(filterSettings);
            AttributeDefs = _attribDefinitionsFactory(filter);
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            ResetAttributeDefinitions();
        }
    }

    public class AttributeDefinitions : IAttributeDefinitions
    {
        private readonly IAttributeFilter _attribFilter;

        public AttributeDefinitions(IAttributeFilter attribFilter)
        {
            _attribFilter = attribFilter;
        }

        private readonly ConcurrentDictionary<string, AttributeDefinition<object, object>> _trxCustomAttributes = new ConcurrentDictionary<string, AttributeDefinition<object, object>>();
        private readonly ConcurrentDictionary<string, AttributeDefinition<object, object>> _spanCustomAttributes = new ConcurrentDictionary<string, AttributeDefinition<object, object>>();
        private readonly ConcurrentDictionary<string, AttributeDefinition<object, object>> _errorCustomAttributes = new ConcurrentDictionary<string, AttributeDefinition<object, object>>();
        private readonly ConcurrentDictionary<string, AttributeDefinition<object, object>> _customEventCustomAttributes = new ConcurrentDictionary<string, AttributeDefinition<object, object>>();
        private readonly ConcurrentDictionary<string, AttributeDefinition<string, string>> _requestParameterAttributes = new ConcurrentDictionary<string, AttributeDefinition<string, string>>();
        private readonly ConcurrentDictionary<string, AttributeDefinition<string, string>> _requestHeadersAttributes = new ConcurrentDictionary<string, AttributeDefinition<string, string>>();
        private readonly ConcurrentDictionary<string, AttributeDefinition<object, object>> _lambdaAttributes = new ConcurrentDictionary<string, AttributeDefinition<object, object>>();
        
        private readonly ConcurrentDictionary<TypeAttributeValue, AttributeDefinition<TypeAttributeValue, string>> _typeAttributes = new ConcurrentDictionary<TypeAttributeValue, AttributeDefinition<TypeAttributeValue, string>>();


        private AttributeDefinition<object, object> CreateCustomAttributeForTransaction(string name)
        {
            return AttributeDefinitionBuilder
                .CreateCustomAttribute(name, AttributeDestinations.All)
                .Build(_attribFilter);
        }

        private AttributeDefinition<object, object> CreateCustomAttributeForCustomEvent(string name)
        {
            return AttributeDefinitionBuilder
                .CreateCustomAttribute(name, AttributeDestinations.CustomEvent)
                .Build(_attribFilter);
        }

        private AttributeDefinition<object, object> CreateCustomAttributeForError(string name)
        {
            return AttributeDefinitionBuilder
                .CreateCustomAttribute(name, AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace)
                .Build(_attribFilter);
        }

        private AttributeDefinition<object, object> CreateCustomAttributeForSpan(string name)
        {
            return AttributeDefinitionBuilder
                .CreateCustomAttribute(name, AttributeDestinations.SpanEvent)
                .Build(_attribFilter);
        }

        private AttributeDefinition<string, string> CreateRequestParameterAttribute(string paramName)
        {
            var attribName = $"request.parameters.{paramName}";

            return AttributeDefinitionBuilder
                .CreateString(attribName, AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.TransactionEvent, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.TransactionEvent))
                .AppliesTo(AttributeDestinations.TransactionTrace, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.TransactionTrace))
                .AppliesTo(AttributeDestinations.ErrorTrace, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.ErrorTrace))
                .AppliesTo(AttributeDestinations.ErrorEvent, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.ErrorEvent))
                .AppliesTo(AttributeDestinations.SpanEvent, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.SpanEvent))
                .Build(_attribFilter);
        }

        private AttributeDefinition<string, string> CreateRequestHeadersAttribute(string paramName)
        {
            var attribName = $"request.headers.{paramName}";

            return AttributeDefinitionBuilder
                .CreateString(attribName, AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.TransactionEvent, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.TransactionEvent))
                .AppliesTo(AttributeDestinations.TransactionTrace, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.TransactionTrace))
                .AppliesTo(AttributeDestinations.ErrorTrace, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.ErrorTrace))
                .AppliesTo(AttributeDestinations.ErrorEvent, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.ErrorEvent))
                .AppliesTo(AttributeDestinations.SpanEvent, _attribFilter.CheckOrAddAttributeClusionCache(attribName, AttributeDestinations.None, AttributeDestinations.SpanEvent))
                .Build(_attribFilter);
        }

        private AttributeDefinition<object, object> CreateLambdaAttribute(string attribName)
        {
            return AttributeDefinitionBuilder
                .Create<object, object>(attribName, AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter);
        }

        public AttributeDefinition<object, object> GetLambdaAttribute(string name)
        {
            return _lambdaAttributes.GetOrAdd(name, CreateLambdaAttribute);
        }

        public AttributeDefinition<object, object> GetCustomAttributeForTransaction(string name)
        {
            return _trxCustomAttributes.GetOrAdd(name, CreateCustomAttributeForTransaction);
        }

        public AttributeDefinition<object, object> GetCustomAttributeForCustomEvent(string name)
        {
            return _customEventCustomAttributes.GetOrAdd(name, CreateCustomAttributeForCustomEvent);
        }

        public AttributeDefinition<object, object> GetCustomAttributeForError(string name)
        {
            return _errorCustomAttributes.GetOrAdd(name, CreateCustomAttributeForError);
        }

        public AttributeDefinition<object, object> GetCustomAttributeForSpan(string name)
        {
            return _spanCustomAttributes.GetOrAdd(name, CreateCustomAttributeForSpan);
        }

        public AttributeDefinition<string, string> GetRequestParameterAttribute(string paramName)
        {
            return _requestParameterAttributes.GetOrAdd(paramName, CreateRequestParameterAttribute);
        }

        public AttributeDefinition<string, string> GetRequestHeadersAttribute(string paramName)
        {
            return _requestHeadersAttributes.GetOrAdd(paramName, CreateRequestHeadersAttribute);
        }

        private AttributeDefinition<TypeAttributeValue, string> CreateTypeAttribute(TypeAttributeValue tm)
        {
            var val = EnumNameCache<TypeAttributeValue>.GetName(tm);

            var dest = AttributeDestinations.None;
            switch (tm)
            {
                case TypeAttributeValue.Transaction:
                    dest = AttributeDestinations.TransactionEvent;
                    break;

                case TypeAttributeValue.TransactionError:
                    dest = AttributeDestinations.ErrorEvent;
                    break;

                case TypeAttributeValue.Span:
                    dest = AttributeDestinations.SpanEvent;
                    break;
            }

            return AttributeDefinitionBuilder.CreateString<TypeAttributeValue>("type", AttributeClassification.Intrinsics)
                .AppliesTo(dest)
                .WithDefaultOutputValue(val)
                .WithConvert((target) => val)
                .Build(_attribFilter);
        }

        public AttributeDefinition<TypeAttributeValue, string> GetTypeAttribute(TypeAttributeValue targetModel)
        {
            return _typeAttributes.GetOrAdd(targetModel, CreateTypeAttribute);
        }

        private AttributeDefinition<TimeSpan?, string> _queueWaitTime;
        public AttributeDefinition<TimeSpan?, string> QueueWaitTime => _queueWaitTime ?? (_queueWaitTime =
            AttributeDefinitionBuilder.CreateString<TimeSpan?>("queue_wait_time_ms", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert((v) => v.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture))
                .Build(_attribFilter));

        private AttributeDefinition<TimeSpan?, double> _queueDuration;
        public AttributeDefinition<TimeSpan?, double> QueueDuration => _queueDuration ?? (_queueDuration =
            AttributeDefinitionBuilder.CreateDouble<TimeSpan?>("queueDuration", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert((v) => v.Value.TotalSeconds)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _originalUrl;
        public AttributeDefinition<string, string> OriginalUrl => _originalUrl ?? (_originalUrl =
            AttributeDefinitionBuilder.CreateString("original_url", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.JavaScriptAgent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _requestMethod;
        public AttributeDefinition<string, string> RequestMethod => _requestMethod ?? (_requestMethod =
            AttributeDefinitionBuilder.CreateString("request.method", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _requestUri;
        public AttributeDefinition<string, string> RequestUri => _requestUri ?? (_requestUri =
            AttributeDefinitionBuilder.CreateString("request.uri", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .WithDefaultOutputValue("/unknown")
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _requestReferrer;
        public AttributeDefinition<string, string> RequestReferrer => _requestReferrer ?? (_requestReferrer =
            AttributeDefinitionBuilder.CreateString("request.referer", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        [ToBeRemovedInFutureRelease("To be removed v9+. Use BuildHttpStatusCodeAttribute instead.")]
        private AttributeDefinition<int?, string> _responseStatus;
        public AttributeDefinition<int?, string> ResponseStatus => _responseStatus ?? (_responseStatus =
            AttributeDefinitionBuilder.CreateString<int?>("response.status", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .WithConvert(x => x.ToString())
                .Build(_attribFilter));

        private AttributeDefinition<long?, long> _httpStatusCode;
        public AttributeDefinition<long?, long> HttpStatusCode => _httpStatusCode ?? (_httpStatusCode =
            AttributeDefinitionBuilder.CreateLong<long?>("http.statusCode", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .WithConvert(x => x.GetValueOrDefault())                //This is ok b/c we check for null input earlier
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _clientCrossProcessId;
        public AttributeDefinition<string, string> ClientCrossProcessId => _clientCrossProcessId ?? (_clientCrossProcessId =
            AttributeDefinitionBuilder.CreateString("client_cross_process_id", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _tripId;        //TripUnderscoreId				
        public AttributeDefinition<string, string> TripId => _tripId ?? (_tripId =
            AttributeDefinitionBuilder.CreateString("trip_id", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _catNrTripId;
        public AttributeDefinition<string, string> CatNrTripId => _catNrTripId ?? (_catNrTripId =
            AttributeDefinitionBuilder.CreateString("nr.tripId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _browserTripId;
        public AttributeDefinition<string, string> BrowserTripId => _browserTripId ?? (_browserTripId =
            AttributeDefinitionBuilder.CreateString("nr.tripId", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.JavaScriptAgent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _catPathHash;
        public AttributeDefinition<string, string> CatPathHash => _catPathHash ?? (_catPathHash =
            AttributeDefinitionBuilder.CreateString("path_hash", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _catNrPathHash;
        public AttributeDefinition<string, string> CatNrPathHash => _catNrPathHash ?? (_catNrPathHash =
            AttributeDefinitionBuilder.CreateString("nr.pathHash", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _catReferringPathHash;
        public AttributeDefinition<string, string> CatReferringPathHash => _catReferringPathHash ?? (_catReferringPathHash =
            AttributeDefinitionBuilder.CreateString("nr.referringPathHash", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _catReferringTransactionGuidForTraces;
        public AttributeDefinition<string, string> CatReferringTransactionGuidForTraces => _catReferringTransactionGuidForTraces ?? (_catReferringTransactionGuidForTraces =
            AttributeDefinitionBuilder.CreateString("referring_transaction_guid", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _catReferringTransactionGuidForEvents;
        public AttributeDefinition<string, string> CatReferringTransactionGuidForEvents => _catReferringTransactionGuidForEvents ?? (_catReferringTransactionGuidForEvents =
            AttributeDefinitionBuilder.CreateString("nr.referringTransactionGuid", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<IEnumerable<string>, string> _catAlternativePathHashes;
        public AttributeDefinition<IEnumerable<string>, string> CatAlternativePathHashes => _catAlternativePathHashes ?? (_catAlternativePathHashes =
            AttributeDefinitionBuilder.CreateString<IEnumerable<string>>("nr.alternatePathHashes", AttributeClassification.Intrinsics)
                .WithConvert((hashes) =>
                {
                    if (hashes == null)
                    {
                        return null;
                    }

                    var hashesArray = hashes.OrderBy(x => x).ToArray();
                    if (hashesArray.Length == 0)
                    {
                        return null;
                    }

                    return string.Join(",", hashesArray);

                })
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _transactionId;
        public AttributeDefinition<string, string> TransactionId => _transactionId ?? (_transactionId =
            AttributeDefinitionBuilder.CreateString("transactionId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _nameForSpan;
        public AttributeDefinition<string, string> NameForSpan => _nameForSpan ?? (_nameForSpan =
            AttributeDefinitionBuilder.CreateString("name", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<SpanCategory, string> _spanCategory;
        public AttributeDefinition<SpanCategory, string> SpanCategory => _spanCategory ?? (_spanCategory =
            AttributeDefinitionBuilder.CreateString<SpanCategory>("category", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert(v => EnumNameCache<SpanCategory>.GetNameToLower(v))
                .Build(_attribFilter));

        private AttributeDefinition<bool, bool> _nrEntryPoint;
        public AttributeDefinition<bool, bool> NrEntryPoint => _nrEntryPoint ?? (_nrEntryPoint =
            AttributeDefinitionBuilder.CreateBool("nr.entryPoint", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<long, long> _threadId;
        public AttributeDefinition<long, long> ThreadId => _threadId ?? (_threadId =
            AttributeDefinitionBuilder.CreateLong("thread.id", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _component;
        public AttributeDefinition<string, string> Component => _component ?? (_component =
            AttributeDefinitionBuilder.CreateString("component", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _spanKind;
        public AttributeDefinition<string, string> SpanKind => _spanKind ?? (_spanKind =
            AttributeDefinitionBuilder.CreateString("span.kind", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithDefaultOutputValue("client")
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _spanErrorClass;
        public AttributeDefinition<string, string> SpanErrorClass => _spanErrorClass ?? (_spanErrorClass =
            AttributeDefinitionBuilder.CreateString("error.class", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _spanErrorMessage;
        public AttributeDefinition<string, string> SpanErrorMessage => _spanErrorMessage ?? (_spanErrorMessage =
            AttributeDefinitionBuilder.CreateErrorMessage("error.message", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _spanId;
        public AttributeDefinition<string, string> SpanId => _spanId ?? (_spanId =
            AttributeDefinitionBuilder.CreateString("spanId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _dbStatement;
        public AttributeDefinition<string, string> DbStatement => _dbStatement ?? (_dbStatement =
            AttributeDefinitionBuilder.CreateDBStatement("db.statement", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _dbSystem;
        public AttributeDefinition<string, string> DbSystem => _dbSystem ?? (_dbSystem =
            AttributeDefinitionBuilder.CreateString("db.system", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _dbOperation;
        public AttributeDefinition<string, string> DbOperation => _dbOperation ?? (_dbOperation =
            AttributeDefinitionBuilder.CreateString("db.operation", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _dbCollection;
        public AttributeDefinition<string, string> DbCollection => _dbCollection ?? (_dbCollection =
            AttributeDefinitionBuilder.CreateString("db.collection", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _dbInstance;
        public AttributeDefinition<string, string> DbInstance => _dbInstance ?? (_dbInstance =
            AttributeDefinitionBuilder.CreateString("db.instance", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _peerAddress;
        public AttributeDefinition<string, string> PeerAddress => _peerAddress ?? (_peerAddress =
            AttributeDefinitionBuilder.CreateString("peer.address", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _peerHostname;
        public AttributeDefinition<string, string> PeerHostname => _peerHostname ?? (_peerHostname =
            AttributeDefinitionBuilder.CreateString("peer.hostname", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<Uri, string> _httpUrl;
        public AttributeDefinition<Uri, string> HttpUrl => _httpUrl ?? (_httpUrl =
            AttributeDefinitionBuilder.CreateString<Uri>("http.url", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert((v) => StringsHelper.CleanUri(v))
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _httpMethod;
        public AttributeDefinition<string, string> HttpMethod => _httpMethod ?? (_httpMethod =
            AttributeDefinitionBuilder.CreateString("http.request.method", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _serverAddress;
        public AttributeDefinition<string, string> ServerAddress => _serverAddress ?? (_serverAddress =
            AttributeDefinitionBuilder.CreateString("server.address", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<long, long> _serverPort;
        public AttributeDefinition<long, long> ServerPort => _serverPort ?? (_serverPort =
            AttributeDefinitionBuilder.CreateLong("server.port", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _dbServerAddress;
        public AttributeDefinition<string, string> DbServerAddress => _dbServerAddress ?? (_dbServerAddress =
            AttributeDefinitionBuilder.CreateString("server.address", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<long, long> _dbServerPort;
        public AttributeDefinition<long, long> DbServerPort => _dbServerPort ?? (_dbServerPort =
            AttributeDefinitionBuilder.CreateLong("server.port", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _errorEventSpanId;
        public AttributeDefinition<string, string> ErrorEventSpanId => _errorEventSpanId ?? (_errorEventSpanId =
           AttributeDefinitionBuilder.CreateString("spanId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _errorType;
        public AttributeDefinition<string, string> ErrorType => _errorType ?? (_errorType =
            AttributeDefinitionBuilder.CreateString("errorType", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _errorMessage;
        public AttributeDefinition<string, string> ErrorMessage => _errorMessage ?? (_errorMessage =
            AttributeDefinitionBuilder.CreateString("errorMessage", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<bool, bool> _isError;       //Error Attribute
        public AttributeDefinition<bool, bool> IsError => _isError ?? (_isError =
            AttributeDefinitionBuilder.CreateBool("error", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<bool, bool> _isErrorExpected;
        public AttributeDefinition<bool, bool> IsErrorExpected => _isErrorExpected ?? (_isErrorExpected =
            AttributeDefinitionBuilder.CreateBool("error.expected", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorTrace, AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<bool, bool> _spanIsErrorExpected;
        public AttributeDefinition<bool, bool> SpanIsErrorExpected => _spanIsErrorExpected ?? (_spanIsErrorExpected =
            AttributeDefinitionBuilder.CreateBool("error.expected", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _errorGroup;
        public AttributeDefinition<string, string> ErrorGroup => _errorGroup ?? (_errorGroup =
            AttributeDefinitionBuilder.CreateString("error.group.name", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace)
                .WithConvert(IgnoreEmptyAndWhitespaceErrorGroupValues)
                .Build(_attribFilter));

        private static string IgnoreEmptyAndWhitespaceErrorGroupValues(string errorGroupValue)
        {
            if (!string.IsNullOrWhiteSpace(errorGroupValue))
            {
                return errorGroupValue;
            }

            return null;
        }

        private AttributeDefinition<string, string> _endUserId;
        public AttributeDefinition<string, string> EndUserId => _endUserId ?? (_endUserId =
            AttributeDefinitionBuilder.CreateString("enduser.id", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace,
                           AttributeDestinations.TransactionTrace, AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<DateTime, long> _timestamp;
        public AttributeDefinition<DateTime, long> Timestamp => _timestamp ?? (_timestamp =
            AttributeDefinitionBuilder.CreateLong<DateTime>("timestamp", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .AppliesTo(AttributeDestinations.CustomEvent)
                .WithDefaultInputValue(DateTime.UtcNow)
                .WithConvert((v) => v.ToUnixTimeMilliseconds())
                .Build(_attribFilter));

        private AttributeDefinition<DateTime, long> _timestampForError;
        public AttributeDefinition<DateTime, long> TimestampForError => _timestampForError ?? (_timestampForError =
            AttributeDefinitionBuilder.CreateLong<DateTime>("timestamp", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert((v) => v.ToUnixTimeMilliseconds())
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _transactionName;
        public AttributeDefinition<string, string> TransactionName => _transactionName ?? (_transactionName =
            AttributeDefinitionBuilder.CreateString("name", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _transactionNameForSpan;
        public AttributeDefinition<string, string> TransactionNameForSpan => _transactionNameForSpan ?? (_transactionNameForSpan =
            AttributeDefinitionBuilder.CreateString("transaction.name", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _transactionNameForError;       //Covers case for Custom Error				
        public AttributeDefinition<string, string> TransactionNameForError => _transactionNameForError ?? (_transactionNameForError =
            AttributeDefinitionBuilder.CreateString("transactionName", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _nrGuid;
        public AttributeDefinition<string, string> NrGuid => _nrGuid ?? (_nrGuid =
            AttributeDefinitionBuilder.CreateString("nr.guid", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _guid;
        public AttributeDefinition<string, string> Guid => _guid ?? (_guid =
            AttributeDefinitionBuilder.CreateString(AttributeDefinition.KeyName_Guid, AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _syntheticsResourceId;
        public AttributeDefinition<string, string> SyntheticsResourceId => _syntheticsResourceId ?? (_syntheticsResourceId =
            AttributeDefinitionBuilder.CreateString("nr.syntheticsResourceId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _syntheticsResourceIdForTraces;
        public AttributeDefinition<string, string> SyntheticsResourceIdForTraces => _syntheticsResourceIdForTraces ?? (_syntheticsResourceIdForTraces =
            AttributeDefinitionBuilder.CreateString("synthetics_resource_id", AttributeClassification.Intrinsics)
                    .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _syntheticsJobId;
        public AttributeDefinition<string, string> SyntheticsJobId => _syntheticsJobId ?? (_syntheticsJobId =
            AttributeDefinitionBuilder.CreateString("nr.syntheticsJobId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _syntheticsJobIdForTraces;
        public AttributeDefinition<string, string> SyntheticsJobIdForTraces => _syntheticsJobIdForTraces ?? (_syntheticsJobIdForTraces =
            AttributeDefinitionBuilder.CreateString("synthetics_job_id", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _syntheticsMonitorId;
        public AttributeDefinition<string, string> SyntheticsMonitorId => _syntheticsMonitorId ?? (_syntheticsMonitorId =
            AttributeDefinitionBuilder.CreateString("nr.syntheticsMonitorId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _syntheticsMonitorIdForTraces;
        public AttributeDefinition<string, string> SyntheticsMonitorIdForTraces => _syntheticsMonitorIdForTraces ?? (_syntheticsMonitorIdForTraces =
            AttributeDefinitionBuilder.CreateString("synthetics_monitor_id", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));

        private AttributeDefinition<TimeSpan, double> _duration;
        public AttributeDefinition<TimeSpan, double> Duration => _duration ?? (_duration =
            AttributeDefinitionBuilder.CreateDouble<TimeSpan>("duration", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert((v) => v.TotalSeconds)
                .Build(_attribFilter));

        private AttributeDefinition<TimeSpan, double> _webDuration;
        public AttributeDefinition<TimeSpan, double> WebDuration => _webDuration ?? (_webDuration =
            AttributeDefinitionBuilder.CreateDouble<TimeSpan>("webDuration", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .WithConvert((v) => v.TotalSeconds)
                .Build(_attribFilter));

        private AttributeDefinition<TimeSpan, double> _totalTime;
        public AttributeDefinition<TimeSpan, double> TotalTime => _totalTime ?? (_totalTime =
            AttributeDefinitionBuilder.CreateDouble<TimeSpan>("totalTime", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .WithConvert((v) => v.TotalSeconds)
                .Build(_attribFilter));

        private AttributeDefinition<TimeSpan, double> _cpuTime;
        public AttributeDefinition<TimeSpan, double> CpuTime => _cpuTime ?? (_cpuTime =
            AttributeDefinitionBuilder.CreateDouble<TimeSpan>("cpuTime", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .WithConvert((v) => v.TotalSeconds)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _apdexPerfZone;
        public AttributeDefinition<string, string> ApdexPerfZone => _apdexPerfZone ?? (_apdexPerfZone =
            AttributeDefinitionBuilder.CreateString("nr.apdexPerfZone", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<float, double> _externalDuration;
        public AttributeDefinition<float, double> ExternalDuration => _externalDuration ?? (_externalDuration =
            AttributeDefinitionBuilder.CreateDouble<float>("externalDuration", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert(x => x)
                .Build(_attribFilter));

        private AttributeDefinition<float, double> _externalCallCount;
        public AttributeDefinition<float, double> ExternalCallCount => _externalCallCount ?? (_externalCallCount =
            AttributeDefinitionBuilder.CreateDouble<float>("externalCallCount", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert(x => x)
                .Build(_attribFilter));

        private AttributeDefinition<float, double> _databaseDuration;
        public AttributeDefinition<float, double> DatabaseDuration => _databaseDuration ?? (_databaseDuration =
            AttributeDefinitionBuilder.CreateDouble<float>("databaseDuration", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert((v) => v)
                .Build(_attribFilter));

        private AttributeDefinition<long, double> _databaseCallCount;
        public AttributeDefinition<long, double> DatabaseCallCount => _databaseCallCount ?? (_databaseCallCount =
            AttributeDefinitionBuilder.CreateDouble<long>("databaseCallCount", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert(x => x)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _errorClass;
        public AttributeDefinition<string, string> ErrorClass => _errorClass ?? (_errorClass =
            AttributeDefinitionBuilder.CreateString("error.class", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<TypeAttributeValue, string> _type;
        public AttributeDefinition<TypeAttributeValue, string> Type => _type ?? (_type =
            AttributeDefinitionBuilder.CreateString<TypeAttributeValue>("type", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert(v => EnumNameCache<TypeAttributeValue>.GetName(v))
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _errorDotMessage;
        public AttributeDefinition<string, string> ErrorDotMessage => _errorDotMessage ?? (_errorDotMessage =
            AttributeDefinitionBuilder.CreateErrorMessage("error.message", AttributeClassification.Intrinsics)
                    .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<TypeAttributeValue, string> _parentType;
        public AttributeDefinition<TypeAttributeValue, string> ParentType => _parentType ?? (_parentType =
            AttributeDefinitionBuilder.CreateString<TypeAttributeValue>("parent.type", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert(v => EnumNameCache<TypeAttributeValue>.GetName(v))
                .Build(_attribFilter));

        private AttributeDefinition<TypeAttributeValue, string> _parentTypeForSpan;
        public AttributeDefinition<TypeAttributeValue, string> ParentTypeForSpan => _parentTypeForSpan ?? (_parentTypeForSpan =
            AttributeDefinitionBuilder.CreateString<TypeAttributeValue>("parent.type", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert(v => EnumNameCache<TypeAttributeValue>.GetName(v))
                .Build(_attribFilter));

        private AttributeDefinition<DistributedTracingParentType, string> _parentTypeForDistributedTracing;
        public AttributeDefinition<DistributedTracingParentType, string> ParentTypeForDistributedTracing => _parentTypeForDistributedTracing ?? (_parentTypeForDistributedTracing =
            AttributeDefinitionBuilder.CreateString<DistributedTracingParentType>("parent.type", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert(v => EnumNameCache<DistributedTracingParentType>.GetName(v))
                .Build(_attribFilter));

        private AttributeDefinition<DistributedTracingParentType, string> _parentTypeForDistributedTracingForSpan;
        public AttributeDefinition<DistributedTracingParentType, string> ParentTypeForDistributedTracingForSpan => _parentTypeForDistributedTracingForSpan ?? (_parentTypeForDistributedTracingForSpan =
            AttributeDefinitionBuilder.CreateString<DistributedTracingParentType>("parent.type", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert(v => EnumNameCache<DistributedTracingParentType>.GetName(v))
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _parentAccount;
        public AttributeDefinition<string, string> ParentAccount => _parentAccount ?? (_parentAccount =
            AttributeDefinitionBuilder.CreateString("parent.account", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _parentAccountForSpan;
        public AttributeDefinition<string, string> ParentAccountForSpan => _parentAccountForSpan ?? (_parentAccountForSpan =
            AttributeDefinitionBuilder.CreateString("parent.account", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _parentApp;
        public AttributeDefinition<string, string> ParentApp => _parentApp ?? (_parentApp =
            AttributeDefinitionBuilder.CreateString("parent.app", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _parentAppForSpan;
        public AttributeDefinition<string, string> ParentAppForSpan => _parentAppForSpan ?? (_parentAppForSpan =
            AttributeDefinitionBuilder.CreateString("parent.app", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<TransportType, string> _parentTransportType;
        public AttributeDefinition<TransportType, string> ParentTransportType => _parentTransportType ?? (_parentTransportType =
            AttributeDefinitionBuilder.CreateString<TransportType>("parent.transportType", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert((transportType) => EnumNameCache<TransportType>.GetName(transportType))
                .Build(_attribFilter));

        private AttributeDefinition<TransportType, string> _parentTransportTypeForSpan;
        public AttributeDefinition<TransportType, string> ParentTransportTypeForSpan => _parentTransportTypeForSpan ?? (_parentTransportTypeForSpan =
            AttributeDefinitionBuilder.CreateString<TransportType>("parent.transportType", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert((transportType) => EnumNameCache<TransportType>.GetName(transportType))
                .Build(_attribFilter));

        private AttributeDefinition<TimeSpan, double> _parentTransportDuration;
        public AttributeDefinition<TimeSpan, double> ParentTransportDuration => _parentTransportDuration ?? (_parentTransportDuration =
            AttributeDefinitionBuilder.CreateDouble<TimeSpan>("parent.transportDuration", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .WithConvert((v) => v.TotalSeconds)
                .Build(_attribFilter));

        private AttributeDefinition<TimeSpan, double> _parentTransportDurationForSpan;
        public AttributeDefinition<TimeSpan, double> ParentTransportDurationForSpan => _parentTransportDurationForSpan ?? (_parentTransportDurationForSpan =
            AttributeDefinitionBuilder.CreateDouble<TimeSpan>("parent.transportDuration", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert((v) => v.TotalSeconds)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _parentSpanId;
        public AttributeDefinition<string, string> ParentSpanId => _parentSpanId ?? (_parentSpanId =
            AttributeDefinitionBuilder.CreateString("parentSpanId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _parentId;
        public AttributeDefinition<string, string> ParentId => _parentId ?? (_parentId =
            AttributeDefinitionBuilder.CreateString("parentId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _trustedParentId;
        public AttributeDefinition<string, string> TrustedParentId => _trustedParentId ?? (_trustedParentId =
            AttributeDefinitionBuilder.CreateString("trustedParentId", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));


        private AttributeDefinition<IEnumerable<string>, string> _tracingVendors;
        public AttributeDefinition<IEnumerable<string>, string> TracingVendors => _tracingVendors ?? (_tracingVendors =
            AttributeDefinitionBuilder.CreateString<IEnumerable<string>>("tracingVendors", AttributeClassification.Intrinsics)
                .WithConvert((vendors) =>
                {
                    var vendorNames = vendors.Select(vse => vse.Split('=')[0]).ToList();
                    if (vendorNames.Count == 0)
                    {
                        return null;
                    }

                    return string.Join(",", vendorNames);
                })
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _distributedTraceId;
        public AttributeDefinition<string, string> DistributedTraceId => _distributedTraceId ?? (_distributedTraceId =
            AttributeDefinitionBuilder.CreateString(AttributeDefinition.KeyName_TraceId, AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        //This is defined as a float->object because the translation between float to double
        //causes the value to be changed during the conversion.
        private AttributeDefinition<float, double> _priority;
        public AttributeDefinition<float, double> Priority => _priority ?? (_priority =
            AttributeDefinitionBuilder.CreateDouble<float>("priority", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .WithConvert((f) => f)
                .Build(_attribFilter));

        private AttributeDefinition<bool, bool> _sampled;
        public AttributeDefinition<bool, bool> Sampled => _sampled ?? (_sampled =
            AttributeDefinitionBuilder.CreateBool("sampled", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.SqlTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _hostDisplayName;
        public AttributeDefinition<string, string> HostDisplayName => _hostDisplayName ?? (_hostDisplayName =
            AttributeDefinitionBuilder.CreateString("host.displayName", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.SpanEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _customEventType;
        public AttributeDefinition<string, string> CustomEventType => _customEventType ?? (_customEventType =
            AttributeDefinitionBuilder.CreateString("type", AttributeClassification.Intrinsics)
                .AppliesTo(AttributeDestinations.CustomEvent)
                .Build(_attribFilter));

        private AttributeDefinition<string, string> _codeFunction;
        public AttributeDefinition<string, string> CodeFunction => _codeFunction ?? (
            _codeFunction = AttributeDefinitionBuilder.CreateString(
                "code.function",
                AttributeClassification.AgentAttributes
            )
            .AppliesTo(AttributeDestinations.SpanEvent)
            .Build(_attribFilter)
        );

        private AttributeDefinition<string, string> _codeNamespace;
        public AttributeDefinition<string, string> CodeNamespace => _codeNamespace ?? (
            _codeNamespace = AttributeDefinitionBuilder.CreateString(
                "code.namespace",
                AttributeClassification.AgentAttributes
            )
            .AppliesTo(AttributeDestinations.SpanEvent)
            .Build(_attribFilter)
        );

        private AttributeDefinition<bool, bool> _llmTransaction;
        public AttributeDefinition<bool, bool> LlmTransaction => _llmTransaction ?? (_llmTransaction =
            AttributeDefinitionBuilder.CreateBool("llm", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .Build(_attribFilter));
    }
}
