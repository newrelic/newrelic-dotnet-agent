// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace NewRelic.Agent.Core;

/// <summary>
/// The class used by the byte-code injected by the profiler to invoke arbitrary methods defined in the managed agent.
/// </summary>
public class ProfilerAgentMethodInvoker
{
    private static readonly ConcurrentDictionary<string, Func<object[], object>> _invokerCache = new ConcurrentDictionary<string, Func<object[], object>>();

    /// <summary>
    /// This method is invoked once using reflection by the byte-code injected by the profiler, to get a
    /// reference to the method that caches reflection lookups and Expression compilation for Agent methods.
    /// </summary>
    public static object GetInvoker()
    {
        return (Func<string, string, string, Type[], Type, object[], object>)GetAndInvokeMethodFromCache;
    }

    public static object GetAndInvokeMethodFromCache(string key, string className, string methodName, Type[] originalTypes, Type returnType, object[] originalParameters)
    {
        var types = originalTypes ?? Array.Empty<Type>();
        var parameters = originalParameters ?? Array.Empty<object>();

        if (!_invokerCache.TryGetValue(key, out var invoker))
        {
            var type = Type.GetType(className);
            var mi = type.GetMethod(methodName, types);
            var delegateType = returnType == null ? Expression.GetActionType(types) : Expression.GetFuncType(GetTypesForFunc(types, returnType));
            var methodDelegate = mi.CreateDelegate(delegateType);

            /* Create a function similar to the following code
             * object InvokeMethod(object[] parameters) {
             *   return methodDelegate.Invoke((types[0])parameters[0]);
             * }
             */
            var parametersParam = Expression.Parameter(typeof(object[]), "parameters");
            var methodToCall = Expression.Constant(methodDelegate, delegateType);
            IEnumerable<Expression> parameterExpressions = GetInvokerParameterExpressions(parametersParam, types);
            var invocationExpression = Expression.Invoke(methodToCall, parameterExpressions);

            // Expression trees do not insert implicit boxing conversions. When returnType is a value type,
            // invocationExpression.Type is that value type while the lambda's declared return type is object,
            // so Expression.Lambda would throw an ArgumentException without an explicit Convert. Expression.Convert
            // to object is a boxing conversion for value types and a no-op upcast for reference types, so it is
            // safe for both cases.
            var lambdaExpression = returnType != null ?
                Expression.Lambda<Func<object[], object>>(Expression.Convert(invocationExpression, typeof(object)), parametersParam) :
                Expression.Lambda<Func<object[], object>>(Expression.Block(invocationExpression, Expression.Constant(null)), parametersParam);

            invoker = lambdaExpression.Compile();
            _invokerCache.TryAdd(key, invoker);
        }

        return invoker(parameters);
    }

    private static IEnumerable<Expression> GetInvokerParameterExpressions(Expression parametersExpression, Type[] types)
    {
        for (var i = 0; i < types.Length; i++)
        {
            var getParameterAtIndex = Expression.ArrayAccess(parametersExpression, Expression.Constant(i));
            yield return Expression.Convert(getParameterAtIndex, types[i]);
        }
    }

    private static Type[] GetTypesForFunc(Type[] parameterTypes, Type returnType)
    {
        if (parameterTypes.Length == 0)
        {
            return new Type[] { returnType };
        }

        var types = new Type[parameterTypes.Length + 1];
        Array.Copy(parameterTypes, types, parameterTypes.Length);
        types[parameterTypes.Length] = returnType;

        return types;
    }
}
