// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Configuration;
using System.Threading;
using NewRelic.Core;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Errors;
using System.Collections.Concurrent;

namespace NewRelic.Agent.Core.Segments
{
    /// <summary>
    /// Interface that allows managed transfer of the segment's state
    /// to the segment data objects.  This provides access to the attribute
    /// system.
    /// </summary>
    public interface ISegmentDataState
    {
        IAttributeDefinitions AttribDefs { get; }
        string TypeName { get; }
    }

    public class Segment : IInternalSpan, ISegmentDataState
    {
        public IAttributeDefinitions AttribDefs => _transactionSegmentState.AttribDefs;
        public string TypeName => MethodCallData.TypeName;

        private SpanAttributeValueCollection _customAttribValues;

        public Segment(ITransactionSegmentState transactionSegmentState, MethodCallData methodCallData)
        {
            ThreadId = transactionSegmentState.CurrentManagedThreadId;
            RelativeStartTime = transactionSegmentState.GetRelativeTime();
            _transactionSegmentState = transactionSegmentState;
            ParentUniqueId = transactionSegmentState.ParentSegmentId();
            UniqueId = transactionSegmentState.CallStackPush(this);
            MethodCallData = methodCallData;
            Data = new MethodSegmentData(methodCallData.TypeName, methodCallData.MethodName);
            Data.AttachSegmentDataState(this);
            Combinable = false;
            IsLeaf = false;
        }

        /// <summary>
        /// This .ctor is used when combining segments or within unit tests that need to control the start time and duration.
        /// </summary>
        /// <param name="relativeStartTime"></param>
        /// <param name="duration"></param>
        /// <param name="segment"></param>
        /// <param name="parameters"></param>
        public Segment(TimeSpan relativeStartTime, TimeSpan? duration, Segment segment, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            //Attach this segment's data state to the data object.
            this.SetSegmentData(segment.Data);

            RelativeStartTime = relativeStartTime;
            _transactionSegmentState = segment._transactionSegmentState;
            ThreadId = _transactionSegmentState.CurrentManagedThreadId;
            UniqueId = segment.UniqueId;
            ParentUniqueId = segment.ParentUniqueId;
            MethodCallData = segment.MethodCallData;
            _parameters = parameters;
            Combinable = segment.Combinable;
            IsLeaf = false;
            if (duration.HasValue)
            {
                RelativeEndTime = relativeStartTime.Add(duration.Value);
            }

            SpanId = segment.SpanId;
        }

        public bool IsValid => true;
        public bool DurationShouldBeDeductedFromParent { get; set; } = false;

        public bool AlwaysDeductChildDuration { private get; set; } = false;

        public bool IsLeaf { get; set; }
        public bool IsExternal => Data.SpanCategory == SpanCategory.Http;

        private string _spanId;
        public string SpanId
        {
            get
            {
                return _spanId ?? (_spanId = GuidGenerator.GenerateNewRelicGuid());
            }
            set
            {
                _spanId = value;
            }
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

        public void End(Exception ex)
        {
            if (ex != null) ErrorData = _transactionSegmentState.ErrorService.FromException(ex);
            End();
        }

        public void MakeCombinable()
        {
            Combinable = true;
        }

        public void RemoveSegmentFromCallStack()
        {
            _transactionSegmentState.CallStackPop(this);
        }

        private const long NoEndTime = -1;
        internal static NoOpSegment NoOpSegment = new NoOpSegment();
        protected readonly static IEnumerable<KeyValuePair<string, object>> EmptyImmutableParameters = new KeyValuePair<string, object>[0];

        private readonly ITransactionSegmentState _transactionSegmentState;
        protected IEnumerable<KeyValuePair<string, object>> _parameters = EmptyImmutableParameters;
        private volatile bool _parentNotified;
        private long _childDurationTicks = 0;

        // We start and end segments on different threads (sometimes) so we need _relativeEndTicks
        // to be threadsafe.  Be careful when using this variable and use the RelativeEndTime property instead 
        // when possible.
        private long _relativeEndTicks = NoEndTime;

        public AbstractSegmentData Data { get; private set; }
        public ISegmentData SegmentData => Data;

        /// <summary>
        /// Returns the thread id of the current thread when this segment was created.
        /// </summary>
        public int ThreadId { get; private set; }

        // used to set the ["unfinished"] parameter, not for real-time state of segment
        public bool Unfinished { get; private set; }

        public IEnumerable<KeyValuePair<string, object>> Parameters => _parameters ?? EmptyImmutableParameters;

        public ErrorData ErrorData { get; set; }

        public bool Combinable { get; set; }

        public MethodCallData MethodCallData { get; private set; }

        public int? ParentUniqueId { get; }

        public int UniqueId { get; }

        public TimeSpan RelativeStartTime { get; private set; }

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

        public TimeSpan DurationOrZero => Duration ?? TimeSpan.Zero;

        public TimeSpan? Duration
        {
            get
            {
                var ticks = Interlocked.Read(ref _relativeEndTicks);
                return NoEndTime == ticks ? (TimeSpan?)null : new TimeSpan(ticks - RelativeStartTime.Ticks);
            }
        }

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

        private void Finish()
        {
            var endTime = _transactionSegmentState.GetRelativeTime();
            RelativeEndTime = endTime;
            _parameters = Data.Finish() ?? EmptyImmutableParameters;
        }

        public SpanAttributeValueCollection GetAttributeValues()
        {
            var attribValues = _customAttribValues ?? new SpanAttributeValueCollection();

            AttribDefs.Duration.TrySetValue(attribValues, DurationOrZero);
            AttribDefs.NameForSpan.TrySetValue(attribValues, GetTransactionTraceName());

            if (ErrorData != null && _transactionSegmentState.ErrorService.ShouldCollectErrors)
            {
                AttribDefs.SpanErrorClass.TrySetValue(attribValues, ErrorData.ErrorTypeName);
                AttribDefs.SpanErrorMessage.TrySetValue(attribValues, ErrorData.ErrorMessage);

                if (ErrorData.IsExpected)
                {
                    AttribDefs.SpanIsErrorExpected.TrySetValue(attribValues, ErrorData.IsExpected);
                }
            }

            Data.SetSpanTypeSpecificAttributes(attribValues);

            return attribValues;
        }

        public void ForceEnd()
        {
            Unfinished = RelativeEndTime.HasValue == false;
            End();
        }

        public void ChildFinished(Segment childSegment)
        {
            // We are attempting to make a guess about whether a child segment was called synchronously or asynchronously.
            // This check is not perfect.
            // _threadId = the thread the parent method _started_ on
            // _transactionSegmentState.CurrentManagedThreadId = the thread segment.End() was called from for the child segment

            var childExecutedSynchronously = ThreadId == _transactionSegmentState.CurrentManagedThreadId;

            if (!childSegment._parentNotified && (childExecutedSynchronously || AlwaysDeductChildDuration || childSegment.DurationShouldBeDeductedFromParent))
            {
                childSegment._parentNotified = true;
                Interlocked.Add(ref _childDurationTicks, childSegment.DurationOrZero.Ticks);
            }
        }

        public void AddMetricStats(TransactionMetricStatsCollection txStats, IConfigurationService configService)
        {
            if (!Duration.HasValue || txStats == null)
            {
                return;
            }

            Data.AddMetricStats(this, TotalChildDuration, txStats, configService);
        }

        public string GetTransactionTraceName()
        {
            return Data.GetTransactionTraceName();
        }

        public bool IsCombinableWith(Segment otherSegment)
        {
            if (!Combinable || !otherSegment.Combinable)
                return false;

            if (!MethodCallData.Equals(otherSegment.MethodCallData))
                return false;

            return Data.IsCombinableWith(otherSegment.Data);
        }

        public Segment CreateSimilar(TimeSpan newRelativeStartTime, TimeSpan newDuration, IEnumerable<KeyValuePair<string, object>> newParameters)
        {
            return new Segment(newRelativeStartTime, newDuration, this, newParameters);
        }

        public string ToStringForFinestLogging()
        {
            return $"Id={UniqueId},ParentId={ParentUniqueId?.ToString() ?? "Root"},Name={Data.GetTransactionTraceName()},IsLeaf={IsLeaf},Combinable={Combinable},MethodCallData={MethodCallData}";
        }

        public ISegmentExperimental SetSegmentData(ISegmentData segmentData)
        {
            Data = (AbstractSegmentData)segmentData;
            Data.AttachSegmentDataState(this);
            return this;
        }

        public ISegmentExperimental MakeLeaf()
        {
            IsLeaf = true;
            return this;
        }

        private readonly object _customAttribValuesSyncRoot = new object();

        public ISpan AddCustomAttribute(string key, object value)
        {
            SpanAttributeValueCollection customAttribValues;
            lock (_customAttribValuesSyncRoot)
            {
                customAttribValues = _customAttribValues ?? (_customAttribValues = new SpanAttributeValueCollection());
            }

            AttribDefs.GetCustomAttributeForSpan(key).TrySetValue(customAttribValues, value);

            return this;
        }
    }
}
