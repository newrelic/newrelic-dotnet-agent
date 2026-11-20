// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Coordinator.Tests;

[TestFixture]
public class CoordinatorSlotTests
{
    private static readonly string RealVersion = new RegistrationCoordinator().Version;

    private class FakeSlot : ICoordinatorSlot
    {
        public object Value { get; set; }
        public bool GetThrows { get; set; }
        public bool SetThrows { get; set; }
        public int SetCalls { get; private set; }

        public object Get()
        {
            if (GetThrows)
            {
                throw new InvalidOperationException("get boom");
            }

            return Value;
        }

        public void Set(object value)
        {
            SetCalls++;
            if (SetThrows)
            {
                throw new InvalidOperationException("set boom");
            }

            Value = value;
        }
    }

    private static RegistrationCoordinator NewCoordinator() => new RegistrationCoordinator(new NoopNative(), 1);

    private class NoopNative : INativeRegistrar
    {
        public int Register(string id, CoordinatorDefinition[] definitions, uint enabledCategories) => definitions.Length;
    }

    [Test]
    public void EmptySlot_Creates_Stores_CreatedTrue()
    {
        var slot = new FakeSlot();
        var made = NewCoordinator();

        var result = CoordinatorSlot.FindOrCreate(slot, RealVersion, () => made, out var created, out var failure);

        Assert.That(result, Is.SameAs(made));
        Assert.That(slot.Value, Is.SameAs(made));
        Assert.That(created, Is.True);
        Assert.That(failure, Is.Null);
    }

    [Test]
    public void SlotHoldsSameVersion_Returns_NoCreate()
    {
        var existing = NewCoordinator();
        var slot = new FakeSlot { Value = existing };
        var createCalls = 0;

        var result = CoordinatorSlot.FindOrCreate(slot, RealVersion, () => { createCalls++; return NewCoordinator(); }, out var created, out var failure);

        Assert.That(result, Is.SameAs(existing));
        Assert.That(created, Is.False);
        Assert.That(failure, Is.Null);
        Assert.That(createCalls, Is.EqualTo(0));
        Assert.That(slot.SetCalls, Is.EqualTo(0));
    }

    [Test]
    public void SlotHoldsOtherVersion_NullFailure()
    {
        var slot = new FakeSlot { Value = NewCoordinator() };
        var createCalls = 0;

        var result = CoordinatorSlot.FindOrCreate(slot, "other-version", () => { createCalls++; return NewCoordinator(); }, out var created, out var failure);

        Assert.That(result, Is.Null);
        Assert.That(created, Is.False);
        Assert.That(failure, Is.EqualTo("coordinator version mismatch: slot has " + RealVersion + ", expected other-version"));
        Assert.That(createCalls, Is.EqualTo(0));
    }

    [Test]
    public void SlotHoldsForeignObject_NullFailure()
    {
        var slot = new FakeSlot { Value = "not a coordinator" };
        var createCalls = 0;

        var result = CoordinatorSlot.FindOrCreate(slot, RealVersion, () => { createCalls++; return NewCoordinator(); }, out var created, out var failure);

        Assert.That(result, Is.Null);
        Assert.That(created, Is.False);
        Assert.That(failure, Is.EqualTo("slot holds System.String"));
        Assert.That(createCalls, Is.EqualTo(0));
    }

    [Test]
    public void CreateThrows_NullFailure()
    {
        var slot = new FakeSlot();

        var result = CoordinatorSlot.FindOrCreate(slot, RealVersion, () => throw new InvalidOperationException("boom"), out var created, out var failure);

        Assert.That(result, Is.Null);
        Assert.That(created, Is.False);
        Assert.That(failure, Is.EqualTo("create: InvalidOperationException: boom"));
        Assert.That(slot.Value, Is.Null);
    }

    [Test]
    public void CreateReturnsNull_NullFailure()
    {
        var slot = new FakeSlot();

        var result = CoordinatorSlot.FindOrCreate(slot, RealVersion, () => null, out var created, out var failure);

        Assert.That(result, Is.Null);
        Assert.That(created, Is.False);
        Assert.That(failure, Is.EqualTo("create: returned null"));
        Assert.That(slot.SetCalls, Is.EqualTo(0));
    }

    [Test]
    public void SlotGetThrows_NullFailure()
    {
        var slot = new FakeSlot { GetThrows = true };
        var createCalls = 0;

        var result = CoordinatorSlot.FindOrCreate(slot, RealVersion, () => { createCalls++; return NewCoordinator(); }, out var created, out var failure);

        Assert.That(result, Is.Null);
        Assert.That(created, Is.False);
        Assert.That(failure, Is.EqualTo("slot get: InvalidOperationException: get boom"));
        Assert.That(createCalls, Is.EqualTo(0));
    }

    [Test]
    public void SlotSetThrows_NullFailure()
    {
        var slot = new FakeSlot { SetThrows = true };

        var result = CoordinatorSlot.FindOrCreate(slot, RealVersion, () => NewCoordinator(), out var created, out var failure);

        Assert.That(result, Is.Null);
        Assert.That(created, Is.False);
        Assert.That(failure, Is.EqualTo("slot set: InvalidOperationException: set boom"));
    }

    [Test]
    public void CoordinatorHost_TryGet_InProcess_ReturnsSameInstanceTwice()
    {
        var first = CoordinatorHost.TryGet(out var firstLocation, out var firstFailure);
        var second = CoordinatorHost.TryGet(out var secondLocation, out var secondFailure);

        Assert.That(firstFailure, Is.Null);
        Assert.That(secondFailure, Is.Null);
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.SameAs(first));
#if NETFRAMEWORK
        // The NUnit adapter runs tests in a non-default AppDomain, so on .NET Framework this exercises the real
        // default-domain route (MetaHost COM activation, named mutex, AppDomain data slot, cross-domain proxy).
        Assert.That(firstLocation, Is.AnyOf("default AppDomain", "in-process"));
        Assert.That(secondLocation, Is.EqualTo(firstLocation));
#else
        Assert.That(firstLocation, Is.EqualTo("in-process"));
        Assert.That(secondLocation, Is.EqualTo("in-process"));
#endif
    }
}
