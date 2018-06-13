using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Collections;
using NewRelic.Agent.Core.Errors;
using System.Threading;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public interface ITransactionMetadata: ITransactionAttributeMetadata
	{
		[NotNull]
		ImmutableTransactionMetadata ConvertToImmutableMetadata();

		[CanBeNull]
		string CrossApplicationReferrerPathHash { get; }

		[CanBeNull]
		string CrossApplicationReferrerProcessId { get; }

		[CanBeNull]
		string CrossApplicationReferrerTripId { get; }

		string DistributedTraceParentType { get; set; }
		string DistributedTraceParentId { get; set; }
		string DistributedTraceTraceId { get; set; }
		bool DistributedTraceSampled { get; set; }

		[CanBeNull]
		string SyntheticsResourceId { get; }
		[CanBeNull]
		string SyntheticsJobId { get; }
		[CanBeNull]
		string SyntheticsMonitorId { get; }
		string LatestCrossApplicationPathHash { get; }
		void SetUri([NotNull] String uri);
		void SetOriginalUri([NotNull] string uri);
		void SetReferrerUri([NotNull] string uri);
		void SetQueueTime(TimeSpan queueTime);
		void AddRequestParameter([NotNull] string key, [NotNull] string value);
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

		float Priority { get; set; }
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

		[CanBeNull] private volatile string _distributedTraceParentType;
		[CanBeNull] private volatile string _distributedTraceParentId;
		[CanBeNull] private volatile string _distributedTraceTraceId;
		private volatile bool _distributedTraceSampled;

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

		private readonly ConcurrentDictionary<string, string> _requestParameters = new ConcurrentDictionary<string, string>();
		private readonly ConcurrentDictionary<string, object> _userAttributes = new ConcurrentDictionary<string, object>();
		private readonly ConcurrentDictionary<string, object> _userErrorAttributes = new ConcurrentDictionary<string, object>();

		//everything below this does not have a getter, meaning it is only updated and not read during the transaction

		[NotNull]
		private readonly IList<ErrorData> _transactionExceptionDatas = new ConcurrentList<ErrorData>();
		[NotNull]
		private readonly IList<ErrorData> _customErrorDatas = new ConcurrentList<ErrorData>();
		[NotNull]
		private readonly ConcurrentHashSet<string> _allCrossApplicationPathHashes = new ConcurrentHashSet<string>();

		private volatile int _httpResponseSubStatusCode = Int32.MinValue;
		private volatile bool _hasResponseCatHeaders;

		public ImmutableTransactionMetadata ConvertToImmutableMetadata()
		{
			var alternateCrossApplicationPathHashes = _allCrossApplicationPathHashes
				.Except(new[] { _latestCrossApplicationPathHash })
				.Take(PathHashMaker.AlternatePathHashMaxSize);

			return new ImmutableTransactionMetadata(_uri, _originalUri, _referrerUri, GetTimeSpan(), _requestParameters, _userAttributes, _userErrorAttributes, HttpResponseStatusCode, HttpResponseSubStatusCode, _transactionExceptionDatas, _customErrorDatas, _crossApplicationReferrerPathHash, _latestCrossApplicationPathHash, alternateCrossApplicationPathHashes, _crossApplicationReferrerTransactionGuid, _crossApplicationReferrerProcessId, _crossApplicationReferrerTripId, _distributedTraceParentType, _distributedTraceParentId, _distributedTraceTraceId, _distributedTraceSampled, _syntheticsResourceId, _syntheticsJobId, _syntheticsMonitorId, IsSynthetics, _hasResponseCatHeaders, Priority);
		}

		public float Priority { get; set; }

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

			if (_userAttributes.Count >= Attributes.UserAttributeClamp)
			{
				Log.Debug($"User Attribute discarded: {key}. User limit of 64 reached.");
				return;
			}

			_userAttributes[key] = value;
		}

		public void AddUserErrorAttribute(string key, object value)
		{
			_userErrorAttributes[key] = value;
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

		public string DistributedTraceParentType
		{
			get => _distributedTraceParentType;
			set => _distributedTraceParentType = value;
		}
		public string DistributedTraceParentId
		{
			get => _distributedTraceParentId;
			set => _distributedTraceParentId = value;
		}
		public string DistributedTraceTraceId
		{
			get => _distributedTraceTraceId;
			set => _distributedTraceTraceId = value;
		}
		public bool DistributedTraceSampled
		{
			get => _distributedTraceSampled;
			set => _distributedTraceSampled = value;
		}

		public string Uri => _uri;
		[CanBeNull]
		public string OriginalUri => _originalUri;
		[CanBeNull]
		public string ReferrerUri => _referrerUri;

		public TimeSpan? QueueTime => GetTimeSpan();

		private TimeSpan? GetTimeSpan() => _timeSpanQueueTime?.Invoke();

		public int? HttpResponseStatusCode => (_httpResponseStatusCode == Int32.MinValue) ? default(Int32?) : _httpResponseStatusCode;

		int? HttpResponseSubStatusCode => (_httpResponseSubStatusCode == Int32.MinValue) ? default(Int32?) : _httpResponseSubStatusCode;

		public KeyValuePair<string, string>[] RequestParameters => _requestParameters.ToArray();
		public KeyValuePair<string, object>[] UserAttributes => _userAttributes.ToArray();
		public KeyValuePair<string, object>[] UserErrorAttributes => _userErrorAttributes.ToArray();
	}
}
