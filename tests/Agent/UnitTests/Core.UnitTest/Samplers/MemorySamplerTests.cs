// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers;

[TestFixture]
public class MemorySamplerTests
{
    private MemorySampler _memorySampler;

    private IMemorySampleTransformer _memorySampleTransformer;

    private Action _sampleAction;

    private long _privateMemorySize;
    private long _workingSet;

    [SetUp]
    public void SetUp()
    {
        var scheduler = Mock.Create<IScheduler>();
        Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
            .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _sampleAction = action);
        _memorySampleTransformer = Mock.Create<IMemorySampleTransformer>();

        var processStatic = Mock.Create<IProcessStatic>();
        var process = Mock.Create<IProcess>();
        Mock.Arrange(() => processStatic.GetCurrentProcess()).Returns(process);
        Mock.Arrange(() => process.PrivateMemorySize64).Returns(() => _privateMemorySize);
        Mock.Arrange(() => process.WorkingSet64).Returns(() => _workingSet);

        _memorySampler = new MemorySampler(scheduler, _memorySampleTransformer, processStatic);
        _memorySampler.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _memorySampler.Dispose();
    }

    [Test]
    public void memory_sample_generated_on_sample()
    {
        // Arrange
        _privateMemorySize = 1000;
        _workingSet = 2000;

        var memorySample = null as ImmutableMemorySample;
        Mock.Arrange(() => _memorySampleTransformer.Transform(Arg.IsAny<ImmutableMemorySample>()))
            .DoInstead<ImmutableMemorySample>(sample => memorySample = sample);

        // Act
        _sampleAction();

        // Assert
        Assert.That(memorySample, Is.Not.Null);
    }

    [Test]
    public void memory_values_increase_over_time()
    {
        // Arrange
        _privateMemorySize = 1000;
        _workingSet = 2000;

        var memorySampleBefore = null as ImmutableMemorySample;
        Mock.Arrange(() => _memorySampleTransformer.Transform(Arg.IsAny<ImmutableMemorySample>()))
            .DoInstead<ImmutableMemorySample>(sample => memorySampleBefore = sample);

        // Act
        _sampleAction();

        // Arrange -- simulate memory usage increasing between samples
        _privateMemorySize = 5000;
        _workingSet = 6000;

        var memorySampleAfter = null as ImmutableMemorySample;
        Mock.Arrange(() => _memorySampleTransformer.Transform(Arg.IsAny<ImmutableMemorySample>()))
            .DoInstead<ImmutableMemorySample>(sample => memorySampleAfter = sample);

        // Act
        _sampleAction();

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(memorySampleBefore.MemoryPrivate, Is.LessThan(memorySampleAfter.MemoryPrivate), "PrivateMemorySize64 did not increase as expected");
            Assert.That(memorySampleBefore.MemoryWorkingSet, Is.LessThan(memorySampleAfter.MemoryWorkingSet), "WorkingSet64 did not increase as expected");
        });
    }

    [Test]
    public void memory_values_unchanged_when_reported_values_are_the_same()
    {
        // Arrange
        _privateMemorySize = 3000;
        _workingSet = 4000;

        var memorySampleBefore = null as ImmutableMemorySample;
        Mock.Arrange(() => _memorySampleTransformer.Transform(Arg.IsAny<ImmutableMemorySample>()))
            .DoInstead<ImmutableMemorySample>(sample => memorySampleBefore = sample);

        // Act
        _sampleAction();

        // Arrange -- values reported by the process do not change between samples
        var memorySampleAfter = null as ImmutableMemorySample;
        Mock.Arrange(() => _memorySampleTransformer.Transform(Arg.IsAny<ImmutableMemorySample>()))
            .DoInstead<ImmutableMemorySample>(sample => memorySampleAfter = sample);

        // Act
        _sampleAction();

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(memorySampleAfter.MemoryPrivate, Is.EqualTo(memorySampleBefore.MemoryPrivate), "PrivateMemorySize64 should not have changed");
            Assert.That(memorySampleAfter.MemoryWorkingSet, Is.EqualTo(memorySampleBefore.MemoryWorkingSet), "WorkingSet64 should not have changed");
        });
    }
}
