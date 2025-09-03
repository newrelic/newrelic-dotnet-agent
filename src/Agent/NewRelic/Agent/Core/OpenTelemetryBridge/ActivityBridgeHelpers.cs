// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    public static class ActivityBridgeHelpers
    {
        private static readonly List<int> _activityKindsThatStartATransaction =
        [
            (int)ActivityKind.Server,
            (int)ActivityKind.Consumer
        ];

        public static bool IsTransactionRequiredForActivity(object originalActivity)
        {
            // TODO: Determine if this is the right thing to do. Our wrapper service separates these concepts.
            return !ShouldStartTransactionForActivity(originalActivity);
        }

        public static bool ShouldStartTransactionForActivity(object originalActivity)
        {
            dynamic activity = originalActivity;

            return (bool)activity.HasRemoteParent || _activityKindsThatStartATransaction.Contains((int)activity.Kind);
        }

        public static ITransaction StartTransactionForActivity(object originalActivity, IAgent agent)
        {
            dynamic activity = originalActivity;

            bool isWeb = (int)activity.Kind == (int)ActivityKind.Server;

            return agent.CreateTransaction(isWeb, "Activity", activity.DisplayName, doNotTrackAsUnitOfWork: true);
        }

        public static IEnumerable<string> GetTraceContextHeadersFromActivity(object originalActivity, string headerName)
        {
            dynamic activity = originalActivity;
            switch (headerName)
            {
                case "traceparent":
                    return [(string)activity.ParentId];
                case "tracestate":
                    return [(string)activity.TraceStateString ?? string.Empty];
                default:
                    return [];
            }
        }

        private static Action<object> _setCurrentActivity;
        private static Func<object> _getCurrentActivity;
        private static readonly object _lock = new();

        /// <summary>
        ///  Sets Activity.Current to the provided activity object.
        /// </summary>
        /// <remarks>This method uses reflection to dynamically set the static <c>Current</c> property of
        /// the <c>System.Diagnostics.Activity</c> class. If the <c>System.Diagnostics.DiagnosticSource</c> assembly is
        /// not available or the <c>Current</c> property cannot be set, the method will perform no operation.</remarks>
        /// <param name="activity">The activity object to set as the current activity. This should be an instance of the
        /// <c>System.Diagnostics.Activity</c> class or a compatible object. Passing <see langword="null"/> will clear
        /// the current activity.</param>
        public static void SetCurrentActivity(object activity)
        {
            if (_setCurrentActivity == null)
            {
                lock (_lock)
                {
                    if (_setCurrentActivity == null)
                    {
                        // use reflection to get the static Current property from System.Diagnostics.Activity
                        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
                        if (assembly != null)
                        {
                            var activityType = assembly.GetType("System.Diagnostics.Activity");
                            if (activityType != null)
                            {
                                var currentProperty = activityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                                if (currentProperty != null && currentProperty.CanWrite)
                                {
                                    var activityParam = Expression.Parameter(typeof(object), "activity");
                                    var convertedActivity = Expression.Convert(activityParam, activityType);
                                    var assign = Expression.Assign(Expression.Property(null, currentProperty), convertedActivity);
                                    var setterLambda = Expression.Lambda<Action<object>>(assign, activityParam);
                                    _setCurrentActivity = setterLambda.Compile();
                                }
                            }
                        }

                        // If we couldn't create the setter, create a no-op to avoid trying again.
                        _setCurrentActivity ??= (_) => { };
                    }
                }
            }

            _setCurrentActivity(activity);
        }

        /// <summary>
        /// Retrieves the current activity from the diagnostic source, if available.
        /// </summary>
        /// <remarks>This method uses reflection to access the static <c>Current</c> property of the
        /// <c>System.Diagnostics.Activity</c> class from the <c>System.Diagnostics.DiagnosticSource</c> assembly. If
        /// the property is not available or cannot be accessed, the method returns <see langword="null"/>.</remarks>
        /// <returns>The current activity as an <see cref="object"/>, or <see langword="null"/> if no activity is available or
        /// the <c>System.Diagnostics.Activity</c> class is not accessible.</returns>
        public static object GetCurrentActivity()
        {
            if (_getCurrentActivity == null)
            {
                lock (_lock)
                {
                    if (_getCurrentActivity == null)
                    {
                        // use reflection to get the static Current property from System.Diagnostics.Activity
                        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
                        if (assembly != null)
                        {
                            var activityType = assembly.GetType("System.Diagnostics.Activity");
                            if (activityType != null)
                            {
                                var currentProperty = activityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                                if (currentProperty != null && currentProperty.CanRead)
                                {
                                    var propertyAccess = Expression.Property(null, currentProperty);
                                    var convert = Expression.Convert(propertyAccess, typeof(object));
                                    var getterLambda = Expression.Lambda<Func<object>>(convert);
                                    _getCurrentActivity = getterLambda.Compile();
                                }
                            }
                        }

                        // If we couldn't create the getter, create one that returns null to avoid trying again.
                        _getCurrentActivity ??= () => null;
                    }
                }
            }

            return _getCurrentActivity();
        }

        /// <summary>
        /// FOR TESTING ONLY: Resets the static fields used for getting and setting the current activity.
        /// </summary>
        public static void Reset()
        {
            // Reset the static fields to null to allow for reinitialization.
            _setCurrentActivity = null;
            _getCurrentActivity = null;
        }
    }
}
