// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Identifies which method fired for an OTel distro generated integration, given the site and the CallTarget type
/// arguments OTel instantiated <c>OnMethodBegin</c> with, and whether that method is async (as the NR profiler decides
/// it: by an attribute named <c>System.Runtime.CompilerServices.AsyncStateMachineAttribute</c>). Called once per
/// instantiation (the plugin caches the result), so reflection here is off the hot path. Never throws.
/// </summary>
public static class DistroMethodResolver
{
    private const string AsyncStateMachineAttributeName = "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    private const string IsByRefLikeAttributeName = "System.Runtime.CompilerServices.IsByRefLikeAttribute";

    // OTel passes ref-struct parameters as this type. Core does not reference OTel, so it is compared by full name.
    private const string CallTargetRefStructName = "OpenTelemetry.AutoInstrumentation.CallTarget.CallTargetRefStruct";

    private const BindingFlags DeclaredMethods = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private const ulong FnvOffsetBasis = 14695981039346656037ul;
    private const ulong FnvPrime = 1099511628211ul;

    public static DistroResolvedMethod Resolve(DistroInstrumentationSite site, Type targetType, Type[] argumentTypes)
    {
        return Resolve(site, targetType, argumentTypes, AppDomain.CurrentDomain.GetAssemblies());
    }

    /// <summary>Test seam: <paramref name="loadedAssemblies"/> replaces the AppDomain's assemblies, in order.</summary>
    public static DistroResolvedMethod Resolve(DistroInstrumentationSite site, Type targetType, Type[] argumentTypes, IEnumerable<Assembly> loadedAssemblies)
    {
        var result = ResolveCore(site, targetType, argumentTypes, loadedAssemblies);
        // Concurrent first calls of one overload each resolve and each report, so the fallback supportability count can be
        // incremented more than once for it (the log line is still written once). Accepted: the metric is a diagnostic
        // count of reports, not of distinct overloads.
        DistroMethodResolutionDiagnostics.Report(result, site, argumentTypes?.Length ?? 0);
        return result;
    }

    private static DistroResolvedMethod ResolveCore(DistroInstrumentationSite site, Type targetType, Type[] argumentTypes, IEnumerable<Assembly> loadedAssemblies)
    {
        try
        {
            var declaringType = FindDeclaringType(site, targetType, argumentTypes, loadedAssemblies);
            if (declaringType == null)
            {
                return Unresolved(DistroResolutionOutcome.NotFound, site, targetType);
            }

            var candidates = FindCandidates(declaringType, site, argumentTypes);
            if (candidates.Count == 0)
            {
                return Unresolved(DistroResolutionOutcome.NotFound, site, targetType);
            }

            var first = candidates[0];
            var isAsync = IsAsync(first);
            var outcome = DistroResolutionOutcome.Resolved;
            for (var i = 1; i < candidates.Count; i++)
            {
                outcome = DistroResolutionOutcome.AmbiguousAgreeing;
                if (IsAsync(candidates[i]) != isAsync)
                {
                    // Spike-owner decision (2026-11-11), accepted residue: overloads OTel passes with identical type
                    // arguments cannot be told apart. When they disagree on async-ness the call is deliberately
                    // treated as NOT async (today's behaviour, with the site's identity). A wrong "async" on a sync
                    // method would make the wrapper wait on a non-Task and never end the segment.
                    return Unresolved(DistroResolutionOutcome.AmbiguousDisagreeing, site, targetType);
                }
            }

            return new DistroResolvedMethod(
                outcome,
                isAsync,
                ComputeFunctionId(site.FunctionId, first.MetadataToken, first.Module.ModuleVersionId),
                first.DeclaringType,
                DistroSignatureFormatter.Format(first.GetParameters()));
        }
        catch (Exception)
        {
            return Unresolved(DistroResolutionOutcome.NotFound, site, targetType);
        }
    }

    /// <summary>
    /// FNV-1a 64 over the site's function id (8 bytes, little-endian), the method's metadata token (4 bytes,
    /// little-endian) and its module version id (16 bytes, <see cref="Guid.ToByteArray"/> order), so each real
    /// overload gets its own function id, as in agent mode.
    /// </summary>
    public static ulong ComputeFunctionId(ulong siteFunctionId, int metadataToken, Guid moduleVersionId)
    {
        var hash = FnvOffsetBasis;
        for (var i = 0; i < 8; i++)
        {
            hash = (hash ^ (byte)(siteFunctionId >> (8 * i))) * FnvPrime;
        }

        for (var i = 0; i < 4; i++)
        {
            hash = (hash ^ (byte)((uint)metadataToken >> (8 * i))) * FnvPrime;
        }

        foreach (var b in moduleVersionId.ToByteArray())
        {
            hash = (hash ^ b) * FnvPrime;
        }

        return hash;
    }

    private static DistroResolvedMethod Unresolved(DistroResolutionOutcome outcome, DistroInstrumentationSite site, Type targetType)
    {
        return new DistroResolvedMethod(outcome, false, site?.FunctionId ?? 0ul, targetType, site?.ArgumentSignature ?? string.Empty);
    }

    private static Type FindDeclaringType(DistroInstrumentationSite site, Type targetType, Type[] argumentTypes, IEnumerable<Assembly> loadedAssemblies)
    {
        // Non-generic classes: OTel passes the declaring type itself. Generic classes (and types nested in them) arrive
        // as a non-generic base or object, so those are resolved by name.
        if (targetType.FullName == site.TypeName)
        {
            return targetType;
        }

        var assembly = FindAssembly(site.AssemblyName, targetType, argumentTypes, loadedAssemblies);
        Log.Finest("OTel distro: looking up {0} by name; using assembly {1}.", site.TypeName, assembly?.FullName ?? "(none loaded)");
        return assembly?.GetType(site.TypeName, false, false);
    }

    private static Assembly FindAssembly(string assemblyName, Type targetType, Type[] argumentTypes, IEnumerable<Assembly> loadedAssemblies)
    {
        // Prefer the copy the call is actually using (the target's or an argument's assembly); otherwise the first
        // loaded assembly with that simple name, in AppDomain order.
        if (HasSimpleName(targetType.Assembly, assemblyName))
        {
            return targetType.Assembly;
        }

        foreach (var argumentType in argumentTypes)
        {
            if (HasSimpleName(argumentType.Assembly, assemblyName))
            {
                return argumentType.Assembly;
            }
        }

        foreach (var assembly in loadedAssemblies)
        {
            if (HasSimpleName(assembly, assemblyName))
            {
                return assembly;
            }
        }

        return null;
    }

    private static bool HasSimpleName(Assembly assembly, string simpleName)
    {
        return string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase);
    }

    private static List<MethodInfo> FindCandidates(Type declaringType, DistroInstrumentationSite site, Type[] argumentTypes)
    {
        var explicitSignature = site.ArgumentSignature.Length == 0 ? null : NormalizeXmlSignature(site.ArgumentSignature);
        var candidates = new List<MethodInfo>();
        foreach (var method in declaringType.GetMethods(DeclaredMethods))
        {
            if (method.Name != site.MethodName)
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != argumentTypes.Length || !ParametersMatch(parameters, argumentTypes))
            {
                continue;
            }

            if (explicitSignature != null && !string.Equals(explicitSignature, DistroSignatureFormatter.Format(parameters), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            candidates.Add(method);
        }

        return candidates;
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, Type[] argumentTypes)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!ParameterMatches(parameters[i].ParameterType, argumentTypes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ParameterMatches(Type parameterType, Type argumentType)
    {
        // A parameter containing a method (!!N) or class (!N) generic parameter matches any argument: OTel passes the
        // closed runtime type.
        if (parameterType == argumentType || parameterType.ContainsGenericParameters)
        {
            return true;
        }

        // By-ref parameters arrive as their element type; ref-struct parameters arrive as CallTargetRefStruct.
        var elementType = parameterType.IsByRef ? parameterType.GetElementType() : parameterType;
        if (elementType == argumentType)
        {
            return true;
        }

        return argumentType.FullName == CallTargetRefStructName && HasAttributeNamed(elementType.GetCustomAttributesData(), IsByRefLikeAttributeName);
    }

    private static bool IsAsync(MethodInfo method)
    {
        // Matched by name, as the NR profiler does (Function.h): old Microsoft.Bcl.Async-era assemblies carry their own
        // copy of the attribute type.
        return HasAttributeNamed(method.GetCustomAttributesData(), AsyncStateMachineAttributeName);
    }

    private static bool HasAttributeNamed(IList<CustomAttributeData> attributes, string fullName)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName == fullName)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeXmlSignature(string signature)
    {
        // As the profiler normalizes XML parameters: whitespace removed, "void" means a parameterless method.
        var builder = new StringBuilder(signature.Length);
        foreach (var c in signature)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        var normalized = builder.ToString();
        return string.Equals(normalized, "void", StringComparison.OrdinalIgnoreCase) ? string.Empty : normalized;
    }
}
