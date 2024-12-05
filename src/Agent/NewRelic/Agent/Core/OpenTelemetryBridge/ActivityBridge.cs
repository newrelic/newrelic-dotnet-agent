// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
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
        // activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
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
            var samplingValue = Expression.Convert(Expression.Constant((int)ActivitySamplingResult.AllDataAndRecorded), activitySamplingResultType);
            var sampleLambda = Expression.Lambda(sampleProperty.PropertyType, samplingValue, optionsParameter);

            sampleProperty.SetValue(activityListener, sampleLambda.Compile());
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
                var segment = transaction.StartCustomSegment(methodCall, activity.DisplayName);

                activity.SetCustomProperty("NewRelicSegment", segment);
            }
        }

        private static void ActivityStopped(object originalActivity, IAgent agent)
        {
            // This method will be called when an activity is stopped. This is where we would end a segment or transaction.
            dynamic activity = originalActivity;
            var segment = activity.GetCustomProperty("NewRelicSegment") as ISegment;

            if (segment != null)
            {
                segment.End();
            }
        }
    }
}
