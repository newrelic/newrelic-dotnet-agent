// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

namespace CompositeTests.HybridAgent.Helpers;

public static class OpenTelemetryOperations
{
    public static ActivitySource TestAppActivitySource = new("TestApp activity source", "1.2.3");

    public static void DoWorkInSpan(string spanName, ActivityKind activityKind, Action work)
    {
        using var activity = TestAppActivitySource.StartActivity(spanName, activityKind);

        work();
    }

    public static void DoWorkInSpanWithRemoteParent(string spanName, ActivityKind activityKind, Action work)
    {
        var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
        using var activity = TestAppActivitySource.StartActivity(spanName, activityKind, parentContext);

        work();
    }

    public static void DoWorkInSpanWithInboundContext(string spanName, ActivityKind activityKind, InboundContext inboundContext, Action work)
    {
        var parentContext = GetActivityContextFromInboundContext(inboundContext);
        using var activity = TestAppActivitySource.StartActivity(spanName, activityKind, parentContext);

        work();
    }

    private static ActivityContext GetActivityContextFromInboundContext(InboundContext inboundContext)
    {
        var otelPropagator = DistributedContextPropagator.Current;
        otelPropagator.ExtractTraceIdAndState(inboundContext, (object carrier, string fieldName, out string fieldValue, out IEnumerable<string> fieldValues) =>
        {
            if (carrier == null)
            {
                fieldValue = null;
                fieldValues = null;
                return;
            }

            var typedCarrier = (InboundContext)carrier;
            switch (fieldName.ToLower())
            {
                case "traceparent":
                    fieldValue = typedCarrier.GetTraceParentHeader();
                    break;
                case "tracestate":
                    fieldValue = typedCarrier.GetTraceStateHeader();
                    break;
                default:
                    fieldValue = null;
                    break;
            }

            fieldValues = null;
        }, out var traceParent, out var traceState);

        if (!ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var context))
        {
            throw new Exception("Failed to parse traceparent and tracestate from inbound context.");
        }
        return context;
    }

    public static void AddAttributeToCurrentSpan(string key, object value, string type, Action work)
    {
        // convert value to the type specified, if type is provided
        var convertedValue = value;
        if (value != null && !string.IsNullOrWhiteSpace(type))
        {
            switch (type.ToLowerInvariant())
            {
                case "int":
                case "int32":
                    convertedValue = Convert.ToInt32(value);
                    break;
                case "long":
                case "int64":
                    convertedValue = Convert.ToInt64(value);
                    break;
                case "double":
                    convertedValue = Convert.ToDouble(value);
                    break;
                case "float":
                case "single":
                    convertedValue = Convert.ToSingle(value);
                    break;
                case "bool":
                case "boolean":
                    convertedValue = Convert.ToBoolean(value);
                    break;
                case "string":
                    convertedValue = Convert.ToString(value);
                    break;
                default:
                    // fallback: try to use the original value
                    break;
            }
        }
        Activity.Current?.AddTag(key, convertedValue);
        work();
    }

    public static void AddSpanLink(string linkedTraceId, string linkedSpanId, IDictionary<string, object> attributes, Action work)
    {
        var currentActivity = Activity.Current;
        if (currentActivity == null)
        {
            Console.WriteLine("No current activity to add link to");
            work();
            return;
        }

        var linkedContext = new ActivityContext(
            ActivityTraceId.CreateFromString(linkedTraceId.AsSpan()),
            ActivitySpanId.CreateFromString(linkedSpanId.AsSpan()),
            ActivityTraceFlags.Recorded,
            traceState: null);

        var activityTags = attributes != null 
            ? new ActivityTagsCollection(attributes) 
            : null;

        currentActivity.AddLink(new ActivityLink(linkedContext, activityTags));

        work();
    }

    public static void AddSpanEvent(string eventName, IDictionary<string, object> attributes, Action work)
    {
        var currentActivity = Activity.Current;
        if (currentActivity == null)
        {
            Console.WriteLine("No current activity to add event to");
            work();
            return;
        }

        var activityTags = attributes != null 
            ? new ActivityTagsCollection(attributes) 
            : null;

        currentActivity.AddEvent(new ActivityEvent(eventName, tags: activityTags));

        work();
    }

    public static void AssertNotValidSpan()
    {
        Assert.That(Activity.Current, Is.Null, "Expected no active span, but found one.");
    }

    public static object GetCurrentTraceId()
    {
        return Activity.Current!.TraceId.ToString();
    }

    public static object GetCurrentSpanId()
    {
        return Activity.Current!.SpanId.ToString();
    }

    public static void RecordExceptionOnSpan(string errorMessage, Action work)
    {
        Activity.Current?.AddException(new Exception(errorMessage));
        Activity.Current?.SetStatus(ActivityStatusCode.Error, errorMessage);

        work();
    }

    public static void SetOkStatusOnSpan(Action work)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Ok);
        work();
    }

    public static void SetErrorStatusOnSpan(string statusDescription, Action work)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error, statusDescription);

        work();
    }

    public static void InjectHeaders(Action work)
    {
        var externalCall = SimulatedOperations.GetCurrentExternalCall()!;

        var otelPropagator = DistributedContextPropagator.Current;

        otelPropagator.Inject(Activity.Current, externalCall, (call, headerName, headerValue) => ((ExternalCallLibrary)call!).Headers[headerName] = headerValue);

        work();
    }
}
