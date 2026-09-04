// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.Wrapper;

/// <summary>
/// The result of classifying one runtime-async method's declared return shape.
/// </summary>
/// <remarks>
/// Deliberately a plain immutable class rather than a record: Core targets net462 and
/// netstandard2.0, where a positional record's init accessors would require
/// System.Runtime.CompilerServices.IsExternalInit, which neither framework provides and for
/// which this repo carries no shim.
/// </remarks>
public sealed class RuntimeAsyncNormalization
{
    /// <summary>
    /// Maps the profiler's raw result to an already-completed task.
    /// </summary>
    public Func<object, Task> Normalize { get; }

    /// <summary>
    /// False when the declared result type was an open generic and Task&lt;object&gt; was
    /// substituted, because exact typing is genuinely unavailable in that case.
    /// </summary>
    public bool IsExactlyTyped { get; }

    public RuntimeAsyncNormalization(Func<object, Task> normalize, bool isExactlyTyped)
    {
        Normalize = normalize;
        IsExactlyTyped = isExactlyTyped;
    }
}

/// <summary>
/// Converts the value a .NET 11 runtime-async method's IL body actually returns into the
/// already-completed Task the agent's after-delegate machinery expects.
///
/// Per the ECMA-335 augment (I.8.4.5) a runtime-async body pushes nothing before `ret` for
/// Task/ValueTask and an unwrapped T for Task&lt;T&gt;/ValueTask&lt;T&gt;, so the profiler hands
/// FinishTracer a null or a boxed T where every wrapper expects a Task. Restoring the Task here --
/// once, at the single choke point in WrapperService -- is what lets all 47 GetAsyncDelegateFor
/// call sites keep working unchanged. See NR-610232.
/// </summary>
public static class RuntimeAsyncResultNormalizer
{
    private static readonly MethodInfo CompletedTaskFactory =
        typeof(RuntimeAsyncResultNormalizer).GetMethod(nameof(MakeCompletedTask), BindingFlags.NonPublic | BindingFlags.Static);

    private const BindingFlags MethodSearchFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>
    /// Builds a result normalizer for one instrumented method, or returns null when the method's
    /// declared return type is not a task type at all (or the method cannot be resolved). A null
    /// return means the caller must keep synchronous completion semantics -- never guess, because
    /// guessing wrong strands transactions. Intended to be called once per functionId and the
    /// result cached; it reflects.
    /// </summary>
    public static RuntimeAsyncNormalization TryCreate(Type declaringType, string methodName, string parameterTypeNames)
    {
        var method = TryResolveMethod(declaringType, methodName, parameterTypeNames);
        if (method == null)
        {
            return null;
        }

        var returnType = method.ReturnType;

        if (returnType == typeof(Task) || returnType == typeof(ValueTask))
        {
            return new RuntimeAsyncNormalization(_ => Task.CompletedTask, true);
        }

        if (!returnType.IsGenericType)
        {
            return null;
        }

        var genericDefinition = returnType.GetGenericTypeDefinition();
        if (genericDefinition != typeof(Task<>) && genericDefinition != typeof(ValueTask<>))
        {
            return null;
        }

        var resultType = returnType.GetGenericArguments()[0];

        // An open generic (Task<T> on a generic method, Task<IAsyncCursor<TDoc>> on a generic type,
        // ...) cannot be bound to an invokable delegate: CreateDelegate throws for any method whose
        // ContainsGenericParameters is true. Fall back to Task<object>, which still satisfies the
        // `result is Task` gate that every wrapper but HttpClient/SendAsync uses, and which the
        // reflective result readers handle unchanged. NOT null -- declining here would leave IsAsync
        // false and preserve the original defect for these methods.
        var isExactlyTyped = !resultType.ContainsGenericParameters;
        if (!isExactlyTyped)
        {
            resultType = typeof(object);
        }

        var normalize = (Func<object, Task>)CompletedTaskFactory
            .MakeGenericMethod(resultType)
            .CreateDelegate(typeof(Func<object, Task>));

        return new RuntimeAsyncNormalization(normalize, isExactlyTyped);
    }

    // Bound generically so the synthesized task is exactly Task<T> for the DECLARED T. Wrappers
    // cast the task with `as` (Delegates.OnSuccess), and Task<FooImpl> as Task<IFoo> is null, so a
    // Task<object> shortcut would silently feed those wrappers a null result.
    private static Task MakeCompletedTask<T>(object result)
    {
        return Task.FromResult(result is T typedResult ? typedResult : default);
    }

    private static MethodInfo TryResolveMethod(Type declaringType, string methodName, string parameterTypeNames)
    {
        if (declaringType == null || string.IsNullOrEmpty(methodName))
        {
            return null;
        }

        var candidates = new List<MethodInfo>();
        foreach (var candidate in declaringType.GetMethods(MethodSearchFlags))
        {
            if (candidate.Name == methodName)
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        MethodInfo match = null;
        var signature = parameterTypeNames ?? string.Empty;
        foreach (var candidate in candidates)
        {
            if (BuildParameterTypeNames(candidate) != signature)
            {
                continue;
            }

            // Two candidates cannot be told apart by the profiler's signature string; refuse both.
            if (match != null)
            {
                return null;
            }

            match = candidate;
        }

        return match;
    }

    // Mirrors MethodSignature::ToString in SignatureParser/Types.h: full type names, comma
    // separated, no spaces. A generic parameter has a null FullName and so will never match --
    // which is the correct outcome, since those take the Task<object> tier anyway.
    private static string BuildParameterTypeNames(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return string.Empty;
        }

        var names = new string[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            names[i] = parameters[i].ParameterType.FullName;
        }

        return string.Join(",", names);
    }
}
