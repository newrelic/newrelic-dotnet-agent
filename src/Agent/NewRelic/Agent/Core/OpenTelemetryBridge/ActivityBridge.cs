// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    public class ActivityBridge : IDisposable
    {
        private IAgent _agent;

        private dynamic _activityListener;

        public ActivityBridge(IAgent agent)
        {
            _agent = agent;
        }

        public void Start()
        {
            TryCreateActivityListener();
        }

        private void TryCreateActivityListener()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
            var activityListenerType = assembly.GetType("System.Diagnostics.ActivityListener", throwOnError: false);
            var activitySourceType = assembly.GetType("System.Diagnostics.ActivitySource", throwOnError: false);

            if (activityListenerType == null || activitySourceType == null)
            {
                return;
            }

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

        // Generates code similar to the following.
        // activityListener.ShouldListenTo = (activitySource) => ShouldListenToActivitySource(activitySource);
        private static void ConfigureShouldListenToCallback(object activityListener, Type activityListenerType, Type activitySourceType)
        {
            var shouldListenToProperty = activityListenerType.GetProperty("ShouldListenTo");

            var activitySourceParameter = Expression.Parameter(activitySourceType, "activitySource");

            var shouldListenToActivitySourceMethod = typeof(ActivityBridge).GetMethod(nameof(ShouldListenToActivitySource), BindingFlags.NonPublic | BindingFlags.Static);

            var shouldListenToCall = Expression.Call(null, shouldListenToActivitySourceMethod, activitySourceParameter);

            var shouldListenToLambda = Expression.Lambda(shouldListenToProperty.PropertyType, shouldListenToCall, activitySourceParameter);

            shouldListenToProperty.SetValue(activityListener, shouldListenToLambda.Compile());
        }

        private static bool ShouldListenToActivitySource(object activitySource)
        {
            dynamic dynamicActivitySource = activitySource;
            // Listen to all non-legacy activity sources for now
            return !string.IsNullOrEmpty((string)dynamicActivitySource.Name);
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

        private static void ConfigureActivityStartedAndStoppedCallbacks(object activityListener, Type activityListenerType, Assembly assembly, IAgent agent)
        {
            var activityType = assembly.GetType("System.Diagnostics.Activity", throwOnError: false);
            ConfigureActivityStartedCallback(activityListener, activityListenerType, activityType, agent);
            ConfigureActivityStoppedCallback(activityListener, activityListenerType, activityType, agent);
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

        private static void ConfigureActivityStoppedCallback(object activityListener, Type activityListenerType, Type activityType, IAgent agent)
        {
            var activityStoppedProperty = activityListenerType.GetProperty("ActivityStopped");
            var activityStoppedMethod = typeof(ActivityBridge).GetMethod(nameof(ActivityStopped), BindingFlags.NonPublic | BindingFlags.Static);

            var activityParameter = Expression.Parameter(activityType, "activity");
            var agentInstance = Expression.Constant(agent);
            var activityStoppedCall = Expression.Call(null, activityStoppedMethod, activityParameter, agentInstance);
            var activityStoppedLambda = Expression.Lambda(activityStoppedProperty.PropertyType, activityStoppedCall, activityParameter);

            activityStoppedProperty.SetValue(activityListener, activityStoppedLambda.Compile());
        }

        private static void ActivityStarted(object originalActivity, IAgent agent)
        {
            // This method will be called when an activity is started. This is where we would start a segment or transaction.
            var transaction = agent.CurrentTransaction;
            if (transaction.IsValid && !transaction.IsFinished)
            {
                dynamic activity = originalActivity;
                var method = new Method(typeof(ActivityBridge), nameof(ActivityStarted), "object,IAgent");
                var methodCall = new MethodCall(method, null, Array.Empty<object>(), false);
                _ = transaction.StartActivitySegment(methodCall, new RuntimeNewRelicActivity(originalActivity));
            }
        }

        private static void ActivityStopped(object originalActivity, IAgent agent)
        {
            // This method will be called when an activity is stopped. This is where we would end a segment or transaction.
            var segment = RuntimeNewRelicActivity.GetSegmentFromActivity(originalActivity);

            if (segment != null)
            {
                segment.End();
            }
        }
    }

    public class NewRelicActivitySourceProxy
    {
        public const string SegmentCustomPropertyName = "NewRelicSegment";

        private const string ActivitySourceName = "NewRelic.Agent";
        private static INewRelicActivitySource _activitySource = CreateDefaultActivitySource();
        private static int _usingRuntimeActivitySource = 0;

        private static INewRelicActivitySource CreateDefaultActivitySource()
        {
            return new DefaultActivitySource(ActivitySourceName, AgentInstallConfiguration.AgentVersion);
        }

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

        public INewRelicActivity StartActivity(string activityName, ActivityKind kind)
        {
            return _activitySource.StartActivity(activityName, kind);
        }
    }

    public interface INewRelicActivitySource : IDisposable
    {
        INewRelicActivity StartActivity(string activityName, ActivityKind kind);
    }

    public class DefaultActivitySource : INewRelicActivitySource
    {
        private readonly ActivitySource _activitySource;

        public DefaultActivitySource(string name, string version)
        {
            _activitySource = new ActivitySource(name, version);
        }
        public void Dispose()
        {
            _activitySource.Dispose();
        }

        public INewRelicActivity StartActivity(string activityName, ActivityKind kind)
        {
            var activity = _activitySource.StartActivity(activityName, kind);
            return new DefaultNewRelicActivity(activity);
        }
    }

    public class RuntimeActivitySource : INewRelicActivitySource
    {
        private readonly dynamic _activitySource;
        private readonly Func<string, int, object> _startActivityMethod;

        public RuntimeActivitySource(string name, string version, Type activitySourceType, Type activityKindType)
        {
            _activitySource = Activator.CreateInstance(activitySourceType, name, version);
            _startActivityMethod = CreateStartActivityMethod(activitySourceType, activityKindType);
        }

        public void Dispose()
        {
            _activitySource?.Dispose();
        }

        public INewRelicActivity StartActivity(string activityName, ActivityKind kind)
        {
            var activity = _startActivityMethod(activityName, (int)kind);
            return new RuntimeNewRelicActivity(activity);
        }

        private Func<string, int, object> CreateStartActivityMethod(Type activitySourceType, Type activityKindType)
        {
            var activityNameParameter = Expression.Parameter(typeof(string), "activityName");
            var activityKindParameter = Expression.Parameter(typeof(int), "kind");

            var typedActivityKind = Expression.Convert(activityKindParameter, activityKindType);
            var activitySourceInstance = Expression.Constant(_activitySource, activitySourceType);

            var startActivityMethod = activitySourceType.GetMethod("StartActivity", new Type[] { typeof(string), activityKindType });
            var startActivityCall = Expression.Call(activitySourceInstance, startActivityMethod, activityNameParameter, typedActivityKind);
            var startActivityLambda = Expression.Lambda<Func<string, int, object>>(startActivityCall, activityNameParameter, activityKindParameter);
            return startActivityLambda.Compile();
        }
    }

    public class DefaultNewRelicActivity : INewRelicActivity
    {
        private readonly Activity _activity;

        public DefaultNewRelicActivity(Activity activity)
        {
            _activity = activity;
        }

        public bool IsStopped => _activity == default || _activity.IsStopped;

        public string SpanId => _activity == default ? null : _activity.SpanId.ToString();

        public ISegment Segment
        {
            get => _activity.GetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName) as ISegment;
            set => _activity.SetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName, value);
        }

        public void Dispose()
        {
            _activity.Dispose();
        }

        public void Stop()
        {
            _activity.Stop();
        }
    }

    public class RuntimeNewRelicActivity : INewRelicActivity
    {
        private readonly object _activity;

        public RuntimeNewRelicActivity(object activity)
        {
            _activity = activity;
        }

        public bool IsStopped => (bool?)((dynamic)_activity)?.IsStopped ?? true;

        public string SpanId => (string)((dynamic)_activity)?.SpanId.ToString();

        public ISegment Segment
        {
            get => GetSegmentFromActivity(_activity);
            set => ((dynamic)_activity).SetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName, value);
        }

        public void Dispose()
        {
            dynamic dynamicActivity = _activity;
            dynamicActivity?.Dispose();
        }

        public void Stop()
        {
            dynamic dynamicActivity = _activity;
            dynamicActivity.Stop();
        }

        public static ISegment GetSegmentFromActivity(object activity)
        {
            return ((dynamic)activity).GetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName) as ISegment;
        }
    }
}
