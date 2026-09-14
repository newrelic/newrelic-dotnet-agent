// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Wrapper;

/// <summary>
/// Converts the value a runtime-async method's IL body actually returns into the
/// already-completed Task the agent's after-delegate machinery expects.
///
/// Per the ECMA-335 augment (I.8.4.5) a runtime-async body pushes nothing before `ret` for
/// Task/ValueTask and an unwrapped T for Task&lt;T&gt;/ValueTask&lt;T&gt;, so the profiler hands
/// FinishTracer a null or a boxed T where every wrapper expects a Task. Restoring the Task here --
/// once, at the single choke point in WrapperService -- is what lets existing GetAsyncDelegateFor
/// call sites keep working unchanged.
///
/// The profiler supplies the type. It already computes the effective return type to size the
/// instrumented method's result local, and passes that same type as the last tracer argument.
/// Because a generic parameter is rendered as a TypeSpec that the CLR resolves in the method's
/// generic context, the type that arrives is the concrete closed type even for a generic method --
/// which is why no open-generic fallback is needed here.
/// </summary>
public static class RuntimeAsyncResultNormalizer
{
    private static readonly MethodInfo CompletedTaskFactory =
        typeof(RuntimeAsyncResultNormalizer).GetMethod(nameof(MakeCompletedTask), BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly Func<object, Task> NoResultNormalizer = _ => Task.CompletedTask;

    // Keyed by result type, not by functionId: a functionId is per method DEFINITION, so a generic
    // runtime-async method arrives once per instantiation with a different type each time. Bounded
    // by the number of distinct effective return types actually instrumented in the process. Null
    // values are cached too, so a type that cannot be bound is not retried on every call.
    private static readonly ConcurrentDictionary<Type, Func<object, Task>> NormalizersByResultType =
        new ConcurrentDictionary<Type, Func<object, Task>>();

    /// <summary>
    /// Returns a normalizer for a runtime-async method whose body returns
    /// <paramref name="effectiveReturnType"/>, or null when no delegate could be built -- in which
    /// case the caller must keep synchronous completion semantics rather than guess, because
    /// guessing wrong strands transactions.
    ///
    /// A null <paramref name="effectiveReturnType"/> is not a failure: it is how the profiler
    /// reports a body that returns nothing, which is the Task / ValueTask case.
    /// </summary>
    public static Func<object, Task> TryCreate(Type effectiveReturnType)
    {
        if (effectiveReturnType == null)
        {
            return NoResultNormalizer;
        }

        // The factory can run concurrently for the same key; building the same delegate twice is
        // harmless, so this needs no lock.
        return NormalizersByResultType.GetOrAdd(effectiveReturnType, BuildNormalizer);
    }

    private static Func<object, Task> BuildNormalizer(Type resultType)
    {
        // An open generic cannot produce a meaningful normalizer: there is no concrete T to type the
        // task to. Guarded explicitly rather than left to MakeGenericMethod/CreateDelegate, because
        // they disagree across frameworks -- .NET Framework binds it happily and hands back a
        // delegate over a still-open method, where .NET 10 throws. Refusing here makes the outcome
        // the same everywhere.
        //
        // Not expected to be reachable: the profiler renders a generic parameter as a TypeSpec that
        // the CLR resolves in the method's generic context, so a closed type is what arrives.
        if (resultType.ContainsGenericParameters)
        {
            Log.Debug("Runtime-async result type {0} is an open generic; instrumenting with synchronous completion semantics.",
                resultType.FullName);

            return null;
        }

        try
        {
            return (Func<object, Task>)CompletedTaskFactory
                .MakeGenericMethod(resultType)
                .CreateDelegate(typeof(Func<object, Task>));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not build a result normalizer for runtime-async result type {0}; instrumenting with synchronous completion semantics.",
                resultType.FullName);

            return null;
        }
    }

    // Bound generically so the synthesized task is exactly Task<T> for the type the profiler
    // reported. Wrappers cast the task with `as` (Delegates.OnSuccess), and Task<FooImpl> as
    // Task<IFoo> is null, so a Task<object> shortcut would silently feed those wrappers a null
    // result.
    private static Task MakeCompletedTask<T>(object result)
    {
        return Task.FromResult(result is T typedResult ? typedResult : default);
    }
}
