// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;

namespace NewRelic.WeakActions
{
    public class WeakActionUtilities
    {
        public static IWeakAction<T> MakeWeak<T>(Action<T> action, Action<Action<T>> actionGarbageCollectedCallback)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            var weakActionType = CreateWeakActionType(action);
            var weakActionConstructor = GetWeakActionConstructor<T>(weakActionType);
            var weakAction = ConstructWeakAction(weakActionConstructor, action, actionGarbageCollectedCallback);

            return weakAction;
        }
        private static Type CreateWeakActionType<T>(Action<T> action)
        {
            if (action.Method == null)
                throw new ArgumentException("Action must have a MethodInfo.", "action");

            var typeOfWeakAction = typeof(WeakAction<,>);
            return typeOfWeakAction.MakeGenericType(action.Method.DeclaringType, typeof(T));
        }
        private static ConstructorInfo GetWeakActionConstructor<T>(Type weakActionGenericInstantiationType)
        {
            var constructor = weakActionGenericInstantiationType.GetConstructor(new[] { typeof(Action<T>), typeof(Action<Action<T>>) });
            if (constructor == null)
                throw new NullReferenceException("Unable to locate an appropriate constructor for WeakAction.");

            return constructor;
        }
        private static IWeakAction<T> ConstructWeakAction<T>(ConstructorInfo weakActionConstructor, Action<T> action, Action<Action<T>> actionGarbageCollectedCallback)
        {
            var weakAction = (IWeakAction<T>)weakActionConstructor.Invoke(new object[] { action, actionGarbageCollectedCallback });
            if (weakAction == null)
                throw new NullReferenceException("Unable to invoke weakEventHandlerConstructor.");

            return weakAction;
        }
        public static IWeakAction<T1, T2> MakeWeak<T1, T2>(Action<T1, T2> action, Action<Action<T1, T2>> actionGarbageCollectedCallback)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            var weakActionType = CreateWeakActionType(action);
            var weakActionConstructor = GetWeakActionConstructor<T1, T2>(weakActionType);
            var weakAction = ConstructWeakAction(weakActionConstructor, action, actionGarbageCollectedCallback);

            return weakAction;
        }
        private static Type CreateWeakActionType<T1, T2>(Action<T1, T2> action)
        {
            if (action.Method == null)
                throw new ArgumentException("Action must have a MethodInfo.", "action");

            var typeOfWeakAction = typeof(WeakAction<,,>);
            return typeOfWeakAction.MakeGenericType(action.Method.DeclaringType, typeof(T1), typeof(T2));
        }
        private static ConstructorInfo GetWeakActionConstructor<T1, T2>(Type weakActionGenericInstantiationType)
        {
            var constructor = weakActionGenericInstantiationType.GetConstructor(new[] { typeof(Action<T1, T2>), typeof(Action<Action<T1, T2>>) });
            if (constructor == null)
                throw new NullReferenceException("Unable to locate an appropriate constructor for WeakAction.");

            return constructor;
        }
        private static IWeakAction<T1, T2> ConstructWeakAction<T1, T2>(ConstructorInfo weakActionConstructor, Action<T1, T2> action, Action<Action<T1, T2>> actionGarbageCollectedCallback)
        {
            var weakAction = (IWeakAction<T1, T2>)weakActionConstructor.Invoke(new object[] { action, actionGarbageCollectedCallback });
            if (weakAction == null)
                throw new NullReferenceException("Unable to invoke weakEventHandlerConstructor.");

            return weakAction;
        }
    }
}
