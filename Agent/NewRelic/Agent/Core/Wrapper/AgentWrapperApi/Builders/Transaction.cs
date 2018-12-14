using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Extensions.Providers;
using System.Data;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public interface ITransactionSegmentState
	{
		/// <summary>
		/// Returns a time span relative to the start of the transaction (the 
		/// transaction's duration up to now).
		/// </summary>
		/// <returns></returns>
		TimeSpan GetRelativeTime();

		/// <summary>
		/// Returns the segment id on the top of the current call stack;
		/// </summary>
		/// <returns></returns>
		int? ParentSegmentId();

		/// <summary>
		/// Pushes a segment onto the call stack and adds it to the list of segments.
		/// Returns the unique id of the segment.
		/// </summary>
		/// <param name="segment"></param>
		int CallStackPush(Segment segment);

		/// <summary>
		/// Pops a segment off of the call stack.
		/// </summary>
		/// <param name="segment"></param>
		/// <param name="notifyParent">Notify parent will be true when a segment has ended.  In the async case this is when the task completes.</param>
		void CallStackPop(Segment segment, bool notifyParent = false);

		int CurrentManagedThreadId { get; }
	}

	[NeedSerializableContainer]
	public interface ITransaction
	{
		/// <summary>
		/// Returns a list of the segments in the transaction.  The segment list is always
		/// ordered by the segment creation time.  A segment will always be preceeded in the list by its 
		/// parent segment (unless it is a root segment).
		/// </summary>
		[NotNull]
		IList<Segment> Segments { get; }

		[NotNull]
		ICandidateTransactionName CandidateTransactionName { get; }

		[NotNull]
		ITransactionMetadata TransactionMetadata { get; }
		
		[NotNull]
		ICallStackManager CallStackManager { get; }

		int UnitOfWorkCount { get; }
		int NestedTransactionAttempts { get; }

		[NotNull]
		ImmutableTransaction ConvertToImmutableTransaction();

		void Ignore();
		int NoticeUnitOfWorkBegins();
		int NoticeUnitOfWorkEnds();
		int NoticeNestedTransactionAttempt();
		void IgnoreAutoBrowserMonitoringForThisTx();
		void IgnoreAllBrowserMonitoringForThisTx();
		void IgnoreApdex();

		bool IsFinished { get; }

		/// <summary>
		/// Marks this builder as cleanly finished.
		/// </summary>
		/// <returns>true if the transaction actually finished, and false if it was already finished.</returns>
		bool Finish();

		/// <summary>
		/// Forces the duration of the builder to a particular value, regardless of how long it has actually run for
		/// </summary>
		void ForceChangeDuration(TimeSpan duration);

		// There are some cases where we need access to information prior to the completion 
		// of the transaction. Here are the gettter methods for accessing that data.
		// Note that this could be access on different threads.
		bool Ignored { get; }

		bool IgnoreAutoBrowserMonitoring { get; }

		bool IgnoreAllBrowserMonitoring { get; }
		// This guid is created during the transaction initizialiaztion
		string Guid { get; }
		DateTime StartTime { get; }

		// Used for RUM and CAT to get the duration until this point in time
		TimeSpan GetDurationUntilNow();

		ITransactionSegmentState GetTransactionSegmentState();
		object GetOrSetValueFromCache(string key, Func<object> func);
		ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql);
	}

	public class Transaction : ITransaction, ITransactionSegmentState
	{
		private static readonly ITransactionName EmptyTransactionName = new OtherTransactionName("empty", "empty");

		private readonly ConcurrentList<Segment> _segments = new ConcurrentList<Segment>();
		[NotNull]
		public IList<Segment> Segments { get => _segments; }

		[NotNull]
		private readonly ITimer _timer;
		[NotNull]
		private readonly DateTime _startTime;
		private TimeSpan? _forcedDuration;

		private volatile bool _ignored;
		private int _unitOfWorkCount;
		private int _totalNestedTransactionAttempts;
		private readonly int _transactionTracerMaxSegments;

		[NotNull] private string _guid;

		private volatile bool _ignoreAutoBrowserMonitoring;
		private volatile bool _ignoreAllBrowserMonitoring;
		private bool _ignoreApdex;

		public ICandidateTransactionName CandidateTransactionName { get; }
		public ITransactionMetadata TransactionMetadata { get; }
		public int UnitOfWorkCount => _unitOfWorkCount;
		public int NestedTransactionAttempts => _totalNestedTransactionAttempts;
	
		public ICallStackManager CallStackManager { get; }

		private readonly SqlObfuscator _sqlObfuscator;
		private readonly IDatabaseStatementParser _databaseStatementParser;

		public Transaction(IConfiguration configuration, ITransactionName initialTransactionName,
			ITimer timer, DateTime startTime, ICallStackManager callStackManager, SqlObfuscator sqlObfuscator, float priority, IDatabaseStatementParser databaseStatementParser)
		{
			CandidateTransactionName = new CandidateTransactionName(initialTransactionName);
			_guid = GuidGenerator.GenerateNewRelicGuid();
			TransactionMetadata = new TransactionMetadata
			{
				Priority = priority,
				DistributedTraceTraceId = _guid
			};

			CallStackManager = callStackManager;
			_transactionTracerMaxSegments = configuration.TransactionTracerMaxSegments;
			_startTime = startTime;
			_timer = timer;
			_unitOfWorkCount = 1;
			_sqlObfuscator = sqlObfuscator;
			_databaseStatementParser = databaseStatementParser;
		}

		public int Add(Segment segment)
		{
			if (segment != null)
			{
				return _segments.AddAndReturnIndex(segment);
			}
			return -1;
		}

		public ImmutableTransaction ConvertToImmutableTransaction()
		{
			var transactionName = CandidateTransactionName.CurrentTransactionName;
			var transactionMetadata = TransactionMetadata.ConvertToImmutableMetadata();

			return new ImmutableTransaction(transactionName, Segments, transactionMetadata, _startTime, _forcedDuration ?? _timer.Duration, _guid, _ignoreAutoBrowserMonitoring, _ignoreAllBrowserMonitoring, _ignoreApdex, _sqlObfuscator);
		}

		public void Ignore()
		{
			_ignored = true;
		}

		public int NoticeUnitOfWorkBegins()
		{
			return Interlocked.Increment(ref _unitOfWorkCount);
		}

		public int NoticeUnitOfWorkEnds()
		{
			return Interlocked.Decrement(ref _unitOfWorkCount);
		}

		public int NoticeNestedTransactionAttempt()
		{
			return Interlocked.Increment(ref _totalNestedTransactionAttempts);
		}

		public void IgnoreAutoBrowserMonitoringForThisTx()
		{
			_ignoreAutoBrowserMonitoring = true;
		}

		public void IgnoreAllBrowserMonitoringForThisTx()
		{
			_ignoreAllBrowserMonitoring = true;
		}

		public void IgnoreApdex()
		{
			_ignoreApdex = true;
		}

		#region Methods to access data
		public bool IgnoreAutoBrowserMonitoring => _ignoreAutoBrowserMonitoring;
		public bool IgnoreAllBrowserMonitoring => _ignoreAllBrowserMonitoring;

		public bool Ignored => _ignored;
		public string Guid => _guid;
		public DateTime StartTime => _startTime;

		/// <summary>
		/// This is a method instead of property to prevent StackOverflowException when our 
		/// Transaction is serialized. Sometimes 3rd party tools serialize our stuff even when 
		/// we don't want. We still want to do no harm, when possible.
		/// 
		/// Ideally, we don't return the instance in this way but putting in a quick fix for now.
		/// </summary>
		/// <returns></returns>
		public ITransactionSegmentState GetTransactionSegmentState()
		{
			return this;
		}

		private ConcurrentDictionary<string, object> _transactionCache;

		private ConcurrentDictionary<string, object> TransactionCache => _transactionCache ?? (_transactionCache = new ConcurrentDictionary<string, object>());

		public int CurrentManagedThreadId => Thread.CurrentThread.ManagedThreadId;

		public object GetOrSetValueFromCache(string key, Func<object> func)
		{
			if (key == null)
			{
				Log.Debug("GetOrSetValueFromCache(), key is NULL");
				return null;
			}

			return TransactionCache.GetOrAdd(key, x => func());
		}

		// This will need to get cleaned up with all of the timing stuff.
		// Having a timer in the transaction and then separate timers in the segments is bad.
		public TimeSpan GetDurationUntilNow()
		{
			return _timer.Duration;
		}

		public TimeSpan GetRelativeTime()
		{
			return GetDurationUntilNow();
		}

		#endregion

		#region TranasctionBuilder finalization logic

		public bool IsFinished { get; private set; } = false;

		private object _finishLock = new object();

		public bool Finish()
		{
			_timer.Stop();
			// Prevent the finalizer/destructor from running
			GC.SuppressFinalize(this);

			if (IsFinished) return false;

			lock (_finishLock)
			{
				if (IsFinished) return false;

				//Only the call that successfully sets IsFinished to true should return true so that the transaction can only be finished once.
				IsFinished = true;
				return true;
			}
		}

		/// <summary>
		/// A destructor/finalizer that will announce when a Transaction is garbage collected without ending normally (e.g. without Finish() being called).
		/// </summary>
		~Transaction()
		{
			try
			{
				GC.SuppressFinalize(this);
				EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(this));
			}
			catch
			{
				// Swallow because throwing from a finally is fatal
			}
		}

		public void ForceChangeDuration(TimeSpan duration)
		{
			_forcedDuration = duration;
		}

		#endregion TranasctionBuilder finalization logic

		public int CallStackPush(Segment segment)
		{
			int id = -1;
			if (!_ignored)
			{
				id = Add(segment);
				if (id >= 0)
				{
					CallStackManager.Push(id);
				}
			}
			return id;
		}

		public void CallStackPop(Segment segment, bool notifyParent = false)
		{
			CallStackManager.TryPop(segment.UniqueId, segment.ParentUniqueId);
			if (notifyParent)
			{
				if (segment.UniqueId >= _transactionTracerMaxSegments)
				{
					// we're over the segment limit.  Null out the reference to the segment.
					_segments[segment.UniqueId] = null;
				}

				if (segment.ParentUniqueId.HasValue)
				{
					var parentSegment = _segments[segment.ParentUniqueId.Value];
					if (null != parentSegment)
					{
						parentSegment.ChildFinished(segment);
					}
				}
			}
		}

		public int? ParentSegmentId()
		{
			return CallStackManager.TryPeek();
		}

		public ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql)
		{
			return _databaseStatementParser.ParseDatabaseStatement(datastoreVendor, commandType, sql);
		}
	}
}
