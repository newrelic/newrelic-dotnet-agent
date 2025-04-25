// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    // TODO: Review all usages of the Activity class to ensure that we are only using the things are available in the
    // supported versions of the DiagnosticSource assembly. We could also support some backwards compatibility by only
    // having certain features available in the necessary properties or methods are available.
    public class ActivityBridge : IDisposable
    {
        private IAgent _agent;
        private IErrorService _errorService;

        private dynamic _activityListener;

        private static Action<object, bool> _activityTraceFlagsSetter = null;

        public ActivityBridge(IAgent agent, IErrorService errorService)
        {
            _agent = agent;
            _errorService = errorService;
        }

        public void Start()
        {
            TryCreateActivityListener();
        }

        private void TryCreateActivityListener()
        {
            // TODO: Add logging to indicate why the activity listener was not created in the failure scenarios

            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");

            // TODO: Enforce that an appropriate minimum version of the DiagnosticSource assembly is loaded.
            if (assembly == null || (assembly.GetName().Version?.Major ?? 0) < 7)
            {
                return;
            }

            var activityListenerType = assembly.GetType("System.Diagnostics.ActivityListener", throwOnError: false);
            var activitySourceType = assembly.GetType("System.Diagnostics.ActivitySource", throwOnError: false);

            if (activityListenerType == null || activitySourceType == null)
            {
                return;
            }

            var memoryAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Memory");
            if (memoryAssembly == null)
            {
                // System.Memory is a dependency of System.Diagnostics.DiagnosticSource, but it may not be loaded yet.
                // TODO: Wrap this in a try catch block to handle the case where the assembly is not found and log the error.
                memoryAssembly = Assembly.Load("System.Memory");
            }

            var memoryExtensionsType = memoryAssembly.GetType("System.MemoryExtensions", throwOnError: false);
            if (memoryExtensionsType == null)
            {
                return;
            }

            var activityType = assembly.GetType("System.Diagnostics.Activity", throwOnError: false);
            var activityTraceIdType = assembly.GetType("System.Diagnostics.ActivityTraceId", throwOnError: false);

            SetDefaultActivityIdFormat(activityType);
            ConfigureTraceIdGenerator(activityType, activityTraceIdType, memoryExtensionsType);
            CreateActivityTraceFlagsSetter(activityType);

            _activityListener = Activator.CreateInstance(activityListenerType);

            ConfigureShouldListenToCallback(this, _activityListener, activityListenerType, activitySourceType);

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
            var addActivityListenerMethod = activitySourceType.GetMethod("AddActivityListener", [activityListenerType]);
            addActivityListenerMethod.Invoke(null, new object[] { _activityListener });

            // Allow agent instrumentation to create activities visible to the customer's application code.
            var activityKindType = assembly.GetType("System.Diagnostics.ActivityKind", throwOnError: false);
            NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(activitySourceType, activityKindType);
        }

        public void Dispose()
        {
            _activityListener?.Dispose();
        }

        // Only .net 5 and newer libraries and applications will use the W3C id format by default.
        // We need to set the default id format to W3C to ensure that the activities we create are created
        // with the expected format.
        private static void SetDefaultActivityIdFormat(Type activityType)
        {
            var defaultIdFormatPropertyInfo = activityType.GetProperty("DefaultIdFormat", BindingFlags.Public | BindingFlags.Static);
            defaultIdFormatPropertyInfo.SetValue(null, ActivityIdFormat.W3C);
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

            var getOrCreateTraceIdExpression = Expression.Condition(hasCurrentTraceIdExpression, parseTraceIdExpression, generateNewTraceIdExpression);

            var lambdaBodyExpression = Expression.Block([currentTraceIdVariable], assignCurrentTraceIdExpression, getOrCreateTraceIdExpression);

            var traceIdGenerator = Expression.Lambda(traceIdGeneratorPropertyInfo.PropertyType, lambdaBodyExpression).Compile();
            traceIdGeneratorPropertyInfo.SetValue(null, traceIdGenerator);
        }

        private string TryGetCurrentTraceId()
        {
            var transaction = _agent.CurrentTransaction;
            var hybridAgentTransaction = transaction as IHybridAgentTransaction;
            if (transaction.IsValid && !transaction.IsFinished && hybridAgentTransaction != null)
            {
                return hybridAgentTransaction.TraceId;
            }
            return null;
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
        private static void ConfigureShouldListenToCallback(ActivityBridge instance, object activityListener, Type activityListenerType, Type activitySourceType)
        {
            var shouldListenToProperty = activityListenerType.GetProperty("ShouldListenTo");

            var activitySourceParameter = Expression.Parameter(activitySourceType, "activitySource");

            var shouldListenToActivitySourceMethod =
                typeof(ActivityBridge).GetMethod(nameof(ShouldListenToActivitySource),
                    BindingFlags.NonPublic | BindingFlags.Instance);
            var activityBridgeInstanceExpression = Expression.Constant(instance);

            var shouldListenToCall = Expression.Call(activityBridgeInstanceExpression, shouldListenToActivitySourceMethod, activitySourceParameter);

            var shouldListenToLambda = Expression.Lambda(shouldListenToProperty.PropertyType, shouldListenToCall, activitySourceParameter);

            shouldListenToProperty.SetValue(activityListener, shouldListenToLambda.Compile());
        }

        private bool ShouldListenToActivitySource(object activitySource)
        {
            dynamic dynamicActivitySource = activitySource;
            string activitySourceName = (string)dynamicActivitySource.Name;

            var includedActivitySources = _agent.Configuration.IncludedActivitySources;
            var excludedActivitySources = _agent.Configuration.ExcludedActivitySources;

            return !string.IsNullOrEmpty(activitySourceName)
                   && includedActivitySources.Contains(activitySourceName)
                   && !excludedActivitySources.Contains(activitySourceName);
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

        private static void ActivityStarted(object originalActivity, IAgent agent)
        {
            // TODO: Much of this code was copied from the WrapperService, can we share this code between the two classes?

            // This method will be called when an activity is started. This is where we would start a segment or transaction.
            var transaction = agent.CurrentTransaction;
            var existingTransactionRequired = IsTransactionRequiredForActivity(originalActivity);
            dynamic activity = originalActivity;

            if (transaction.IsFinished)
            {
                if (Log.IsFinestEnabled)
                {
                    if (existingTransactionRequired)
                    {
                        transaction.LogFinest($"Transaction has already ended, skipping activity {activity.Id}.");
                    }
                    else
                    {
                        transaction.LogFinest("Transaction has already ended, detaching from transaction storage context.");
                    }
                }

                transaction.Detach();
                transaction = agent.CurrentTransaction;

                if (existingTransactionRequired)
                {
                    return;
                }
            }

            if (existingTransactionRequired)
            {
                if (!transaction.IsValid)
                {
                    if (Log.IsFinestEnabled)
                    {
                        transaction.LogFinest($"No transaction, skipping activity {activity.Id}.");
                    }

                    return;
                }

                if (transaction.CurrentSegment.IsLeaf)
                {
                    if (Log.IsFinestEnabled)
                    {
                        transaction.LogFinest($"Parent segment is a leaf segment, skipping activity {activity.Id}.");
                    }
                    return;
                }
            }

            bool shouldStartTransaction = ShouldStartTransactionForActivity(originalActivity);
            if (shouldStartTransaction)
            {
                transaction = StartTransactionForActivity(originalActivity, agent);

                // We need to accept the distributed tracing context before we start any segments in the transaction
                // so that OTel view tracing and the New Relic view of the transaction are consistent.
                transaction.AcceptDistributedTraceHeaders(originalActivity, GetTraceContextHeadersFromActivity, TransportType.Unknown);
            }

            // TODO: We need a better way to detect activities created by a segment.
            if (activity.DisplayName != "temp segment name")
            {
                var method = new Method(typeof(ActivityBridge), nameof(ActivityStarted), "object,IAgent");
                var methodCall = new MethodCall(method, null, Array.Empty<object>(), false);
                var segment = transaction.StartActivitySegment(methodCall, new RuntimeNewRelicActivity(originalActivity)) as IHybridAgentSegment;

                if (segment != null)
                {
                    segment.ActivityStartedTransaction = shouldStartTransaction;
                }
            }

            var hybridAgentTransaction = transaction as IHybridAgentTransaction;
            if (hybridAgentTransaction != null)
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

        private static bool IsTransactionRequiredForActivity(object originalActivity)
        {
            // TODO: Determine if this is the right thing to do. Our wrapper service separates these concepts.
            return !ShouldStartTransactionForActivity(originalActivity);
        }

        private static readonly List<int> _activityKindsThatStartATransaction = new List<int>
        {
            (int)ActivityKind.Server,
            (int)ActivityKind.Consumer
        };

        private static bool ShouldStartTransactionForActivity(object originalActivity)
        {
            dynamic activity = originalActivity;
            
            return (bool)activity.HasRemoteParent || _activityKindsThatStartATransaction.Contains((int)activity.Kind);
        }

        private static ITransaction StartTransactionForActivity(object originalActivity, IAgent agent)
        {
            dynamic activity = originalActivity;

            bool isWeb = (int)activity.Kind == (int)ActivityKind.Server;

            return agent.CreateTransaction(isWeb, "Activity", activity.DisplayName, doNotTrackAsUnitOfWork: true);
        }

        private static IEnumerable<string> GetTraceContextHeadersFromActivity(object originalActivity, string headerName)
        {
            dynamic activity = originalActivity;
            switch (headerName)
            {
                case "traceparent":
                    return [(string)activity.ParentId];
                case "tracestate":
                    return [(string)activity.TraceStateString ?? string.Empty];
                default:
                    return Enumerable.Empty<string>();
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
                AddActivityTagsToSegment(originalActivity, segment);
                AddExceptionEventInformationToSegment(originalActivity, segment, errorService);
                segment.End();
            }

            var hybridAgentSegment = segment as IHybridAgentSegment;
            if (hybridAgentSegment != null && hybridAgentSegment.ActivityStartedTransaction)
            {
                if (hybridAgentSegment.TryGetTransactionFromSegment(out var transaction))
                {
                    transaction.End();
                }
            }
        }

        private static void AddActivityTagsToSegment(object originalActivity, ISegment segment)
        {
            dynamic activity = originalActivity;
            foreach (var tag in activity.TagObjects)
            {
                // TODO: We may not want to add all tags to the segment. We may want to filter out some tags, especially
                // the ones that we map to intrinsic or agent attributes.
                segment.AddCustomAttribute((string)tag.Key, (object)tag.Value);
            }
        }

        private static void AddExceptionEventInformationToSegment(object originalActivity, ISegment segment, IErrorService errorService)
        {
            // Exceptions recorded during an activity are currently added as events on the activity. Not every way of recording
            // an exception will trigger the ExceptionRecorder callback, so we need to enumerate the events on the activity
            // to look for events with an eventName of "exception" and record the available exception information.

            dynamic activity = originalActivity;
            foreach (var activityEvent in activity.Events)
            {
                if (activityEvent.Name == "exception")
                {
                    string exceptionMessage = null;
                    //string exceptionType = null;
                    //string exceptionStacktrace = null;

                    foreach (var tag in activityEvent.Tags)
                    {
                        if (tag.Key == "exception.message")
                        {
                            exceptionMessage = tag.Value?.ToString();
                        }
                        //else if (tag.Key == "exception.type")
                        //{
                        //    exceptionType = tag.Value?.ToString();
                        //}
                        //else if (tag.Key == "exception.stacktrace")
                        //{
                        //    exceptionStacktrace = tag.Value?.ToString();
                        //}

                        // Add all of the original attributes to the segment.
                        segment.AddCustomAttribute((string)tag.Key, (object)tag.Value);
                    }

                    if (exceptionMessage != null)
                    {

                        // TODO: The agent does not support ignoring errors by message, but if a type is available we could
                        // consider ignoring the error based on the type.

                        // TODO: In the future consider using the span status to determine if the exception is expected or not.
                        var errorData = errorService.FromMessage(exceptionMessage, (IDictionary<string, object>)null, false);
                        //var span = (IInternalSpan)segment;
                        //span.ErrorData = errorData;

                        // TODO: Record the errorData on the transaction.
                        var hybridAgentSegment = segment as IHybridAgentSegment;
                        if (hybridAgentSegment != null && hybridAgentSegment.TryGetTransactionFromSegment(out var transaction))
                        {
                            var internalTransaction = (IHybridAgentTransaction)transaction;
                            internalTransaction.NoticeErrorOnTransactionAndSegment(errorData, segment);
                        }
                    }

                    // Short circuiting the loop after finding the first exception event.
                    return;
                }
            }
        }
    }

    public class NewRelicActivitySourceProxy
    {
        public const string SegmentCustomPropertyName = "NewRelicSegment";

        private const string ActivitySourceName = "NewRelic.Agent";
        private static INewRelicActivitySource _activitySource = null;
        private static int _usingRuntimeActivitySource = 0;

        public static void SetAndCreateRuntimeActivitySource(Type activitySourceType, Type activityKindType)
        {
            // We only need to create the runtime activity source once. If it has already been created, we can return early.
            if (Interlocked.CompareExchange(ref _usingRuntimeActivitySource, 1, 0) == 1)
            {
                return;
            }

            var originalActivitySource = Interlocked.Exchange(ref _activitySource, new RuntimeActivitySource(ActivitySourceName, AgentInstallConfiguration.AgentVersion, activitySourceType, activityKindType)) as IDisposable;
            originalActivitySource?.Dispose();
        }

        public INewRelicActivity TryCreateActivity(string activityName, ActivityKind kind)
        {
            if (_activitySource == null)
            {
                return null;
            }
            return _activitySource.CreateActivity(activityName, kind);
        }
    }

    public interface INewRelicActivitySource : IDisposable
    {
        INewRelicActivity CreateActivity(string activityName, ActivityKind kind);
    }

    public class RuntimeActivitySource : INewRelicActivitySource
    {
        private readonly dynamic _activitySource;
        private readonly Func<string, int, object> _createActivityMethod;

        public RuntimeActivitySource(string name, string version, Type activitySourceType, Type activityKindType)
        {
            _activitySource = Activator.CreateInstance(activitySourceType, name, version);
            _createActivityMethod = CreateCreateActivityMethod(activitySourceType, activityKindType);
        }

        public void Dispose()
        {
            _activitySource?.Dispose();
        }

        public INewRelicActivity CreateActivity(string activityName, ActivityKind kind)
        {
            var activity = _createActivityMethod(activityName, (int)kind);
            return new RuntimeNewRelicActivity(activity);
        }

        private Func<string, int, object> CreateCreateActivityMethod(Type activitySourceType, Type activityKindType)
        {
            var activityNameParameter = Expression.Parameter(typeof(string), "activityName");
            var activityKindParameter = Expression.Parameter(typeof(int), "kind");

            var typedActivityKind = Expression.Convert(activityKindParameter, activityKindType);
            var activitySourceInstance = Expression.Constant(_activitySource, activitySourceType);

            var startActivityMethod = activitySourceType.GetMethod("CreateActivity", [typeof(string), activityKindType]);
            var startActivityCall = Expression.Call(activitySourceInstance, startActivityMethod, activityNameParameter, typedActivityKind);
            var startActivityLambda = Expression.Lambda<Func<string, int, object>>(startActivityCall, activityNameParameter, activityKindParameter);
            return startActivityLambda.Compile();
        }
    }

    // TODO: Not all of these properties on activities are available in all versions of the DiagnosticSource assembly.
    // We should either have code that gracefully handles the property or method not being available, or we need to
    // ensure that we only enable the bridging code when an appropriate minimum version of the DiagnosticSource
    // assembly is loaded.
    public class RuntimeNewRelicActivity : INewRelicActivity
    {
        private readonly object _activity;

        public RuntimeNewRelicActivity(object activity)
        {
            _activity = activity;
        }

        public bool IsStopped => (bool?)((dynamic)_activity)?.IsStopped ?? true;

        public string SpanId => (string)((dynamic)_activity)?.SpanId.ToString();

        public string TraceId => (string)((dynamic)_activity)?.TraceId.ToString();

        public string DisplayName => (string)((dynamic)_activity)?.DisplayName;

        public ISegment Segment
        {
            get => GetSegmentFromActivity(_activity);
            set => ((dynamic)_activity)?.SetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName, value);
        }

        public void Dispose()
        {
            dynamic dynamicActivity = _activity;
            dynamicActivity?.Dispose();
        }

        public void Start()
        {
            dynamic dynamicActivity = _activity;
            dynamicActivity?.Start();
        }

        public void Stop()
        {
            dynamic dynamicActivity = _activity;
            dynamicActivity?.Stop();
        }

        public static ISegment GetSegmentFromActivity(object activity)
        {
            return ((dynamic)activity)?.GetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName) as ISegment;
        }
    }
}
