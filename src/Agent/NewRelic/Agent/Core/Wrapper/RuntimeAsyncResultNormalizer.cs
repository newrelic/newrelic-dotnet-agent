// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Logging;

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
/// once, at the single choke point in WrapperService -- is what lets existing GetAsyncDelegateFor
/// call sites keep working unchanged.
/// </summary>
public static class RuntimeAsyncResultNormalizer
{
    private static readonly MethodInfo CompletedTaskFactory =
        typeof(RuntimeAsyncResultNormalizer).GetMethod(nameof(MakeCompletedTask), BindingFlags.NonPublic | BindingFlags.Static);

    private const BindingFlags MethodSearchFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>
    /// Builds a result normalizer for one instrumented method, or returns null when the method's
    /// declared return type is not a task type at all, no method of that name exists, or reflecting
    /// over the declaring type throws. A null return means the caller must keep synchronous
    /// completion semantics -- never guess, because guessing wrong strands transactions. Intended to
    /// be called once per functionId and the result cached; it reflects.
    ///
    /// Overloads are resolved by rendering each candidate's parameter types the way the profiler
    /// does and comparing against parameterTypeNames. When that picks out exactly one candidate it
    /// is the answer. When it cannot -- an unrenderable parameter type, or two candidates rendering
    /// alike -- the fallback is that only the RESULT SHAPE matters, so candidates that all normalize
    /// identically need not be told apart. Only genuinely differing shapes return null.
    /// </summary>
    public static RuntimeAsyncNormalization TryCreate(Type declaringType, string methodName, string parameterTypeNames)
    {
        // Every reflection call below reaches the loader, so a customer type with a member whose
        // signature references an assembly that cannot be loaded throws FileNotFoundException or
        // TypeLoadException -- the classic GetMethods() failure with optional dependencies. No frame
        // above this one handles that: AgentShim.GetTracer would swallow it and return a null
        // tracer, leaving the functionId uncached, so every later call to the method would repeat
        // the whole enumeration AND the throw. Degrading to null keeps that cost one-time and lands
        // on the outcome the caller already handles.
        try
        {
            return Classify(declaringType, methodName, parameterTypeNames);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to classify the return shape of runtime-async method {0}.{1}({2}); instrumenting it with synchronous completion semantics.",
                declaringType?.FullName, methodName, parameterTypeNames);

            return null;
        }
    }

    private static RuntimeAsyncNormalization Classify(Type declaringType, string methodName, string parameterTypeNames)
    {
        if (declaringType == null || string.IsNullOrEmpty(methodName))
        {
            return null;
        }

        var candidates = FindCandidates(declaringType, methodName);
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return Build(DescribeResultShape(candidates[0]));
        }

        var signature = parameterTypeNames ?? string.Empty;

        MethodInfo match = null;
        var matchCount = 0;
        foreach (var candidate in candidates)
        {
            if (!TryBuildParameterTypeNames(candidate, out var rendered) || rendered != signature)
            {
                continue;
            }

            match = candidate;
            matchCount++;
        }

        if (matchCount == 1)
        {
            return Build(DescribeResultShape(match));
        }

        // Either no candidate's parameter types could be rendered the way the profiler renders
        // them, or two of them render identically. We do not actually need to know WHICH overload
        // this is -- only its result shape -- so if every candidate would normalize the same way,
        // the ambiguity is harmless and refusing would needlessly drop the method to synchronous
        // completion semantics. Disagreement is the only case where we genuinely cannot tell, and
        // that still returns null rather than guessing.
        var agreed = DescribeResultShape(candidates[0]);
        for (var i = 1; i < candidates.Count; i++)
        {
            if (!ShapesAgree(agreed, DescribeResultShape(candidates[i])))
            {
                return null;
            }
        }

        return Build(agreed);
    }

    private static List<MethodInfo> FindCandidates(Type declaringType, string methodName)
    {
        var candidates = new List<MethodInfo>();
        foreach (var candidate in declaringType.GetMethods(MethodSearchFlags))
        {
            if (candidate.Name == methodName)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    /// <summary>
    /// The part of a method's return type that determines its normalizer, so that two candidates
    /// can be compared without building a delegate for either. A null return means the return type
    /// is not a task type at all.
    /// </summary>
    private static ResultShape DescribeResultShape(MethodInfo method)
    {
        var returnType = method.ReturnType;

        if (returnType == typeof(Task) || returnType == typeof(ValueTask))
        {
            return ResultShape.NoResult;
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

        // An open generic (Task<T> on a generic method / Task<IAsyncCursor<TDoc>> on a generic type)
        // cannot be bound to an invokable delegate: CreateDelegate throws for any method whose
        // ContainsGenericParameters is true. Fall back to Task<object>, which still satisfies the
        // `result is Task` gate that most wrappers for async methods use, and which those wrappers'
        // reflective result readers handle unchanged.
        var isExactlyTyped = !resultType.ContainsGenericParameters;
        if (!isExactlyTyped)
        {
            resultType = typeof(object);
        }

        return new ResultShape(resultType, isExactlyTyped);
    }

    private static bool ShapesAgree(ResultShape left, ResultShape right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        return left.HasResult == right.HasResult
            && left.ResultType == right.ResultType
            && left.IsExactlyTyped == right.IsExactlyTyped;
    }

    private static RuntimeAsyncNormalization Build(ResultShape shape)
    {
        if (shape == null)
        {
            return null;
        }

        if (!shape.HasResult)
        {
            return new RuntimeAsyncNormalization(_ => Task.CompletedTask, true);
        }

        var normalize = (Func<object, Task>)CompletedTaskFactory
            .MakeGenericMethod(shape.ResultType)
            .CreateDelegate(typeof(Func<object, Task>));

        return new RuntimeAsyncNormalization(normalize, shape.IsExactlyTyped);
    }

    // Bound generically so the synthesized task is exactly Task<T> for the DECLARED T. Wrappers
    // cast the task with `as` (Delegates.OnSuccess), and Task<FooImpl> as Task<IFoo> is null, so a
    // Task<object> shortcut would silently feed those wrappers a null result.
    private static Task MakeCompletedTask<T>(object result)
    {
        return Task.FromResult(result is T typedResult ? typedResult : default);
    }

    /// <summary>
    /// Renders one method's parameter types the way MethodSignature::ToString in
    /// SignatureParser/Types.h does -- comma separated, no spaces -- because that is the string the
    /// profiler emits into the instrumented IL and hands back as parameterTypeNames.
    ///
    /// Returns false when a parameter type cannot be rendered faithfully. Bailing matters: a
    /// plausible-but-wrong rendering could coincidentally equal a DIFFERENT candidate's correct one
    /// and silently select the wrong overload, whereas a false here falls through to comparing
    /// result shapes instead.
    /// </summary>
    private static bool TryBuildParameterTypeNames(MethodInfo method, out string parameterTypeNames)
    {
        parameterTypeNames = null;

        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            parameterTypeNames = string.Empty;
            return true;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            if (!TryAppendTypeName(parameters[i].ParameterType, builder))
            {
                return false;
            }
        }

        parameterTypeNames = builder.ToString();
        return true;
    }

    // Each branch mirrors the matching Type subclass in SignatureParser/Types.h.
    private static bool TryAppendTypeName(Type type, StringBuilder builder)
    {
        // ByRefType: element then '&'. Has to come first -- a by-ref array is by-ref on the outside.
        if (type.IsByRef)
        {
            if (!TryAppendTypeName(type.GetElementType(), builder))
            {
                return false;
            }

            builder.Append('&');
            return true;
        }

        // SzArrayType: element then "[]".
        if (type.IsArray)
        {
            if (!TryAppendTypeName(type.GetElementType(), builder))
            {
                return false;
            }

            builder.Append("[]");
            return true;
        }

        // VarType is "!N" for a type's own generic parameter, MvarType "!!N" for a method's.
        if (type.IsGenericParameter)
        {
            builder.Append(type.DeclaringMethod != null ? "!!" : "!");
            builder.Append(type.GenericParameterPosition);
            return true;
        }

        // GenericType: the definition's name, '[', the arguments comma separated, ']'. The arguments
        // are NOT assembly qualified, and that is precisely why Type.FullName cannot be used here --
        // it renders List<int> as List`1[[System.Int32, System.Private.CoreLib, Version=...]].
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var definitionName = type.GetGenericTypeDefinition().FullName;
            if (definitionName == null)
            {
                return false;
            }

            builder.Append(definitionName);
            builder.Append('[');

            var arguments = type.GetGenericArguments();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                if (!TryAppendTypeName(arguments[i], builder))
                {
                    return false;
                }
            }

            builder.Append(']');
            return true;
        }

        // Pointers and function pointers have no rendering verified against the native side, and a
        // bare generic type definition cannot appear as a real parameter type. Refuse rather than
        // invent one.
        if (type.IsPointer || type.IsGenericTypeDefinition || type.FullName == null)
        {
            return false;
        }

        builder.Append(type.FullName);
        return true;
    }

    /// <summary>
    /// Deliberately a plain class rather than a record, for the same net462 / netstandard2.0 reason
    /// given on <see cref="RuntimeAsyncNormalization"/>.
    /// </summary>
    private sealed class ResultShape
    {
        public static readonly ResultShape NoResult = new ResultShape(null, true);

        /// <summary>
        /// False for a bare Task / ValueTask, whose body returns nothing.
        /// </summary>
        public bool HasResult { get; }

        public Type ResultType { get; }

        public bool IsExactlyTyped { get; }

        public ResultShape(Type resultType, bool isExactlyTyped)
        {
            HasResult = resultType != null;
            ResultType = resultType;
            IsExactlyTyped = isExactlyTyped;
        }
    }
}
