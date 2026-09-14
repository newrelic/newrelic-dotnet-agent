// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.TestInfrastructure;

[Trait(RuntimeLaneResolver.TraitName, RuntimeLaneResolver.CoreValue)]
[Trait(RuntimeLaneResolver.TraitName, RuntimeLaneResolver.FrameworkValue)]
public class RuntimeLaneResolverTests
{
    private readonly RuntimeLaneResolver _resolver = new RuntimeLaneResolver(new Dictionary<string, RuntimeLane>());

    [Fact]
    public void GetFixtureType_ReadsTheClassFixtureGenericArgument()
    {
        Assert.Equal(
            typeof(ConsoleDynamicMethodFixtureCoreLatest),
            RuntimeLaneResolver.GetFixtureType(typeof(Grpc.GrpcTests_NetCoreLatest)));
    }

    [Fact]
    public void GetFixtureType_ReturnsNullWhenThereIsNoClassFixture()
    {
        Assert.Null(RuntimeLaneResolver.GetFixtureType(typeof(RuntimeLaneResolverTests)));
    }

    [Theory]
    [InlineData(typeof(ConsoleDynamicMethodFixtureCoreLatest), RuntimeLane.Core)]
    [InlineData(typeof(ConsoleDynamicMethodFixtureCoreOldest), RuntimeLane.Core)]
    [InlineData(typeof(ConsoleDynamicMethodFixtureFWLatest), RuntimeLane.Framework)]
    [InlineData(typeof(ConsoleDynamicMethodFixtureFW462), RuntimeLane.Framework)]
    public void ResolveFromFixtureType_ClassifiesTheConsoleFixtureFamilyByName(Type fixtureType, RuntimeLane expected)
    {
        Assert.Equal(expected, _resolver.ResolveFromFixtureType(fixtureType));
    }

    [Fact]
    public void ResolveFromFixtureType_ReadsIsCoreAppForFixturesWhoseNameSaysNothing()
    {
        // BasicMvcApplicationTestFixture launches through RemoteWebApplication
        // (HostedWebCore), so it is Framework, and nothing in its name says so.
        Assert.Equal(RuntimeLane.Framework, _resolver.ResolveFromFixtureType(typeof(RemoteServiceFixtures.BasicMvcApplicationTestFixture)));
    }

    [Theory]
    [InlineData("NewRelic.Agent.IntegrationTests.Grpc.GrpcTests_NetCoreLatest", RuntimeLane.Core)]
    [InlineData("NewRelic.Agent.IntegrationTests.Api.ApiTestsFWLatest", RuntimeLane.Framework)]
    [InlineData("NewRelic.Agent.IntegrationTests.Api.SomethingUnmarked", RuntimeLane.Unknown)]
    public void ResolveFromClassName_IsTheLastResort(string fullName, RuntimeLane expected)
    {
        Assert.Equal(expected, RuntimeLaneResolver.ResolveFromClassName(fullName));
    }

    [Fact]
    public void ResolveFromClassName_PrefersFrameworkWhenBothMarkersArePresent()
    {
        // AspNetCore MVC applications running on .NET Framework match both
        // patterns. Framework must win, or all three land on the Linux lane.
        Assert.Equal(
            RuntimeLane.Framework,
            RuntimeLaneResolver.ResolveFromClassName(
                "NewRelic.Agent.IntegrationTests.AspNetCore.AspNetCoreMvcCoreFrameworkTests"));
    }

    [Fact]
    public void Resolve_PutsTheThreeMisleadinglyNamedAspNetCoreClassesOnFramework()
    {
        foreach (var type in new[]
        {
            typeof(AspNetCore.AspNetCoreMvcFrameworkTests),
            typeof(AspNetCore.AspNetCoreMvcFrameworkAsyncTests),
            typeof(AspNetCore.AspNetCoreMvcCoreFrameworkTests),
        })
        {
            Assert.Equal(RuntimeLane.Framework, _resolver.Resolve(type));
        }
    }

    [Fact]
    public void Resolve_LetsAnExplicitOverrideBeatTheFixture()
    {
        var overrides = new Dictionary<string, RuntimeLane>
        {
            [typeof(Grpc.GrpcTests_NetCoreLatest).FullName] = RuntimeLane.Framework,
        };
        Assert.Equal(RuntimeLane.Framework, new RuntimeLaneResolver(overrides).Resolve(typeof(Grpc.GrpcTests_NetCoreLatest)));
    }

    [Fact]
    public void Resolve_ReturnsUnknownForAClassItCannotClassify()
    {
        Assert.Equal(RuntimeLane.Unknown, _resolver.Resolve(typeof(RuntimeLaneResolverTests)));
    }

    [Theory]
    [InlineData(RuntimeLane.Core, "Core")]
    [InlineData(RuntimeLane.Framework, "Framework")]
    public void ToTraitValue_MapsTheLaneToItsTraitValue(RuntimeLane lane, string expected)
    {
        Assert.Equal(expected, RuntimeLaneResolver.ToTraitValue(lane));
    }

    [Fact]
    public void ToTraitValue_RejectsUnknown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeLaneResolver.ToTraitValue(RuntimeLane.Unknown));
    }
}
