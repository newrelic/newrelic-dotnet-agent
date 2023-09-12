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
using System.Diagnostics;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Utilities;

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
        private static ConfigurationSubscriber _configurationSubscriber = new ConfigurationSubscriber();

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
        /// This .ctor is used when we need to specify both a start time and end time due to a segment being created as part of a batch.  Used mainly in StackExchange.Redis.
        /// </summary>
        /// <param name="transactionSegmentState"></param>
        /// <param name="methodCallData"></param>
        /// <param name="relativeStartTime"></param>
        /// <param name="relativeEndTime"></param>
        public Segment(ITransactionSegmentState transactionSegmentState, MethodCallData methodCallData, TimeSpan relativeStartTime, TimeSpan relativeEndTime)
        {
            ThreadId = transactionSegmentState.CurrentManagedThreadId;
            RelativeStartTime = relativeStartTime;
            RelativeEndTime = relativeEndTime;
            _transactionSegmentState = transactionSegmentState;
            ParentUniqueId = transactionSegmentState.ParentSegmentId();
            UniqueId = transactionSegmentState.CallStackPush(this);
            MethodCallData = methodCallData;
            Data = new MethodSegmentData(methodCallData.TypeName, methodCallData.MethodName);
            Data.AttachSegmentDataState(this);
            Combinable = false;
            IsLeaf = true;
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
            SegmentNameOverride = segment.SegmentNameOverride;
            _parameters = parameters;
            Combinable = segment.Combinable;
            IsLeaf = false;
            if (duration.HasValue)
            {
                RelativeEndTime = relativeStartTime.Add(duration.Value);
            }

            SpanId = segment.SpanId;
        }

        public bool IsDone
        {
            get { return RelativeEndTime.HasValue; }
        }

        public bool IsValid => true;

        public bool DurationShouldBeDeductedFromParent { get; set; } = false;

        public bool AlwaysDeductChildDuration { private get; set; } = false;

        public bool IsLeaf { get; set; }
        public bool IsExternal => Data.SpanCategory == SpanCategory.Http;

        private string _spanId;
        /// <summary>
        /// Gets the SpanId for the segment.  If SpanId has not been set it will create one.
        /// This call could potentially generate more than one Id if GET is called from multiple threads at the same time.
        /// Current usage of this property does not do this.
        /// </summary>
        public string SpanId
        {
            get
            {
                // If _spanId is null and this is called rapidly from different threads, the returned value could be different for each.
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
                // This order is to ensure the segment end time is correct, but also not mark the segment as IsDone so that CleanUp ignores it.
                var endTime = _transactionSegmentState.GetRelativeTime();
                Agent.Instance?.StackExchangeRedisCache?.Harvest(this);
                RelativeEndTime = endTime;

                Finish();

                _transactionSegmentState.CallStackPop(this, true);
            }
        }

        private void Finish()
        {
            _parameters = Data.Finish();

            // if transactionTracer is disabled, we not need stack traces.
            // if stack frames is 0, it is considered that the customer disabled stack traces.
            // if max stack traces is 0, it is considered that the customer disabled stack traces.
            if (_configurationSubscriber.Configuration.TransactionTracerEnabled
            && _configurationSubscriber.Configuration.StackTraceMaximumFrames > 0
            && _configurationSubscriber.Configuration.TransactionTracerMaxStackTraces > 0)
            {
                var stackFrames = StackTraces.ScrubAndTruncate(new StackTrace(2, true), _configurationSubscriber.Configuration.StackTraceMaximumFrames);// first 2 stack frames are agent code
                var stackFramesAsStringArray = StackTraces.ToStringList(stackFrames); // serializer doesn't understand StackFrames, but does understand strings
                if (_parameters == null)
                {
                    _parameters = new KeyValuePair<string, object>[1] { new KeyValuePair<string, object>("backtrace", stackFramesAsStringArray) };
                }
                else
                {
                    // Only external segments return a collection and its a Dictionary
                    ((Dictionary<string, object>)_parameters).Add("backtrace", stackFramesAsStringArray);
                }
            }
            else if (_parameters == null) // External segments return a dictionary, so we have to check for null here.
            {
                _parameters = EmptyImmutableParameters;
            }
        }

        public void EndStackExchangeRedis()
        {
            Finish();
            _transactionSegmentState.CallStackPop(this, true);
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

        // For auto-instrumentation, we often instrument a function of the framework itself
        // which represents and executes user code. So we need to keep track of the actual user
        // code namespace (type) and function that the instrumentation represents for mapping to
        // customer code.
        public string UserCodeNamespace { get; set; } = null;
        public string UserCodeFunction { get; set; } = null;
		
        public string SegmentNameOverride { get; set; }

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

            if (_configurationSubscriber.Configuration.CodeLevelMetricsEnabled)
            {
                var codeNamespace = !string.IsNullOrEmpty(this.UserCodeNamespace)
                    ? this.UserCodeNamespace
                    : this.MethodCallData.TypeName;

                var codeFunction = !string.IsNullOrEmpty(this.UserCodeFunction)
                    ? this.UserCodeFunction
                    : this.MethodCallData.MethodName;

                AttribDefs.CodeNamespace.TrySetValue(attribValues, codeNamespace);
                AttribDefs.CodeFunction.TrySetValue(attribValues, codeFunction);
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
            if(!string.IsNullOrWhiteSpace(SegmentNameOverride))
            {
                return SegmentNameOverride;
            }

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

        public ISpan SetName(string name)
        {
            SegmentNameOverride = name;
            return this;
        }

        public string GetCategory()
        {
            return EnumNameCache<SpanCategory>.GetName(Data.SpanCategory);
        }
    }
}
