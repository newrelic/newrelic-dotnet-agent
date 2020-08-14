// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Collections;
using NewRelic.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Transactions
{
    public interface ITransactionMetadata : ITransactionAttributeMetadata, IImmutableTransactionMetadata
    {
        IImmutableTransactionMetadata ConvertToImmutableMetadata();
        string LatestCrossApplicationPathHash { get; }
        void SetUri(string uri);
        void SetOriginalUri(string uri);
        void SetReferrerUri(string uri);
        void SetQueueTime(TimeSpan queueTime);
        void AddRequestParameter(string key, string value);
        void AddUserAttribute(string key, object value);
        void SetHttpResponseStatusCode(int statusCode, int? subStatusCode, IErrorService errorService);
        void SetCrossApplicationReferrerTripId(string tripId);
        void SetCrossApplicationReferrerPathHash(string referrerPathHash);
        void SetCrossApplicationReferrerProcessId(string referrerProcessId);
        void SetCrossApplicationReferrerContentLength(long referrerContentLength);
        void SetCrossApplicationReferrerTransactionGuid(string transactionGuid);
        void SetCrossApplicationPathHash(string pathHash);
        void SetCrossApplicationResponseTimeInSeconds(float responseTimeInSeconds);
        new bool HasOutgoingTraceHeaders { get; set; }
        void SetSyntheticsResourceId(string syntheticsResourceId);
        void SetSyntheticsJobId(string syntheticsJobId);
        void SetSyntheticsMonitorId(string syntheticsMonitorId);
        void MarkHasCatResponseHeaders();

        long GetCrossApplicationReferrerContentLength();

        ITransactionErrorState TransactionErrorState { get; }
    }

    /// <summary>
    /// An object for a collection of optional transaction metadata.
    /// </summary>
    public class TransactionMetadata : ITransactionMetadata
    {
        private readonly object _sync = new object();
        //This mapping needs to be kept in-sync with the TransportType enum
        public static readonly string[] TransportTypeToStringMapping = new[]
        {
            "Unknown",
            "HTTP",
            "HTTPS",
            "Kafka",
            "JMS",
            "IronMQ",
            "AMQP",
            "Queue",
            "Other"
        };

        // These are all volatile because they can be read before the transaction is completed.
        // These can be written by one thread and read by another.
        private volatile string _crossApplicationReferrerPathHash;
        private volatile string _crossApplicationReferrerProcessId;
        private volatile string _crossApplicationReferrerTripId;
        private volatile string _crossApplicationReferrerTransactionGuid;
        private volatile float _crossApplicationResponseTimeInSeconds = 0;

        private volatile string _syntheticsResourceId;
        private volatile string _syntheticsJobId;
        private volatile string _syntheticsMonitorId;
        private volatile string _latestCrossApplicationPathHash;

        //if this never gets set, then default to -1
        // thread safety for this occurrs in the getter and setter below
        private long _crossApplicationReferrerContentLength = -1;
        //This is a timeSpan? struct
        private volatile Func<TimeSpan> _timeSpanQueueTime = null;

        private volatile int _httpResponseStatusCode = int.MinValue;
        private volatile int _httpResponseSubStatusCode = int.MinValue;

        private volatile string _uri;
        private volatile string _originalUri;
        private volatile string _referrerUri;

        private readonly ConcurrentDictionary<string, string> _requestParameters = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, object> _userAttributes = new ConcurrentDictionary<string, object>();

        private readonly ConcurrentHashSet<string> _allCrossApplicationPathHashes = new ConcurrentHashSet<string>();
        private volatile bool _hasResponseCatHeaders;

        public IImmutableTransactionMetadata ConvertToImmutableMetadata()
        {
            return this;
        }

        public bool IsSynthetics => !string.IsNullOrEmpty(_syntheticsResourceId) && !string.IsNullOrEmpty(_syntheticsJobId) &&
                                     !string.IsNullOrEmpty(_syntheticsMonitorId);

        public void SetUri(string uri)
        {
            _uri = uri;
        }

        public void SetOriginalUri(string uri)
        {
            _originalUri = uri;
        }

        public void SetReferrerUri(string uri)
        {
            _referrerUri = uri;
        }

        public void SetQueueTime(TimeSpan queueTime)
        {
            _timeSpanQueueTime = () => queueTime;
        }

        public void AddRequestParameter(string key, string value)
        {
            _requestParameters[key] = value;
        }

        public void AddUserAttribute(string key, object value)
        {
            
            // A context switch is possible between calls to Count and AddOrUpdate.
            // This makes the following logic somewhat bogus. That is, it is possible for more attributes to be added than allowed by UserAttributeClamp.
            // However, the AttributeService still enforces UserAttributeClamp on the back end.
            if (_userAttributes.Count >= AttributeValueCollection.MaxCountUserAttrib)
            {
                Log.Debug($"User Attribute discarded: {key}. User limit of 64 reached.");
                return;
            }

            _userAttributes[key] = value;
        }

        public void SetHttpResponseStatusCode(int statusCode, int? subStatusCode, IErrorService errorService)
        {
            _httpResponseStatusCode = statusCode;
            _httpResponseSubStatusCode = subStatusCode.HasValue ? (int)subStatusCode : int.MinValue;

            if (statusCode >= 400)
            {
                if (!errorService.ShouldIgnoreHttpStatusCode(statusCode, subStatusCode))
                {
                    var errorData = errorService.FromErrorHttpStatusCode(statusCode, subStatusCode, DateTime.UtcNow);
                    TransactionErrorState.AddStatusCodeErrorData(errorData);
                }
                else
                {
                    TransactionErrorState.SetIgnoreAgentNoticedErrors();
                }
            }
        }

        public ITransactionErrorState TransactionErrorState { get; } = new TransactionErrorState();
        public IReadOnlyTransactionErrorState ReadOnlyTransactionErrorState => TransactionErrorState;

        public void SetCrossApplicationReferrerPathHash(string referrerPathHash)
        {
            _crossApplicationReferrerPathHash = referrerPathHash;
        }

        public void SetCrossApplicationReferrerProcessId(string referrerProcessId)
        {
            _crossApplicationReferrerProcessId = referrerProcessId;
        }

        public void SetCrossApplicationReferrerContentLength(long contentLength)
        {
            Interlocked.Exchange(ref _crossApplicationReferrerContentLength, contentLength);
        }

        public void SetCrossApplicationReferrerTransactionGuid(string transactionGuid)
        {
            _crossApplicationReferrerTransactionGuid = transactionGuid;
        }

        public void SetCrossApplicationPathHash(string pathHash)
        {
            _latestCrossApplicationPathHash = pathHash;
            _allCrossApplicationPathHashes.Add(pathHash);
        }

        public void SetCrossApplicationReferrerTripId(string referrerTripId)
        {
            _crossApplicationReferrerTripId = referrerTripId;
        }

        public void SetCrossApplicationResponseTimeInSeconds(float responseTimeInSeconds)
        {
            Interlocked.Exchange(ref _crossApplicationResponseTimeInSeconds, responseTimeInSeconds);
        }

        public void SetSyntheticsResourceId(string syntheticsResourceId)
        {
            _syntheticsResourceId = syntheticsResourceId;
        }
        public void SetSyntheticsJobId(string syntheticsJobId)
        {
            _syntheticsJobId = syntheticsJobId;
        }
        public void SetSyntheticsMonitorId(string syntheticsMonitorId)
        {
            _syntheticsMonitorId = syntheticsMonitorId;
        }

        public void MarkHasCatResponseHeaders()
        {
            _hasResponseCatHeaders = true;
        }

        public long GetCrossApplicationReferrerContentLength()
        {
            return Interlocked.Read(ref _crossApplicationReferrerContentLength);
        }

        public string SyntheticsJobId => _syntheticsJobId;
        public string SyntheticsMonitorId => _syntheticsMonitorId;
        public string SyntheticsResourceId => _syntheticsResourceId;
        public string CrossApplicationReferrerPathHash => _crossApplicationReferrerPathHash;
        public string CrossApplicationReferrerTripId => _crossApplicationReferrerTripId;
        public string CrossApplicationReferrerProcessId => _crossApplicationReferrerProcessId;
        public string CrossApplicationReferrerTransactionGuid => _crossApplicationReferrerTransactionGuid;
        public string LatestCrossApplicationPathHash => _latestCrossApplicationPathHash;
        public float CrossApplicationResponseTimeInSeconds => _crossApplicationResponseTimeInSeconds;

        public bool HasOutgoingTraceHeaders { get; set; }

        public string Uri => _uri;
        public string OriginalUri => _originalUri;
        public string ReferrerUri => _referrerUri;

        public TimeSpan? QueueTime => GetTimeSpan();

        private TimeSpan? GetTimeSpan() => _timeSpanQueueTime?.Invoke();

        public int? HttpResponseStatusCode => _httpResponseStatusCode == int.MinValue ? default(int?) : _httpResponseStatusCode;
        public int? HttpResponseSubStatusCode => _httpResponseSubStatusCode == int.MinValue ? default(int?) : _httpResponseSubStatusCode;

        public KeyValuePair<string, string>[] RequestParameters => _requestParameters.ToArray();
        public KeyValuePair<string, object>[] UserAttributes => _userAttributes.ToArray();


        public IEnumerable<string> CrossApplicationAlternatePathHashes => _allCrossApplicationPathHashes
            .Except(new[] { _latestCrossApplicationPathHash })
            .Take(PathHashMaker.AlternatePathHashMaxSize);

        public string CrossApplicationPathHash => _latestCrossApplicationPathHash;

        public bool HasCatResponseHeaders => _hasResponseCatHeaders;
    }
}
