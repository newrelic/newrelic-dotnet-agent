// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    public class ActivityBridge : IDisposable
    {
        private dynamic _activityListener;
        private object 

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

            // Need to subscribe to the Sample and SampleUsingParentId callbacks to get sampling decisions aligned with the agent.
            // Activity will not actually be created by an activity source unless an activity listener is listening to it and the
            // activity is sampled.

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
            // Listen to all activity sources for now
            return true;
        }
    }
}
