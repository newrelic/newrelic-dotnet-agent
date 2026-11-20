// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Core.OtelDistro;

namespace NewRelic.OtelDistro.Plugin;

// One cache class per arity. Each closed instantiation (integration class, target type, argument types) is one call
// signature OTel passes, so its static field holds the resolution of the method that signature fires: resolved at most
// once (a concurrent first call resolves twice at worst, and every caller then uses the one published value). TIntegration
// is a reference type, so the generic call runs as shared generic code and reaching this field is a per-instantiation
// static read through the runtime's generic dictionary (cached after the first lookup), then a null check: no lock,
// reflection or allocation on every call after the first. TIntegration only separates the sites: two integration
// classes with the same target and argument types get their own field. An unknown site resolves to null and is not
// cached, which makes Core start no tracer for that call. The slow path is shared (DistroMethodPublisher), so each class
// is only its field, the hot-path read and the argument types.

internal static class DistroMethodCache<TIntegration, TTarget>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), Type.EmptyTypes);
}

internal static class DistroMethodCache<TIntegration, TTarget, TArg1>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), new[] { typeof(TArg1) });
}

internal static class DistroMethodCache<TIntegration, TTarget, TArg1, TArg2>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), new[] { typeof(TArg1), typeof(TArg2) });
}

internal static class DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), new[] { typeof(TArg1), typeof(TArg2), typeof(TArg3) });
}

internal static class DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), new[] { typeof(TArg1), typeof(TArg2), typeof(TArg3), typeof(TArg4) });
}

internal static class DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), new[] { typeof(TArg1), typeof(TArg2), typeof(TArg3), typeof(TArg4), typeof(TArg5) });
}

internal static class DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), new[] { typeof(TArg1), typeof(TArg2), typeof(TArg3), typeof(TArg4), typeof(TArg5), typeof(TArg6) });
}

internal static class DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), new[] { typeof(TArg1), typeof(TArg2), typeof(TArg3), typeof(TArg4), typeof(TArg5), typeof(TArg6), typeof(TArg7) });
}

internal static class DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>
{
    private static DistroResolvedMethod _value;

    internal static DistroResolvedMethod Get(int siteIndex) => _value ?? DistroMethodPublisher.Publish(ref _value, siteIndex, typeof(TTarget), new[] { typeof(TArg1), typeof(TArg2), typeof(TArg3), typeof(TArg4), typeof(TArg5), typeof(TArg6), typeof(TArg7), typeof(TArg8) });
}

internal static class DistroMethodPublisher
{
    /// <summary>
    /// The slow path of every <c>DistroMethodCache</c>: resolves the method for the site and publishes it into
    /// <paramref name="field"/> unless another thread already did, returning whichever value is published. Returns null,
    /// publishing nothing, when there is no site table or no site at <paramref name="siteIndex"/>, and when this thread
    /// is already resolving (an instrumented method called from inside resolution would otherwise start another
    /// resolution without end): that inner call gets no tracer and its overload resolves on its next call.
    /// </summary>
    internal static DistroResolvedMethod Publish(ref DistroResolvedMethod field, int siteIndex, Type targetType, Type[] argumentTypes)
    {
        var resolved = DistroResolutionGuard.RunOnce(() =>
        {
            var sites = DistroSiteTable.Sites;
            if (sites == null || !sites.TryGet(siteIndex, out var site))
            {
                return null;
            }

            return DistroMethodResolver.Resolve(site, targetType, argumentTypes);
        });

        if (resolved == null)
        {
            return null;
        }

        return Interlocked.CompareExchange(ref field, resolved, null) ?? resolved;
    }
}
