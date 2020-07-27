using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    public interface ITransactionMetadata : ITransactionAttributeMetadata
    {
        [NotNull]
        ImmutableTransactionMetadata ConvertToImmutableMetadata();

        [CanBeNull]
        string CrossApplicationReferrerPathHash { get; }

        [CanBeNull]
        string CrossApplicationReferrerProcessId { get; }

        [CanBeNull]
        string CrossApplicationReferrerTripId { get; }

        [CanBeNull]
        string SyntheticsResourceId { get; }
        [CanBeNull]
        string SyntheticsJobId { get; }
        [CanBeNull]
        string SyntheticsMonitorId { get; }
        string LatestCrossApplicationPathHash { get; }
        void SetUri([NotNull] String uri);
        void SetOriginalUri([NotNull] string uri);
        void SetPath([NotNull] string path);
        void SetReferrerUri([NotNull] string uri);
        void SetQueueTime(TimeSpan queueTime);
        void AddRequestParameter([NotNull] string key, [NotNull] string value);
        void AddServiceParameter([NotNull] string key, [NotNull] string value);
        void AddUserAttribute([NotNull] string key, [NotNull] Object value);
        void AddUserErrorAttribute([NotNull] string key, [NotNull] Object value);
        void SetHttpResponseStatusCode(int statusCode, int? subStatusCode);
        void AddExceptionData(ErrorData errorData);
        void AddCustomErrorData(ErrorData errorData);
        void SetCrossApplicationReferrerTripId([NotNull] string tripId);
        void SetCrossApplicationReferrerPathHash([NotNull] string referrerPathHash);
        void SetCrossApplicationReferrerProcessId([NotNull] string referrerProcessId);
        void SetCrossApplicationReferrerContentLength(long referrerContentLength);
        void SetCrossApplicationReferrerTransactionGuid([NotNull] string transactionGuid);
        void SetCrossApplicationPathHash([NotNull] string pathHash);
        void SetSyntheticsResourceId(string syntheticsResourceId);
        void SetSyntheticsJobId(string syntheticsJobId);
        void SetSyntheticsMonitorId(string syntheticsMonitorId);
        void MarkHasCatResponseHeaders();

        long GetCrossApplicationReferrerContentLength();

        bool IsSynthetics { get; }
    }

    /// <summary>
    /// An object for a collection of optional transaction metadata.
    /// </summary>
    public class TransactionMetadata : ITransactionMetadata
    {

        // These are all volatile because they can be read before the transaction is completed.
        // These can be written by one thread and read by another.
        private volatile string _crossApplicationReferrerPathHash;
        private volatile string _crossApplicationReferrerProcessId;
        [CanBeNull] private volatile string _crossApplicationReferrerTripId;
        [CanBeNull] private volatile string _crossApplicationReferrerTransactionGuid;
        [CanBeNull] private volatile string _syntheticsResourceId;
        [CanBeNull] private volatile string _syntheticsJobId;
        [CanBeNull] private volatile string _syntheticsMonitorId;
        [CanBeNull] private volatile string _latestCrossApplicationPathHash;

        //if this never gets set, then default to -1
        // thread safety for this occurrs in the getter and setter below
        private long _crossApplicationReferrerContentLength = -1;
        //This is a timeSpan? struct
        private volatile Func<TimeSpan> _timeSpanQueueTime = null;
        //This is a Int32? struct
        private volatile int _httpResponseStatusCode = Int32.MinValue;


        [CanBeNull]
        private volatile string _uri;
        [CanBeNull]
        private volatile string _originalUri;
        [CanBeNull]
        private volatile string _referrerUri;

        [NotNull]
        private readonly IDictionary<string, string> _requestParameters = new ConcurrentDictionary<string, string>();
        [NotNull]
        private readonly IDictionary<string, string> _serviceParameters = new ConcurrentDictionary<string, string>();
        [NotNull]
        private readonly IDictionary<string, Object> _userAttributes = new ConcurrentDictionary<string, Object>();
        [NotNull]
        private readonly IDictionary<string, Object> _userErrorAttributes = new ConcurrentDictionary<string, Object>();

        //everything below this does not have a getter, meaning it is only updated and not read during the transaction

        [NotNull]
        private readonly IList<ErrorData> _transactionExceptionDatas = new ConcurrentList<ErrorData>();
        [NotNull]
        private readonly IList<ErrorData> _customErrorDatas = new ConcurrentList<ErrorData>();
        [NotNull]
        private readonly ConcurrentHashSet<string> _allCrossApplicationPathHashes = new ConcurrentHashSet<string>();

        [CanBeNull]
        private volatile string _path;
        private volatile int _httpResponseSubStatusCode = Int32.MinValue;
        private volatile bool _hasResponseCatHeaders;

        public ImmutableTransactionMetadata ConvertToImmutableMetadata()
        {
            var alternateCrossApplicationPathHashes = _allCrossApplicationPathHashes
                .Except(new[] { _latestCrossApplicationPathHash })
                .Take(PathHashMaker.AlternatePathHashMaxSize);

            return new ImmutableTransactionMetadata(_uri, _originalUri, _path, _referrerUri, GetTimeSpan(), _requestParameters, _serviceParameters, _userAttributes, _userErrorAttributes, HttpResponseStatusCode, HttpResponseSubStatusCode, _transactionExceptionDatas, _customErrorDatas, _crossApplicationReferrerPathHash, _latestCrossApplicationPathHash, alternateCrossApplicationPathHashes, _crossApplicationReferrerTransactionGuid, _crossApplicationReferrerProcessId, _crossApplicationReferrerTripId, _syntheticsResourceId, _syntheticsJobId, _syntheticsMonitorId, IsSynthetics, _hasResponseCatHeaders);
        }

        public bool IsSynthetics
        {
            get
            {
                return (!string.IsNullOrEmpty(_syntheticsResourceId) && !string.IsNullOrEmpty(_syntheticsJobId) &&
                        !string.IsNullOrEmpty(_syntheticsMonitorId));
            }
        }

        public void SetUri(string uri)
        {
            _uri = uri;
        }

        public void SetOriginalUri(string uri)
        {
            _originalUri = uri;
        }

        public void SetPath(string path)
        {
            _path = path;
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
            _requestParameters.Add(key, value);
        }

        public void AddServiceParameter(string key, string value)
        {
            _serviceParameters.Add(key, value);
        }

        public void AddUserAttribute(string key, Object value)
        {
            if (_userAttributes.ContainsKey(key))
            {
                Log.Debug($"User Attribute already exists: {key}");
                return;
            }

            _userAttributes.Add(key, value);
        }

        public void AddUserErrorAttribute(string key, Object value)
        {
            if (_userErrorAttributes.ContainsKey(key))
            {
                Log.Debug($"User Error Attribute already exists: {key}");

                return;
            }

            _userErrorAttributes.Add(key, value);
        }

        public void SetHttpResponseStatusCode(int statusCode, int? subStatusCode)
        {
            _httpResponseStatusCode = statusCode;
            _httpResponseSubStatusCode = (subStatusCode.HasValue ? ((int)subStatusCode) : Int32.MinValue);
        }

        public void AddExceptionData(ErrorData errorData)
        {
            _transactionExceptionDatas.Add(errorData);
        }

        public void AddCustomErrorData(ErrorData errorData)
        {
            _customErrorDatas.Add(errorData);
        }

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
        public string Uri => _uri;
        [CanBeNull]
        public string OriginalUri => _originalUri;
        [CanBeNull]
        public string ReferrerUri => _referrerUri;

        public TimeSpan? QueueTime => GetTimeSpan();

        private TimeSpan? GetTimeSpan() => _timeSpanQueueTime?.Invoke();

        public int? HttpResponseStatusCode => (_httpResponseStatusCode == Int32.MinValue) ? default(Int32?) : _httpResponseStatusCode;

        int? HttpResponseSubStatusCode => (_httpResponseSubStatusCode == Int32.MinValue) ? default(Int32?) : _httpResponseSubStatusCode;


        public IEnumerable<KeyValuePair<string, string>> RequestParameters => _requestParameters.ToList();
        public IEnumerable<KeyValuePair<string, string>> ServiceParameters => _serviceParameters.ToList();
        public IEnumerable<KeyValuePair<string, Object>> UserAttributes => _userAttributes.ToList();
        public IEnumerable<KeyValuePair<string, Object>> UserErrorAttributes => _userErrorAttributes.ToList();

    }
}
