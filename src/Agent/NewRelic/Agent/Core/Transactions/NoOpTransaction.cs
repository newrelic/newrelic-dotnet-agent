// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.CodeAttributes;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NewRelic.Agent.Core.Transactions
{
    [NrExcludeFromCodeCoverage]
    public class NoOpTransaction : ITransaction, ITransactionExperimental
    {
        public bool IsValid => false;
        public bool IsFinished => false;
        public ISegment CurrentSegment => Segment.NoOpSegment;

        public DateTime StartTime => DateTime.UtcNow;

        private object _wrapperToken;

        private static readonly IExternalSegmentData _noOpExternalSegmentData = new ExternalSegmentData(new Uri("https://www.newrelic.com/"), string.Empty);
        private static readonly IDatastoreSegmentData _noOpDatastoreSegmentData = new DatastoreSegmentData(new DatabaseService(), new ParsedSqlStatement(DatastoreVendor.Other, string.Empty, string.Empty));

        public void End(bool captureResponseTime = true)
        {
        }

        public void Dispose()
        {
        }

        public ISegment StartCustomSegment(MethodCall methodCall, string segmentName)
        {
#if DEBUG
            Log.Finest("Skipping StartCustomSegment outside of a transaction");
#endif
            return Segment.NoOpSegment;
        }

        public ISegment StartDatastoreSegment(MethodCall methodCall, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters = null, bool isLeaf = false)
        {
#if DEBUG
            Log.Finest("Skipping StartDatastoreSegment outside of a transaction");
#endif
            return Segment.NoOpSegment;
        }

        public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method, bool isLeaf = false)
        {
#if DEBUG
            Log.Finest("Skipping StartExternalRequestSegment outside of a transaction");
#endif
            return Segment.NoOpSegment;
        }

        public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method, Action<IExternalSegmentData> segmentDataDelegate)
        {
#if DEBUG
            Log.Finest("Skipping StartExternalRequestSegment outside of a transaction");
#endif
            segmentDataDelegate?.Invoke(_noOpExternalSegmentData);

            return Segment.NoOpSegment;
        }

        public ISegment StartMessageBrokerSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName)
        {
#if DEBUG
            Log.Finest("Skipping StartMessageBrokerSegment outside of a transaction");
#endif
            return Segment.NoOpSegment;
        }

        public ISegment StartMessageBrokerSerializationSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName, string kind)
        {
#if DEBUG
            Log.Finest("Skipping StartMessageBrokerSegment outside of a transaction");
#endif
            return Segment.NoOpSegment;
        }

        public ISegment StartMethodSegment(MethodCall methodCall, string typeName, string methodName, bool isLeaf = false)
        {
#if DEBUG
            Log.Finest("Skipping StartMethodSegment outside of a transaction");
#endif
            return Segment.NoOpSegment;
        }

        public ISegment StartTransactionSegment(MethodCall methodCall, string segmentDisplayName)
        {
#if DEBUG
            Log.Finest("Skipping StartTransactionSegment outside of a transaction");
#endif
            return Segment.NoOpSegment;
        }

        public IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
        {
            Log.Debug("Tried to retrieve CAT response metadata, but there was no transaction");

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
        {
            Log.Debug("Tried to retrieve CAT request metadata, but there was no transaction");

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public IDistributedTracePayload CreateDistributedTracePayload()
        {
            Log.Debug("Tried to create distributed trace payload, but there was no transaction");

            return DistributedTraceApiModel.EmptyModel;
        }

        public void NoticeError(Exception exception)
        {
            Log.Debug(exception, "Ignoring application error because it occurred outside of a transaction");
        }

        public void NoticeError(string message)
        {
            Log.Debug($"Ignoring application error because it occurred outside of a transaction: {message}");
        }

        public void SetHttpResponseStatusCode(int statusCode, int? subStatusCode = null)
        {

        }

        public void AttachToAsync()
        {
        }

        public void Detach()
        {
        }

        public void DetachFromPrimary()
        {
        }

        public void ProcessInboundResponse(IEnumerable<KeyValuePair<string, string>> headers, ISegment segment)
        {

        }

        public void Hold()
        {

        }

        public void Release()
        {

        }

        public void SetWebTransactionName(WebTransactionType type, string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
        {

        }

        public void SetWebTransactionNameFromPath(WebTransactionType type, string path)
        {

        }

        public void SetMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, TransactionNamePriority priority = TransactionNamePriority.Uri)
        {

        }

        public void SetKafkaMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, TransactionNamePriority priority = TransactionNamePriority.Uri)
        {

        }

        public void SetOtherTransactionName(string category, string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
        {

        }

        public void SetCustomTransactionName(string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
        {

        }

        public void SetRequestMethod(string requestMethod)
        {

        }

        public void SetUri(string uri)
        {

        }

        public void SetOriginalUri(string uri)
        {

        }

        public void SetReferrerUri(string uri)
        {

        }

        public void SetQueueTime(TimeSpan queueTime)
        {

        }

        public void SetRequestParameters(IEnumerable<KeyValuePair<string, string>> parameters)
        {

        }

        public object GetOrSetValueFromCache(string key, Func<object> func)
        {
            return null;
        }

        public void LogFinest(string message)
        {
            if (Log.IsFinestEnabled)
            {
                Log.Finest($"Trx Noop: {message}");
            }
        }

        public void Ignore()
        {
        }

        public ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor vendor, CommandType commandType, string sql)
        {
            return null;
        }

        public Dictionary<string, string> GetLinkingMetadata()
        {
            return null;
        }

        public object GetWrapperToken()
        {
            return _wrapperToken;
        }

        public void SetWrapperToken(object wrapperToken)
        {
            _wrapperToken = wrapperToken;
        }

        public ISegment StartSegment(MethodCall methodCall)
        {
            Log.Finest("Skipping StartSegment outside of a transaction");

            return Segment.NoOpSegment;
        }

        public IExternalSegmentData CreateExternalSegmentData(Uri destinationUri, string method)
        {
            return _noOpExternalSegmentData;
        }

        public IDatastoreSegmentData CreateDatastoreSegmentData(ParsedSqlStatement sqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters)
        {
            return _noOpDatastoreSegmentData;
        }

        public ITransaction AddCustomAttribute(string key, object value)
        {
            return this;
        }

        public void InsertDistributedTraceHeaders<T>(T carrier, Action<T, string, string> setter)
        {
            return;
        }

        public void AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType)
        {
            return;
        }

        public ITransaction SetRequestHeaders<T>(T headers, IEnumerable<string> keysToCapture, Func<T, string, string> getter)
        {
            return this;
        }

        public ISegment StartStackExchangeRedisSegment(int invocationTargetHashCode, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, TimeSpan relativeStartTime, TimeSpan relativeEndTime)
        {
            // no log here since this could be called many thousands of times.
            return Segment.NoOpSegment;
        }

        public void SetUserId(string userid)
        {
            return;
        }

        public void SetLlmTransaction(bool isLlmTransaction)
        {
            return;
        }

        public void AddLambdaAttribute(string name, object value)
        {
            return;
        }
    }
}
