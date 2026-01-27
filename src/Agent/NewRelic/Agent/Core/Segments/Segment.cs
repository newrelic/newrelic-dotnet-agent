// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Segments;

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

/// <summary>
/// Interface for a link to another span.  Used for distributed tracing.
/// </summary>
public interface ISpanLink
{
    string LinkedTraceId { get; }
    string LinkedSpanId { get; }
    SpanAttributeValueCollection Attributes { get; }
}

/// <summary>
/// Represents a link to a span in a distributed trace, including its trace ID, span ID, and associated attributes.
/// </summary>
/// <remarks>A <see cref="SpanLink"/> is used to associate a span with another span in a distributed
/// trace,  typically to represent causal relationships or dependencies between spans.  This is useful in scenarios
/// such as linking a span to a parent span in a different trace or  associating spans across asynchronous
/// operations.</remarks>
public class SpanLink : ISpanLink
{
    public SpanLink(string linkedTraceId, string linkedSpanId)
    {
        LinkedTraceId = linkedTraceId;
        LinkedSpanId = linkedSpanId;
        Attributes = new SpanAttributeValueCollection();
    }
    public string LinkedTraceId { get; }
    public string LinkedSpanId { get; }
    public SpanAttributeValueCollection Attributes { get; }
}

/// <summary>
/// Represents an event that occurs within a span, including its name, timestamp, and associated attributes.
/// </summary>
/// <remarks>This interface is typically used to describe events that are part of a distributed tracing
/// span. Each event includes a name, a timestamp indicating when the event occurred, and a collection of attributes
/// that provide additional context about the event.</remarks>
public interface ISpanEventEvent
{
    string Name { get; }
    DateTime Timestamp { get; } // Epoch time in milliseconds
    SpanAttributeValueCollection Attributes { get; }
}

/// <summary>
/// Represents an event that occurred within a span, including its name, timestamp, and associated attributes.
/// </summary>
public class SpanEventEvent : ISpanEventEvent
{
    public SpanEventEvent(string name, DateTime timestamp)
    {
        Name = name;
        Timestamp = timestamp;
        Attributes = new SpanAttributeValueCollection();
    }
    public string Name { get; }
    public DateTime Timestamp { get; } // Epoch time in milliseconds
    public SpanAttributeValueCollection Attributes { get; }
}

public class Segment : IInternalSpan, ISegmentDataState, IHybridAgentSegment
{
    private static ConfigurationSubscriber _configurationSubscriber = new ConfigurationSubscriber();

    public IAttributeDefinitions AttribDefs => _transactionSegmentState.AttribDefs;
    public string TypeName => MethodCallData.TypeName;

    private SpanAttributeValueCollection _attribValues;

    private const int MaxSpanEventsPerSegment = 100;
    private const int MaxSpanLinksPerSegment = 100;

    public InterlockedCounter SpanEventLinksDropped { get; } = new();
    public InterlockedCounter SpanEventEventsDropped { get; } = new();

    public List<ISpanLink> Links { get; } = new();
    public List<ISpanEventEvent> Events { get; } = new();

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
        IsAsync = methodCallData.IsAsync;
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
        IsAsync = methodCallData.IsAsync;
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
        IsAsync = segment.IsAsync;
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
            return _spanId ?? (_spanId = _activity?.SpanId ?? GuidGenerator.GenerateNewRelicGuid());
        }
        set
        {
            _spanId = value;
        }
    }

    public string TryGetActivityTraceId() => _activity?.TraceId;

    public void End()
    {
        // this segment may have already been forced to end
        if (RelativeEndTime.HasValue == false)
        {
            // This order is to ensure the segment end time is correct, but also not mark the segment as IsDone so that CleanUp ignores it.
            var endTime = _transactionSegmentState.GetRelativeTime();
            Agent.Instance?.StackExchangeRedisCache?.Harvest(this);
            RelativeEndTime = endTime;

            if (_activity != null && !_activity.IsStopped)
            {
                _activity.Stop();
            }

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

    private INewRelicActivity _activity;
    public void SetActivity(INewRelicActivity activity)
    {
        _activity = activity;
    }

    public void MakeCombinable()
    {
        Combinable = true;
    }

    public void RemoveSegmentFromCallStack()
    {
        _transactionSegmentState.CallStackPop(this);

        UpdateCurrentActivity();
    }

    private void UpdateCurrentActivity()
    {
        var transaction = GetTransactionFromSegment();
        if (transaction is IHybridAgentTransaction && transaction.CurrentSegment.IsValid)
        {
            if (transaction.CurrentSegment is IHybridAgentSegment hybridSegment)
            {
                hybridSegment.MakeActivityCurrent();
            }
        }
        else
        {
            Log.Finest("Segment.RemoveSegmentFromCallStack: NoOpSegment, setting Activity.Current to null");
            ActivityBridgeHelpers.SetCurrentActivity(null);

            if (Log.IsFinestEnabled) Log.Finest($"Segment.RemoveSegmentFromCallStack: Activity.Current is now: {((dynamic)ActivityBridgeHelpers.GetCurrentActivity())?.Id ?? "null"}");
        }
    }

    public void SetMessageBrokerDestination(string destination)
    {
        if (SegmentData is MessageBrokerSegmentData)
        {
            var messageBrokerSegmentData = SegmentData as MessageBrokerSegmentData;
            messageBrokerSegmentData!.Destination = destination;
        }
    }

    private const long NoEndTime = -1;
    internal static NoOpSegment NoOpSegment = new NoOpSegment();
    protected readonly static IEnumerable<KeyValuePair<string, object>> EmptyImmutableParameters = new KeyValuePair<string, object>[0];

    private readonly ITransactionSegmentState _transactionSegmentState;
    protected IEnumerable<KeyValuePair<string, object>> _parameters = EmptyImmutableParameters;
    private volatile bool _parentNotified;
    private long _childDurationTicks = 0;

    public ITransaction GetTransactionFromSegment()
    {
        return _transactionSegmentState as ITransaction;
    }

    public bool ActivityStartedTransaction { get; set; } = false;

    public void MakeActivityCurrent()
    {
        if (Log.IsFinestEnabled) Log.Finest($"Segment.MakeActivityCurrent: Setting Activity.Current to this segment's activity: {_activity?.Id ?? "null"}");

        _activity?.MakeCurrent();

        if (Log.IsFinestEnabled) Log.Finest($"Segment.MakeActivityCurrent: Activity.Current is now: {((dynamic)ActivityBridgeHelpers.GetCurrentActivity())?.Id ?? "null"}");
    }

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

    public bool IsAsync { get; private set; }

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
        var attribValues = _attribValues ?? new SpanAttributeValueCollection();

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

        if (!IsAsync)
        {
            AttribDefs.ThreadId.TrySetValue(attribValues, ThreadId);
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
        if (!string.IsNullOrWhiteSpace(SegmentNameOverride))
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
        return $"Id={UniqueId},ParentId={ParentUniqueId?.ToString() ?? "Root"},Name={GetTransactionTraceName()},IsLeaf={IsLeaf},Combinable={Combinable},MethodCallData={MethodCallData}";
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

    public bool TryAddEventToSpan(string name, DateTime timestamp, IEnumerable<KeyValuePair<string, object>> attributes)
    {
        if (Events.Count >= MaxSpanEventsPerSegment)
        {
            SpanEventEventsDropped.Increment();
            return false;
        }

        var spanEvent = new SpanEventEvent(name, timestamp);
        if (attributes != null)
        {
            foreach (var attribute in attributes)
            {
                spanEvent.Attributes.TrySetValue(AttribDefs.GetCustomAttributeForSpan(attribute.Key),
                    attribute.Value);
            }
        }

        Events.Add(spanEvent);
        return true;
    }

    public bool TryAddLinkToSpan(string linkedTraceId, string linkedSpanId, IEnumerable<KeyValuePair<string, object>> attributes)
    {
        if (Links.Count >= MaxSpanLinksPerSegment)
        {
            SpanEventLinksDropped.Increment();
            return false;
        }

        var spanLink = new SpanLink(linkedTraceId, linkedSpanId);
        if (attributes != null)
        {
            foreach (var attribute in attributes)
            {
                spanLink.Attributes.TrySetValue(AttribDefs.GetCustomAttributeForSpan(attribute.Key), attribute.Value);
            }
        }
        Links.Add(spanLink);
        return true;
    }


    private readonly object _attribValuesSyncRoot = new object();

    public ISpan AddCustomAttribute(string key, object value)
    {
        SpanAttributeValueCollection customAttribValues;
        lock (_attribValuesSyncRoot)
        {
            customAttribValues = _attribValues ?? (_attribValues = new SpanAttributeValueCollection());
        }

        AttribDefs.GetCustomAttributeForSpan(key).TrySetValue(customAttribValues, value);

        return this;
    }

    public ISpan AddCloudSdkAttribute(string key, object value)
    {
        SpanAttributeValueCollection attribValues;
        lock (_attribValuesSyncRoot)
        {
            attribValues = _attribValues ?? (_attribValues = new SpanAttributeValueCollection());
        }

        AttribDefs.GetCloudSdkAttribute(key).TrySetValue(attribValues, value);

        return this;
    }

    public ISpan AddAgentAttribute(string key, object value)
    {
        SpanAttributeValueCollection attribValues;
        lock (_attribValuesSyncRoot)
        {
            attribValues = _attribValues ??= new SpanAttributeValueCollection();
        }
        AttribDefs.GetAgentAttributeForSpan(key).TrySetValue(attribValues, value);
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

// TODO: Rename this experimental to something else, or find a better way to solve this problem.
// This is merely an attempt to prevent needing to store a reference to the transaction directly on the
// activity class instance.
public interface IHybridAgentSegment
{
    ITransaction GetTransactionFromSegment();

    bool ActivityStartedTransaction { get; set; }
    void MakeActivityCurrent();

    bool TryAddEventToSpan(string name, DateTime timestamp, IEnumerable<KeyValuePair<string, object>> attributes);

    bool TryAddLinkToSpan(string linkedTraceId, string linkedSpanId, IEnumerable<KeyValuePair<string, object>> attributes);
}
