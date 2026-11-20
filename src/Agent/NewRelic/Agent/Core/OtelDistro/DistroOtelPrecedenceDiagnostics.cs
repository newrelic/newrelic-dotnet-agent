// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Reports the methods where an enabled OTel CallTarget site takes a method an NR site also targets. OTel registers its
/// own definitions before the plugin's and the OTel native profiler applies the first matching definition, so on such a
/// method OTel's instrumentation runs and NR's does not. One Warning and one
/// <c>Supportability/Dotnet/OtelDistro/Precedence/OtelWins</c> count (through <see cref="DistroConfigMetrics"/>, queued until
/// Core's sink is set) per method per AppDomain. Never throws.
/// </summary>
public static class DistroOtelPrecedenceDiagnostics
{
    public const string OtelWinsMetricName = "Supportability/Dotnet/OtelDistro/Precedence/OtelWins";

    private const BindingFlags DeclaredMethods = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly object Gate = new object();
    private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Reports every exact overlap: an OTel site of the Direct kind and an NR site on the same assembly (simple name,
    /// case-insensitive), type, method and parameter types. An NR site with a null signature matches every overload.
    /// Derived/Interface OTel sites are left to <see cref="ReportDerived"/>.
    /// </summary>
    public static void Report(IEnumerable<OtelSite> otelSites, IEnumerable<NrSite> nrSites)
    {
        try
        {
            if (otelSites == null || nrSites == null)
            {
                return;
            }

            var nrList = new List<NrSite>(nrSites);
            foreach (var otel in otelSites)
            {
                if (otel == null || otel.MatchesDerivedOrInterface)
                {
                    continue;
                }

                foreach (var nr in nrList)
                {
                    if (nr != null
                        && AssemblyNamesEqual(otel.Assembly, nr.Assembly)
                        && string.Equals(otel.Type, nr.Type, StringComparison.Ordinal)
                        && string.Equals(otel.Method, nr.Method, StringComparison.Ordinal)
                        && SignaturesMatch(otel.Signature, nr.Signature))
                    {
                        ReportOnce(otel, nr, nr.Type);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Diagnostics must never reach OTel start-up.
        }
    }

    /// <summary>
    /// Reports the Derived/Interface overlaps for <paramref name="nrTargetType"/>, a loaded type that NR sites target, by
    /// the OTel native profiler's own rules (src/OpenTelemetry.AutoInstrumentation.Native/rejit_preprocessor.cpp, v1.18.0):
    /// the OTel site's type (full name, and assembly simple name case-insensitive) is the IMMEDIATE base type of
    /// <paramref name="nrTargetType"/> (L541-596, <c>maxDepth = 1</c>) or an interface it declares itself (L469-484, its
    /// own InterfaceImpl rows, <c>classToken == typeDef</c>); <paramref name="nrTargetType"/> declares a method with the
    /// OTel site's name and parameter types (L239 <c>EnumMethodsWithName</c> on the type, L745-770 signature compare); and
    /// an NR site on <paramref name="nrTargetType"/> has that method and parameter types.
    /// </summary>
    public static void ReportDerived(Type nrTargetType, IEnumerable<OtelSite> otelSites, IEnumerable<NrSite> nrSites)
    {
        try
        {
            if (nrTargetType == null || otelSites == null || nrSites == null)
            {
                return;
            }

            var typeName = nrTargetType.FullName;
            var assemblyName = nrTargetType.Assembly.GetName().Name;
            var onType = new List<NrSite>();
            foreach (var nr in nrSites)
            {
                if (nr != null && string.Equals(nr.Type, typeName, StringComparison.Ordinal) && AssemblyNamesEqual(nr.Assembly, assemblyName))
                {
                    onType.Add(nr);
                }
            }

            if (onType.Count == 0)
            {
                return;
            }

            foreach (var otel in otelSites)
            {
                if (otel == null || !otel.MatchesDerivedOrInterface || !DerivesFromOrImplements(nrTargetType, otel) || !DeclaresMethod(nrTargetType, otel))
                {
                    continue;
                }

                foreach (var nr in onType)
                {
                    if (string.Equals(otel.Method, nr.Method, StringComparison.Ordinal) && SignaturesMatch(otel.Signature, nr.Signature))
                    {
                        ReportOnce(otel, nr, typeName);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Diagnostics must never reach OTel start-up or an assembly load.
        }
    }

    /// <summary>
    /// Splits an NR XML <c>parameters</c> string into parameter type names, as the profiler reads it: whitespace removed,
    /// <c>void</c> means parameterless (empty), commas inside <c>[]</c> or <c>&lt;&gt;</c> belong to a generic argument
    /// list. Null or blank means the site matches every overload (null).
    /// </summary>
    public static IReadOnlyList<string> ParseSignature(string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return null;
        }

        var normalized = RemoveWhitespace(signature);
        if (string.Equals(normalized, "void", StringComparison.OrdinalIgnoreCase))
        {
            return new string[0];
        }

        var parameters = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < normalized.Length; i++)
        {
            switch (normalized[i])
            {
                case '[':
                case '<':
                    depth++;
                    break;
                case ']':
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parameters.Add(normalized.Substring(start, i - start));
                    start = i + 1;
                    break;
            }
        }

        parameters.Add(normalized.Substring(start));
        return parameters;
    }

    /// <summary>Test seam: forgets which methods were reported.</summary>
    public static void ResetForTesting()
    {
        lock (Gate)
        {
            Reported.Clear();
        }
    }

    private static void ReportOnce(OtelSite otel, NrSite nr, string typeName)
    {
        var signature = string.Join(",", otel.Signature ?? new string[0]);
        lock (Gate)
        {
            if (!Reported.Add(typeName + "." + otel.Method + "(" + signature + ")"))
            {
                return;
            }
        }

        // Metric first, and the log call guarded, so a throwing logger can neither drop the count nor stop later reports.
        DistroConfigMetrics.Record(OtelWinsMetricName);
        try
        {
            Log.Warn("OTel instrumentation {0} ({1}) takes precedence over NR {2} on {3}.{4}({5}); NR instrumentation will not run there.", otel.Id, otel.Integration, nr.TracerFactory, typeName, otel.Method, signature);
        }
        catch (Exception)
        {
        }
    }

    private static bool DerivesFromOrImplements(Type type, OtelSite otel)
    {
        // Native compares the immediate base type only (rejit_preprocessor.cpp L541 maxDepth = 1).
        var baseType = type.BaseType;
        if (baseType != null && IsOtelTarget(baseType, otel))
        {
            return true;
        }

        // Native compares only the interfaces on the type's own InterfaceImpl rows (L469-484). Reflection has no direct
        // view of those; "implemented, and not already implemented by the base type" approximates them. An interface the
        // type re-declares although its base already implements it is missed (a false negative, never a false warning).
        var inherited = baseType == null ? new HashSet<Type>() : new HashSet<Type>(baseType.GetInterfaces());
        foreach (var implemented in type.GetInterfaces())
        {
            if (!inherited.Contains(implemented) && IsOtelTarget(implemented, otel))
            {
                return true;
            }
        }

        return false;
    }

    // Native rewrites only methods the matched type itself defines (L239 EnumMethodsWithName on the typeDef) whose
    // parameter types equal the site's (L745-770), so the override must exist on this type in the loaded version.
    private static bool DeclaresMethod(Type type, OtelSite otel)
    {
        var expected = otel.Signature ?? new string[0];
        foreach (var method in type.GetMethods(DeclaredMethods))
        {
            if (!string.Equals(method.Name, otel.Method, StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = method.GetParameters();
            var actual = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                actual[i] = FormatOtelTypeName(parameters[i].ParameterType);
            }

            if (SignaturesMatch(expected, actual))
            {
                return true;
            }
        }

        return false;
    }

    // A parameter type as OTel's InstrumentMethodAttribute spells it: CLR full names with generic arguments in brackets
    // ("System.ReadOnlyMemory`1[System.Byte]"), "!N" / "!!N" for class / method generic parameters, "&" for by-ref.
    private static string FormatOtelTypeName(Type type)
    {
        if (type.IsByRef)
        {
            return FormatOtelTypeName(type.GetElementType()) + "&";
        }

        if (type.IsArray)
        {
            return FormatOtelTypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod != null ? "!!" : "!") + type.GenericParameterPosition;
        }

        if (type.IsGenericType)
        {
            var arguments = type.GetGenericArguments();
            var names = new string[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                names[i] = FormatOtelTypeName(arguments[i]);
            }

            return type.GetGenericTypeDefinition().FullName + "[" + string.Join(",", names) + "]";
        }

        return type.FullName;
    }

    private static bool IsOtelTarget(Type type, OtelSite otel)
    {
        return string.Equals(type.FullName, otel.Type, StringComparison.Ordinal) && AssemblyNamesEqual(type.Assembly.GetName().Name, otel.Assembly);
    }

    private static bool AssemblyNamesEqual(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SignaturesMatch(IReadOnlyList<string> otelSignature, IReadOnlyList<string> nrSignature)
    {
        if (nrSignature == null)
        {
            return true;
        }

        var otel = otelSignature ?? new string[0];
        if (otel.Count != nrSignature.Count)
        {
            return false;
        }

        for (var i = 0; i < otel.Count; i++)
        {
            if (!string.Equals(RemoveWhitespace(otel[i]), RemoveWhitespace(nrSignature[i]), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string RemoveWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
