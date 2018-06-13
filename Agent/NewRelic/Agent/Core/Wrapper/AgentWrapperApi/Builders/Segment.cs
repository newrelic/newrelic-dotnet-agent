using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using System.Threading;
using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public abstract class AbstractSegmentData
	{
		/// <summary>
		/// Called when the owning segment finishes.  Returns an enumerable 
		/// of the segment parameters or null if none are applicable.
		/// </summary>
		/// <returns></returns>
		internal virtual IEnumerable<KeyValuePair<String, Object>> Finish()
		{
			return null;
		}

		public abstract bool IsCombinableWith(AbstractSegmentData otherData);
		public abstract string GetTransactionTraceName();

		public abstract void AddMetricStats(Segment segment, TimeSpan durationOfChildren, TransactionMetricStatsCollection txStats, IConfigurationService configService);

		public abstract Segment CreateSimilar(Segment segment, TimeSpan newRelativeStartTime, TimeSpan newDuration, [NotNull] IEnumerable<KeyValuePair<String, Object>> newParameters);

		internal virtual void AddTransactionTraceParameters(IConfigurationService configurationService, Segment segment, IDictionary<string, object> segmentParameters, ImmutableTransaction immutableTransaction)
		{
		}
	}


	public class Segment : ISegment
	{
		protected readonly static IEnumerable<KeyValuePair<String, Object>> EmptyImmutableParameters =
			new KeyValuePair<String, Object>[0];
		private const long NoEndTime = -1;

		public int UniqueId { get; }
		public int? ParentUniqueId { get; }

		public bool IsLeaf { get; set; }

		[NotNull]
		protected readonly MethodCallData _methodCallData;
		public MethodCallData MethodCallData => _methodCallData;

		[NotNull]
		private readonly ITransactionSegmentState _transactionSegmentState;
		protected readonly TimeSpan _relativeStartTime;

		public TimeSpan RelativeStartTime => _relativeStartTime;

		// sigh.  we start and end segments on different threads (sometimes) so we need _relativeEndTicks
		// to be threadsafe.  Be careful when using this variable and use the RelativeEndTime property instead 
		// when possible.
		private long _relativeEndTicks = NoEndTime;

		/// <summary>
		/// Returns the relative end time if Segment.End() has been called.
		/// </summary>
		public TimeSpan? RelativeEndTime
		{
			get
			{
				var ticks = Interlocked.Read(ref _relativeEndTicks);
				return NoEndTime == ticks ? (TimeSpan?)null : new TimeSpan(ticks);
			}
			private set
			{
				if (value.HasValue)
				{
					Interlocked.Exchange(ref _relativeEndTicks, value.Value.Ticks);
				}
			}
		}
		public TimeSpan CalculatedRelativeEndTime => RelativeEndTime.HasValue ? RelativeEndTime.Value : RelativeStartTime;

		public TimeSpan? Duration
		{
			get
			{
				var ticks = Interlocked.Read(ref _relativeEndTicks);
				return NoEndTime == ticks ? (TimeSpan?)null : new TimeSpan(ticks - _relativeStartTime.Ticks);
			}
		}

		public TimeSpan DurationOrZero => Duration ?? TimeSpan.Zero;

		public TimeSpan TotalChildDuration => new TimeSpan(Interlocked.Read(ref _childDurationTicks));

		public TimeSpan ExclusiveDurationOrZero
		{
			get
			{
				var childDurationTicks = Interlocked.Read(ref _childDurationTicks);
				var duration = DurationOrZero;
				if (duration.Ticks <= childDurationTicks)
				{
					return TimeSpan.Zero;
				}
				return new TimeSpan(duration.Ticks - childDurationTicks);
			}
		}

		public Boolean Combinable { get; set; }

		[NotNull]
		public IEnumerable<KeyValuePair<String, Object>> Parameters => _parameters ?? EmptyImmutableParameters;

		protected IEnumerable<KeyValuePair<String, Object>> _parameters = EmptyImmutableParameters;
		private readonly int _threadId;
		private volatile bool _parentNotified;

		private long _childDurationTicks = 0;
		public bool Unfinished { get; private set; }

		/// <summary>
		/// Returns the thread id of the current thread when this segment was created.
		/// </summary>
		public int ThreadId => _threadId;

		public AbstractSegmentData Data { get; }

		protected Segment([NotNull] ITransactionSegmentState transactionSegmentState, [NotNull] MethodCallData methodCallData, AbstractSegmentData segmentData, bool combinable)
		{
			_threadId = transactionSegmentState.CurrentManagedThreadId;
			_relativeStartTime = transactionSegmentState.GetRelativeTime();
			_transactionSegmentState = transactionSegmentState;
			ParentUniqueId = transactionSegmentState.ParentSegmentId();
			UniqueId = transactionSegmentState.CallStackPush(this);
			_methodCallData = methodCallData;
			Data = segmentData;
			Combinable = combinable;
			IsLeaf = false;
		}

		protected Segment(TimeSpan relativeStartTime, TimeSpan? duration, Segment segment, IEnumerable<KeyValuePair<String, Object>> parameters)
		{
			Data = segment.Data;
			_relativeStartTime = relativeStartTime;
			_transactionSegmentState = segment._transactionSegmentState;
			_threadId = _transactionSegmentState.CurrentManagedThreadId;
			UniqueId = segment.UniqueId;
			ParentUniqueId = segment.ParentUniqueId;
			_methodCallData = segment.MethodCallData;
			_parameters = parameters;
			Combinable = segment.Combinable;
			IsLeaf = false;
			if (duration.HasValue)
			{
				RelativeEndTime = relativeStartTime.Add(duration.Value);
			}
		}

		private void Finish()
		{
			var endTime  = _transactionSegmentState.GetRelativeTime();
			RelativeEndTime = endTime;
			_parameters = Data.Finish() ?? EmptyImmutableParameters;
		}

		public void ForceEnd()
		{
			Unfinished = RelativeEndTime.HasValue == false;
			End();
		}

		public void End()
		{
			// this segment may have already been forced to end
			if (RelativeEndTime.HasValue == false)
			{
				Finish();

				_transactionSegmentState.CallStackPop(this, true);
			}
		}

		public void ChildFinished(Segment childSegment)
		{
			if (!childSegment._parentNotified && childSegment._threadId == _threadId)
			{
				childSegment._parentNotified = true;
				Interlocked.Add(ref _childDurationTicks, childSegment.DurationOrZero.Ticks);
			}
		}

		public void End(Exception ex)
		{
			End();
		}

		public void MakeCombinable()
		{
			this.Combinable = true;
		}

		public void RemoveSegmentFromCallStack()
		{
			_transactionSegmentState.CallStackPop(this);
		}

		public bool IsValid => true;

		public bool IsCombinableWith([NotNull] Segment otherSegment)
		{
			if (!Combinable || !otherSegment.Combinable)
				return false;

			if (!MethodCallData.Equals(otherSegment.MethodCallData))
				return false;

			return Data.IsCombinableWith(otherSegment.Data);
		}

		public void AddMetricStats(TransactionMetricStatsCollection txStats, IConfigurationService configService)
		{
			if (!Duration.HasValue || txStats == null)
			{
				return;
			}
			
			Data.AddMetricStats(this, TotalChildDuration, txStats, configService);
		}

		public String GetTransactionTraceName()
		{
			return Data.GetTransactionTraceName();
		}

		public Segment CreateSimilar(TimeSpan newRelativeStartTime, TimeSpan newDuration, [NotNull] IEnumerable<KeyValuePair<String, Object>> newParameters)
		{
			return Data.CreateSimilar(this, newRelativeStartTime, newDuration, newParameters);
		}
	}

	public sealed class TypedSegment<T> : Segment where T : AbstractSegmentData
	{
		public TypedSegment([NotNull] ITransactionSegmentState transactionSegmentState, [NotNull] MethodCallData methodCallData, T segmentData, bool combinable = false) :
			base(transactionSegmentState, methodCallData, segmentData, combinable)
		{
			TypedData = segmentData;
		}

		public TypedSegment(TimeSpan relativeStartTime, TimeSpan? duration, Segment segment, IEnumerable<KeyValuePair<String, Object>> parameters = null) :
			base(relativeStartTime, duration, segment, parameters)
		{
			TypedData = (T)segment.Data;
		}

		public T TypedData { get; }
	}
}
