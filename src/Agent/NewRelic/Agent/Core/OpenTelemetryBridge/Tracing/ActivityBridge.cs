// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;

public class ActivityBridge : IDisposable
{
    public const string TemporarySegmentName = "temp segment name";

    private IAgent _agent;
    private IErrorService _errorService;

    private dynamic _activityListener;

    private static Action<object, bool> _activityTraceFlagsSetter = null;

    // Static Method and MethodCall for ActivityStarted
    private static readonly Method ActivityStartedMethod = new Method(typeof(ActivityBridge), nameof(ActivityStarted), "object,IAgent");
    private static readonly MethodCall ActivityStartedMethodCall = new MethodCall(ActivityStartedMethod, null, Array.Empty<object>(), false);

    public ActivityBridge(IAgent agent, IErrorService errorService)
    {
        _agent = agent;
        _errorService = errorService;
    }

    public bool Start()
    {
        if (!_agent.Configuration.OpenTelemetryTracingEnabled)
        {
            Log.Debug("Open Telemetry Tracing Bridge is disabled via configuration.");
            return true;
        }

        if (_activityListener != null)
        {
            Log.Debug("OpenTelemetry Tracing Bridge has already been created. Not starting a new one.");
            return false;
        }

        Log.Debug("OpenTelemetry Tracing Bridge is enabled. Starting the activity listener.");
        return TryCreateActivityListener();
    }

    public void Dispose()
    {
        _activityListener?.Dispose();
        _activityListener = null;
    }

    // Only .net 5 and newer libraries and applications will use the W3C id format by default.
    // We need to set the default id format to W3C to ensure that the activities we create are created
    // with the expected format.
    private static void SetDefaultActivityIdFormat(Type activityType)
    {
        var defaultIdFormatPropertyInfo = activityType.GetProperty("DefaultIdFormat", BindingFlags.Public | BindingFlags.Static);
        defaultIdFormatPropertyInfo.SetValue(null, ActivityIdFormat.W3C);
    }


    #region Configuration

    private bool TryCreateActivityListener()
    {
        // look for diagnostic source assembly -- if it's not found, manually load it
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
        if (assembly == null)
        {
            Log.Debug("System.Diagnostics.DiagnosticSource assembly not found. Attempting to load it manually.");
            try
            {
                assembly = Assembly.Load("System.Diagnostics.DiagnosticSource");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error loading System.Diagnostics.DiagnosticSource assembly. Not starting the activity listener.");
                return false;
            }
        }

        Log.Debug($"Found System.Diagnostics.DiagnosticSource assembly version {assembly.GetName().Version}.");
        if ((assembly.GetName().Version?.Major ?? 0) < 7)
        {
            Log.Debug("DiagnosticSource assembly not found or version < 7 is not compatible with OpenTelemetry Bridge. Not starting the activity listener.");
            return false;
        }

        var activityListenerType = assembly.GetType("System.Diagnostics.ActivityListener", throwOnError: false);
        var activitySourceType = assembly.GetType("System.Diagnostics.ActivitySource", throwOnError: false);

        if (activityListenerType == null || activitySourceType == null)
        {
            Log.Debug("ActivityListener or ActivitySource type not found in DiagnosticSource assembly. Not starting the activity listener.");
            return false;
        }

        var addActivityListenerMethod = activitySourceType.GetMethod("AddActivityListener", [activityListenerType]);
        if (addActivityListenerMethod == null)
        {
            Log.Debug("AddActivityListener method not found in ActivitySource type. Not starting the activity listener.");
            return false;
        }

        var activityKindType = assembly.GetType("System.Diagnostics.ActivityKind", throwOnError: false);
        if (activityKindType == null)
        {
            Log.Debug("ActivityKind type not found in DiagnosticSource assembly. Not starting the activity listener.");
            return false;
        }

        var memoryAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Memory");
        if (memoryAssembly == null)
        {
            // System.Memory is a dependency of System.Diagnostics.DiagnosticSource, but it may not be loaded yet.
            try
            {
                memoryAssembly = Assembly.Load("System.Memory");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error loading System.Memory assembly");
                return false;
            }
        }

        var memoryExtensionsType = memoryAssembly.GetType("System.MemoryExtensions", throwOnError: false);
        if (memoryExtensionsType == null)
        {
            Log.Debug("System.MemoryExtensions type not found in System.Memory assembly. Not starting the activity listener.");
            return false;
        }

        var activityType = assembly.GetType("System.Diagnostics.Activity", throwOnError: false);
        var activityTraceIdType = assembly.GetType("System.Diagnostics.ActivityTraceId", throwOnError: false);

        SetDefaultActivityIdFormat(activityType);
        ConfigureTraceIdGenerator(activityType, activityTraceIdType, memoryExtensionsType);
        CreateActivityTraceFlagsSetter(activityType);

        _activityListener = Activator.CreateInstance(activityListenerType);

        ConfigureShouldListenToCallback(_activityListener, activityListenerType, activitySourceType);

        // Need to subscribe to ActivityStarted and ActivityStopped callbacks. These methods will be used to trigger the starting and stopping
        // of segments and potentially transactions using the agent's API.
        ConfigureActivityStartedAndStoppedCallbacks(_activityListener, activityListenerType, assembly, _agent);

        // Need to subscribe to the Sample callbacks to ensure that activities are created and attributed collected. Subscribing to the
        // SampleByParentId callback is not necessary, because the ActivitySource will fallback to the Sample callback when the W3C id format
        // is enabled so long as our listener does not define a SampleByParentId callback.
        // The decision to sample an activity can be made by multiple listeners. The highest sampling decision made by any listener is the one
        // that is applied to the activity. It's possible for activities that we do not intend to smaple with our listener, to be sampled
        // because of a decision made by another listener outside of our control.
        ConfigureSampleCallback(_activityListener, activityListenerType, assembly);

        // Consider creating and registering an ExcpetionRecorder callback to capture exceptions recorded on activities.
        // It is possible to record exceptions on activities in a way that will not trigger the ExceptionRecorder delegate,
        // so we may prefer enumerating the events on an activity to look for events weith an eventName
        // of "exception" and record those exceptions as well. The behavior of recording exceptions on Spans will likely
        // change in the future, and the exceptions will no longer be stored as events on the span.

        // Enable the listener
        addActivityListenerMethod.Invoke(null, [_activityListener]);

        // Allow agent instrumentation to create activities visible to the customer's application code.
        NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(activitySourceType, activityKindType);

        return true;
    }

    // This is used to ensure a consistent trace id is used whenever an activity is created as a direct child of
    // of a transaction (when no other parent activity or parent activity context is provided).
    // Creates a Func<ActivityTraceId> using expression syntax trees based on the following code.
    // Activity.TraceIdGenerator = () => {
    //    var currentTraceId = TryGetCurrentTraceId();
    //    if (currentTraceId != null)
    //    {
    //        return ActivityTraceId.CreateFromString(currentTraceId.AsSpan());
    //    }
    //    return ActivityTraceId.CreateRandom();
    // };

    private void ConfigureTraceIdGenerator(Type activityType, Type activityTraceIdType, Type memoryExtensionsType)
    {
        var traceIdGeneratorPropertyInfo = activityType.GetProperty("TraceIdGenerator", BindingFlags.Public | BindingFlags.Static);

        var callTryGetCurrentTraceIdMethod = typeof(ActivityBridge).GetMethod(nameof(TryGetCurrentTraceId), BindingFlags.NonPublic | BindingFlags.Instance);
        var currentTraceIdVariable = Expression.Variable(typeof(string), "currentTraceId");
        var assignCurrentTraceIdExpression = Expression.Assign(currentTraceIdVariable, Expression.Call(Expression.Constant(this), callTryGetCurrentTraceIdMethod));

        var hasCurrentTraceIdExpression = Expression.NotEqual(currentTraceIdVariable, Expression.Constant(null));

        var generateRandomTraceIdMethod = activityTraceIdType.GetMethod("CreateRandom", BindingFlags.Public | BindingFlags.Static);
        var generateNewTraceIdExpression = Expression.Call(null, generateRandomTraceIdMethod);

        // We need to call AsSpan from an expression, because we ILRepack System.Memory in our netframework build
        // of the agent which causes the ReadOnlySpan type to be internalized.
        var currentTraceIdAsSpanExpression = Expression.Call(memoryExtensionsType, "AsSpan", null, currentTraceIdVariable);
        var parseTraceIdMethod = activityTraceIdType.GetMethod("CreateFromString", BindingFlags.Public | BindingFlags.Static);
        var parseTraceIdExpression = Expression.Call(null, parseTraceIdMethod, currentTraceIdAsSpanExpression);

        var logFinestMethod = typeof(Log).GetMethod("Finest", new[] { typeof(string), typeof(object[]) });

        var messageFormat = Expression.Constant("Using current traceId {0}");
        var messageArgs = Expression.NewArrayInit(typeof(object), Expression.Convert(currentTraceIdVariable, typeof(object)));
        var callLogFinestExpression = Expression.Call(logFinestMethod, messageFormat, messageArgs);

        var ifBlockExpression = Expression.Block(
            callLogFinestExpression,
            parseTraceIdExpression
        );

        var newTraceIdVariable = Expression.Variable(activityTraceIdType, "newTraceId");
        var assignNewTraceIdExpression = Expression.Assign(newTraceIdVariable, generateNewTraceIdExpression);
        var toStringMethod = activityTraceIdType.GetMethod("ToString", Type.EmptyTypes);
        var newTraceIdAsString = Expression.Call(newTraceIdVariable, toStringMethod);

        var message2Format = Expression.Constant("Generated new traceId {0}");
        var message2Args = Expression.NewArrayInit(typeof(object), Expression.Convert(newTraceIdAsString, typeof(object)));
        var callLogFinestExpression2 = Expression.Call(logFinestMethod, message2Format, message2Args);

        var elseBlockExpression = Expression.Block(
            [newTraceIdVariable],
            assignNewTraceIdExpression,
            callLogFinestExpression2,
            newTraceIdVariable
        );

        var getOrCreateTraceIdExpression = Expression.Condition(hasCurrentTraceIdExpression, ifBlockExpression, elseBlockExpression);

        var lambdaBodyExpression = Expression.Block([currentTraceIdVariable], assignCurrentTraceIdExpression, getOrCreateTraceIdExpression);

        var traceIdGenerator = Expression.Lambda(traceIdGeneratorPropertyInfo.PropertyType, lambdaBodyExpression).Compile();
        traceIdGeneratorPropertyInfo.SetValue(null, traceIdGenerator);
    }


    private static void CreateActivityTraceFlagsSetter(Type activityType)
    {
        if (_activityTraceFlagsSetter != null)
        {
            return;
        }

        var traceFlagsPropertyInfo = activityType.GetProperty("ActivityTraceFlags", BindingFlags.Public | BindingFlags.Instance);

        var sampledParameter = Expression.Parameter(typeof(bool), "sampled");
        var activityParameter = Expression.Parameter(typeof(object), "activity");

        var typedActivity = Expression.Convert(activityParameter, activityType);
        var traceFlagsExpression = Expression.Property(typedActivity, traceFlagsPropertyInfo);

        var traceFlagsInt = Expression.Condition(sampledParameter, Expression.Constant((int)ActivityTraceFlags.Recorded), Expression.Constant((int)ActivityTraceFlags.None));
        var typedTraceFlagsValue = Expression.Convert(traceFlagsInt, traceFlagsPropertyInfo.PropertyType);

        var traceFlagsAssignment = Expression.Assign(traceFlagsExpression, typedTraceFlagsValue);

        var traceFlagsSetterLambda = Expression.Lambda<Action<object, bool>>(traceFlagsAssignment, activityParameter, sampledParameter);

        _activityTraceFlagsSetter = traceFlagsSetterLambda.Compile();
    }

    // Generates code similar to the following.
    // activityListener.ShouldListenTo = (activitySource) => instance.ShouldListenToActivitySource(activitySource);
    private void ConfigureShouldListenToCallback(object activityListener, Type activityListenerType, Type activitySourceType)
    {
        var shouldListenToProperty = activityListenerType.GetProperty("ShouldListenTo");

        var activitySourceParameter = Expression.Parameter(activitySourceType, "activitySource");

        var shouldListenToActivitySourceMethod =
            typeof(ActivityBridge).GetMethod(nameof(ShouldListenToActivitySource),
                BindingFlags.NonPublic | BindingFlags.Instance);
        var activityBridgeInstanceExpression = Expression.Constant(this);

        var shouldListenToCall = Expression.Call(activityBridgeInstanceExpression, shouldListenToActivitySourceMethod, activitySourceParameter);

        var shouldListenToLambda = Expression.Lambda(shouldListenToProperty.PropertyType, shouldListenToCall, activitySourceParameter);

        shouldListenToProperty.SetValue(activityListener, shouldListenToLambda.Compile());
    }

    // Activity will not actually be created by an activity source unless an activity listener is listening to it and the
    // activity is sampled. This method creates and registers a Sample callback that ensures that the activities we want to
    // capture are created and sampled, and will eventually have the property set that will trigger the population of all of
    // the desired tags (requires activity.AllDataRequested to be true).
    //
    // Generates code similar to the following.
    // activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ShouldSampleActivity((int)options.Kind, options.Parent) ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
    private void ConfigureSampleCallback(object activityListener, Type activityListenerType, Assembly assembly)
    {
        var sampleProperty = activityListenerType.GetProperty("Sample");
        var activityContextType = assembly.GetType("System.Diagnostics.ActivityContext", throwOnError: false);
        var activityCreationOptionsType = assembly.GetType("System.Diagnostics.ActivityCreationOptions`1", throwOnError: false);
        var activitySamplingResultType = assembly.GetType("System.Diagnostics.ActivitySamplingResult", throwOnError: false);

        // The parameter to the Sample callback is a ref ActivityCreationOptions<ActivityContext> parameter.
        var parameterType = activityCreationOptionsType.MakeGenericType(activityContextType).MakeByRefType();
        var optionsParameter = Expression.Parameter(parameterType, "options");
        // Converting the enum types here by using the int representation of the enum value (we can't use the ilrepacked enum type directly).
        var allDataAndRecordedValue = Expression.Convert(Expression.Constant((int)ActivitySamplingResult.AllDataAndRecorded), activitySamplingResultType);
        var noneValue = Expression.Convert(Expression.Constant((int)ActivitySamplingResult.None), activitySamplingResultType);

        //var optionsKindProperty = activityCreationOptionsType.GetProperty("Kind");
        //var optionsParentProperty = activityCreationOptionsType.GetProperty("Parent");
        var shouldSampleActivityMethod = typeof(ActivityBridge).GetMethod(nameof(ShouldSampleActivity), BindingFlags.NonPublic | BindingFlags.Instance);

        var kindExpression = Expression.Convert(Expression.Property(optionsParameter, "Kind"), typeof(int));
        var parentExpression = Expression.Convert(Expression.Property(optionsParameter, "Parent"), typeof(object));
        var shouldSampleActivityCall = Expression.Call(Expression.Constant(this), shouldSampleActivityMethod, kindExpression, parentExpression);

        var resultExpression = Expression.Condition(shouldSampleActivityCall, allDataAndRecordedValue, noneValue);

        var sampleLambda = Expression.Lambda(sampleProperty.PropertyType, resultExpression, optionsParameter);

        sampleProperty.SetValue(activityListener, sampleLambda.Compile());
    }

    private void ConfigureActivityStartedAndStoppedCallbacks(object activityListener, Type activityListenerType, Assembly assembly, IAgent agent)
    {
        var activityType = assembly.GetType("System.Diagnostics.Activity", throwOnError: false);
        ConfigureActivityStartedCallback(activityListener, activityListenerType, activityType, agent);
        ConfigureActivityStoppedCallback(activityListener, activityListenerType, activityType);
    }

    private static void ConfigureActivityStartedCallback(object activityListener, Type activityListenerType, Type activityType, IAgent agent)
    {
        var activityStartedProperty = activityListenerType.GetProperty("ActivityStarted");
        var activityStartedMethod = typeof(ActivityBridge).GetMethod(nameof(ActivityStarted), BindingFlags.NonPublic | BindingFlags.Static);

        var activityParameter = Expression.Parameter(activityType, "activity");
        var agentInstance = Expression.Constant(agent);
        var activityStartedCall = Expression.Call(null, activityStartedMethod, activityParameter, agentInstance);
        var activityStartedLambda = Expression.Lambda(activityStartedProperty.PropertyType, activityStartedCall, activityParameter);

        activityStartedProperty.SetValue(activityListener, activityStartedLambda.Compile());
    }

    private void ConfigureActivityStoppedCallback(object activityListener, Type activityListenerType, Type activityType)
    {
        var activityStoppedProperty = activityListenerType.GetProperty("ActivityStopped");
        var activityStoppedMethod = typeof(ActivityBridge).GetMethod(nameof(ActivityStopped), BindingFlags.NonPublic | BindingFlags.Static);

        var activityParameter = Expression.Parameter(activityType, "activity");
        var agentInstance = Expression.Constant(_agent);
        var errorServiceInstance = Expression.Constant(_errorService);
        var activityStoppedCall = Expression.Call(null, activityStoppedMethod, activityParameter, agentInstance, errorServiceInstance);
        var activityStoppedLambda = Expression.Lambda(activityStoppedProperty.PropertyType, activityStoppedCall, activityParameter);

        activityStoppedProperty.SetValue(activityListener, activityStoppedLambda.Compile());
    }

    #endregion


    #region Callbacks

    private string TryGetCurrentTraceId()
    {
        var transaction = _agent.CurrentTransaction;
        if (transaction.IsValid && !transaction.IsFinished && transaction is IHybridAgentTransaction hybridAgentTransaction)
        {
            return hybridAgentTransaction.TraceId;
        }
        return null;
    }

    private bool ShouldListenToActivitySource(object activitySource)
    {
        dynamic dynamicActivitySource = activitySource;
        string activitySourceName = (string)dynamicActivitySource.Name;

        if (string.IsNullOrWhiteSpace(activitySourceName))
        {
            Log.Finest("ShouldListenToActivitySource: Activity source name is null or empty. Not listening.");
            return false;
        }

        return ActivitySourceExtensions.ShouldListenToActivitySource(activitySourceName, _agent.Configuration);
    }

    private bool ShouldSampleActivity(int kind, object activityContext)
    {
        // If there is a transaction already in progress, we should sample the activity.
        var transaction = _agent.CurrentTransaction;
        if (transaction.IsValid && !transaction.IsFinished)
        {
            return true;
        }

        var activityKind = (ActivityKind)kind;
        dynamic dynamicActivityContext = activityContext;
        if ((activityContext != null && (bool)dynamicActivityContext.IsRemote) || activityKind == ActivityKind.Server || activityKind == ActivityKind.Consumer)
        {
            return true;
        }

        return false;
    }

    private static void ActivityStarted(object originalActivity, IAgent agent)
    {
        // TODO: Much of this code was copied from the WrapperService, can we share this code between the two classes?

        // This method will be called when an activity is started. This is where we would start a segment or transaction.
        var transaction = agent.CurrentTransaction;
        var existingTransactionRequired = ActivityBridgeHelpers.IsTransactionRequiredForActivity(originalActivity);
        dynamic activity = originalActivity;

        var activityId = activity.Id;

        if (transaction.IsFinished)
        {
            if (Log.IsFinestEnabled)
            {
                if (existingTransactionRequired)
                    transaction.LogFinest($"Transaction has already ended, skipping activity {activityId}.");
                else
                    transaction.LogFinest("Transaction has already ended, detaching from transaction storage context.");
            }

            transaction.Detach();
            transaction = agent.CurrentTransaction;

            if (existingTransactionRequired)
                return;
        }

        if (existingTransactionRequired)
        {
            if (!transaction.IsValid)
            {
                if (Log.IsFinestEnabled)
                    transaction.LogFinest($"No transaction, skipping activity {activityId}.");
                return;
            }

            if (transaction.CurrentSegment.IsLeaf)
            {
                if (Log.IsFinestEnabled)
                    transaction.LogFinest($"Parent segment is a leaf segment, skipping activity {activityId}.");
                return;
            }
        }

        bool shouldStartTransaction = ActivityBridgeHelpers.ShouldStartTransactionForActivity(originalActivity);
        if (shouldStartTransaction)
        {
            transaction = ActivityBridgeHelpers.StartTransactionForActivity(originalActivity, agent);

            // We need to accept the distributed tracing context before we start any segments in the transaction
            // so that OTel view tracing and the New Relic view of the transaction are consistent.
            transaction.AcceptDistributedTraceHeaders(originalActivity, ActivityBridgeHelpers.GetTraceContextHeadersFromActivity, TransportType.Unknown);
        }

        // TODO: We need a better way to detect activities created by a segment.  Could we use a custom property instead?
        if (activity.DisplayName != TemporarySegmentName)
        {
            if (transaction.GetExperimentalApi().StartActivitySegment(ActivityStartedMethodCall, new RuntimeNewRelicActivity(originalActivity)) is IHybridAgentSegment segment)
            {
                segment.ActivityStartedTransaction = shouldStartTransaction;
            }
        }

        if (transaction is IHybridAgentTransaction hybridAgentTransaction)
        {
            // Update the activity to contain the expected trace flags and trace state that the New Relic
            // data model expects.
            if (hybridAgentTransaction.TryGetTraceFlagsAndState(out var sampled, out var traceStateString))
            {
                activity.TraceStateString = traceStateString;
                SetTraceFlags(activity, sampled);
            }
        }
    }
    private static void SetTraceFlags(object activity, bool sampled)
    {
        _activityTraceFlagsSetter(activity, sampled);
    }


    private static void ActivityStopped(object originalActivity, IAgent agent, IErrorService errorService)
    {
        // This method will be called when an activity is stopped. This is where we would end a segment or transaction.
        var segment = RuntimeNewRelicActivity.GetSegmentFromActivity(originalActivity);

        if (segment != null)
        {
            // set status code (and description, if available) as agent attributes on the segment
            dynamic activity = originalActivity;
            int activityStatusCode = (int)activity.Status;
            segment.AddAgentAttribute("status.code", activityStatusCode.ToActivityStatusCodeString());

            // description is currently only available when status is Error
            string activityStatusDescription = (string)activity.StatusDescription;
            if (!string.IsNullOrWhiteSpace(activityStatusDescription))
            {
                segment.AddAgentAttribute("status.description", activityStatusDescription);
            }

            segment.ProcessActivityTags(originalActivity, agent, errorService);
            segment.AddExceptionEventInformationToSegment(originalActivity, errorService);
            segment.CaptureEventsOnSpan(originalActivity);
            segment.CaptureSpanLinks(originalActivity);
            segment.End();
        }

        if (segment is IHybridAgentSegment { ActivityStartedTransaction: true } hybridAgentSegment)
        {
            var transaction = hybridAgentSegment.GetTransactionFromSegment();
            transaction?.End();
        }
    }
    #endregion

}

public static class ActivitySourceExtensions
{
    // static to allow unit testing to verify logic without needing to create an ActivityBridge instance
    public static bool ShouldListenToActivitySource(string activitySourceName, IConfiguration config)
    {
        var defaultExcludedActivitySources = config.OpenTelemetryTracingDefaultExcludedActivitySources;
        var customerIncludedActivitySources = config.OpenTelemetryTracingIncludedActivitySources;
        var customerExcludedActivitySources = config.OpenTelemetryTracingExcludedActivitySources;

        // priority from lowest to highest:
        // Default Excluded Activity Sources
        // Customer Included Activity Sources
        // Customer Excluded Activity Sources
        // --> items on the customer include list override the default exclude list
        // --> items on the customer exclude list override the customer include list
        // --> activity sources not in any exclude list are included by default
        var isExcludedByDefault = defaultExcludedActivitySources.Contains(activitySourceName, StringComparer.CurrentCultureIgnoreCase);
        var isIncludedByCustomer = customerIncludedActivitySources.Contains(activitySourceName, StringComparer.CurrentCultureIgnoreCase);
        var isExcludedByCustomer = customerExcludedActivitySources.Contains(activitySourceName, StringComparer.CurrentCultureIgnoreCase);

        bool shouldListenToActivitySource = !isExcludedByCustomer && (!isExcludedByDefault || isIncludedByCustomer);

        Log.Finest($"ShouldListenToActivitySource: {(shouldListenToActivitySource ? "Listening to" : "Not listening to")} {activitySourceName}.");

        return shouldListenToActivitySource;
    }
}
