// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using System.Text;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Formats a method's parameters the way the NR profiler does (SignatureParser <c>MethodSignature::ToString</c>), so
/// the result compares with instrumentation XML <c>parameters</c> strings and matches the argument signature agent mode
/// passes to Core.
/// </summary>
public static class DistroSignatureFormatter
{
    public static string Format(ParameterInfo[] parameters)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendType(builder, parameters[i].ParameterType);
        }

        return builder.ToString();
    }

    private static void AppendType(StringBuilder builder, Type type)
    {
        if (type.IsByRef)
        {
            AppendType(builder, type.GetElementType());
            builder.Append('&');
            return;
        }

        if (type.IsPointer)
        {
            AppendType(builder, type.GetElementType());
            builder.Append('*');
            return;
        }

        if (type.IsArray)
        {
            // The profiler prints a rank-N array as "T[,...]" (no bounds for C# arrays) and an SZ array as "T[]".
            AppendType(builder, type.GetElementType());
            builder.Append('[').Append(',', type.GetArrayRank() - 1).Append(']');
            return;
        }

        if (type.IsGenericParameter)
        {
            builder.Append(type.DeclaringMethod != null ? "!!" : "!").Append(type.GenericParameterPosition);
            return;
        }

        if (type == typeof(void))
        {
            // Only reachable as a pointer element type; the profiler prints "void*".
            builder.Append("void");
            return;
        }

        // The profiler uses the TypeDef/TypeRef metadata name: "Namespace.Name" for a top-level type and only the
        // type's own name for a nested type (its metadata namespace is empty; the enclosing type is not printed).
        if (!type.IsNested && !string.IsNullOrEmpty(type.Namespace))
        {
            builder.Append(type.Namespace).Append('.');
        }

        builder.Append(type.Name);

        if (type.IsGenericType)
        {
            var arguments = type.GetGenericArguments();
            builder.Append('[');
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                AppendType(builder, arguments[i]);
            }

            builder.Append(']');
        }
    }
}
