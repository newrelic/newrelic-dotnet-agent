// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers;

public enum RuntimeLane
{
    Unknown = 0,
    Core = 1,
    Framework = 2,
}

/// <summary>
/// Decides which CI lane a host-run integration test class belongs to.
/// First match wins: explicit override, then the fixture's own answer, then the
/// class name.
/// </summary>
public sealed class RuntimeLaneResolver
{
    public const string TraitName = "Runtime";
    public const string CoreValue = "Core";
    public const string FrameworkValue = "Framework";

    private static readonly string[] FrameworkNameMarkers = { "FWLatest", "FW481", "FW48", "FW471", "FW462", "Framework", "NetFramework" };
    private static readonly string[] CoreNameMarkers = { "CoreLatest", "CoreOldest", "Core100", "Core80", "NetCore", "Core" };

    private static readonly ConcurrentDictionary<Type, RuntimeLane> FixtureLaneCache = new ConcurrentDictionary<Type, RuntimeLane>();

    /// <summary>
    /// Fixture types that must never be constructed for classification, because
    /// their constructor does real work (starts a process, copies files, opens a
    /// network connection) instead of only composing paths. Each value is the
    /// lane that construction would have reported, checked by reading what the
    /// fixture passes to its base RemoteApplication / RemoteService, not guessed
    /// from the fixture's own name.
    /// </summary>
    private static readonly Dictionary<string, RuntimeLane> DoNotConstruct = new Dictionary<string, RuntimeLane>(StringComparer.Ordinal)
    {
        // Constructor eagerly calls OwinRemotingServerApplication.CopyToRemote() and Start().
        ["OwinRemotingFixture"] = RuntimeLane.Framework,
        // Constructor eagerly starts a ChromeDriverService and a headless Chrome process (new ChromeDriver(...)).
        ["BasicAspWebServiceFixture"] = RuntimeLane.Framework,
        // Constructor eagerly starts a ChromeDriverService and a headless Chrome process (new ChromeDriver(...)).
        ["BlazorSignalRApplicationFixture"] = RuntimeLane.Core,
    };

    private readonly IReadOnlyDictionary<string, RuntimeLane> _classOverrides;

    public RuntimeLaneResolver(IReadOnlyDictionary<string, RuntimeLane> classOverrides)
    {
        _classOverrides = classOverrides ?? new Dictionary<string, RuntimeLane>();
    }

    public RuntimeLane Resolve(Type testClass)
    {
        if (testClass == null)
        {
            return RuntimeLane.Unknown;
        }

        if (testClass.FullName != null && _classOverrides.TryGetValue(testClass.FullName, out var overridden))
        {
            return overridden;
        }

        var fixtureType = GetFixtureType(testClass);
        if (fixtureType != null)
        {
            var fromFixture = ResolveFromFixtureType(fixtureType);
            if (fromFixture != RuntimeLane.Unknown)
            {
                return fromFixture;
            }
        }

        return ResolveFromClassName(testClass.FullName);
    }

    /// <summary>
    /// Returns the fixture type from the class's IClassFixture&lt;T&gt;, or null.
    /// </summary>
    public static Type GetFixtureType(Type testClass)
    {
        if (testClass == null)
        {
            return null;
        }

        return testClass.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IClassFixture<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    /// <summary>
    /// The console fixture family states its runtime in its name. Most other
    /// fixtures compose paths only, so they are asked directly: construct once
    /// and read IsCoreApp. A fixture in DoNotConstruct does real work in its
    /// constructor and is never instantiated; its recorded lane is used instead.
    /// </summary>
    public RuntimeLane ResolveFromFixtureType(Type fixtureType)
    {
        if (fixtureType == null)
        {
            return RuntimeLane.Unknown;
        }

        return FixtureLaneCache.GetOrAdd(fixtureType, type =>
        {
            var name = type.Name;
            if (name.StartsWith("ConsoleDynamicMethodFixtureFW", StringComparison.Ordinal))
            {
                return RuntimeLane.Framework;
            }

            if (name.StartsWith("ConsoleDynamicMethodFixtureCore", StringComparison.Ordinal))
            {
                return RuntimeLane.Core;
            }

            if (DoNotConstruct.TryGetValue(name, out var knownLane))
            {
                return knownLane;
            }

            try
            {
                if (Activator.CreateInstance(type) is RemoteApplicationFixture fixture)
                {
                    return fixture.RemoteApplication.IsCoreApp ? RuntimeLane.Core : RuntimeLane.Framework;
                }
            }
            catch (Exception)
            {
                // An abstract fixture, a non-parameterless ctor, or a missing
                // application directory. Fall through to the class name.
            }

            return RuntimeLane.Unknown;
        });
    }

    /// <summary>
    /// Last resort. Framework is checked first: the AspNetCore MVC-on-Framework
    /// classes match both marker sets, and Framework is the correct answer.
    /// </summary>
    public static RuntimeLane ResolveFromClassName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            return RuntimeLane.Unknown;
        }

        if (FrameworkNameMarkers.Any(m => fullName.Contains(m)))
        {
            return RuntimeLane.Framework;
        }

        if (CoreNameMarkers.Any(m => fullName.Contains(m)))
        {
            return RuntimeLane.Core;
        }

        return RuntimeLane.Unknown;
    }

    public static string ToTraitValue(RuntimeLane lane)
    {
        switch (lane)
        {
            case RuntimeLane.Core:
                return CoreValue;
            case RuntimeLane.Framework:
                return FrameworkValue;
            default:
                throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown is not a trait value.");
        }
    }
}
