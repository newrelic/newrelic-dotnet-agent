// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Coordinator.Tests;

[TestFixture]
public class RegistrationCoordinatorTests
{
    private const int ProcessId = 42;

    private class FakeNative : INativeRegistrar
    {
        private readonly object _gate = new object();
        private readonly List<(string Id, CoordinatorDefinition[] Definitions, uint Categories)> _calls = new List<(string, CoordinatorDefinition[], uint)>();

        public Func<CoordinatorDefinition[], int> Behavior { get; set; } = definitions => definitions.Length;

        public List<(string Id, CoordinatorDefinition[] Definitions, uint Categories)> Calls
        {
            get
            {
                lock (_gate)
                {
                    return _calls.ToList();
                }
            }
        }

        public int Register(string id, CoordinatorDefinition[] definitions, uint enabledCategories)
        {
            lock (_gate)
            {
                _calls.Add((id, definitions, enabledCategories));
            }

            return Behavior(definitions);
        }
    }

    private static CoordinatorDefinition Def(string method, string[] signature = null) =>
        new CoordinatorDefinition("asm", "Type", method, signature ?? new[] { "System.Void" },
            1, 2, 3, 4, 5, 6, "intAsm", "IntType", 0, 1);

    private static (RegistrationCoordinator Coordinator, FakeNative Native) Create()
    {
        var native = new FakeNative();
        return (new RegistrationCoordinator(native, ProcessId), native);
    }

    [Test]
    public void Register_NewDefinitions_SendsAllOnce_ReturnsSent()
    {
        var (coordinator, native) = Create();

        var result = coordinator.Register(new[] { Def("a"), Def("b") });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.Sent));
            Assert.That(result.Requested, Is.EqualTo(2));
            Assert.That(result.Sent, Is.EqualTo(2));
            Assert.That(result.NativeTaken, Is.EqualTo(2));
            Assert.That(result.Id, Is.EqualTo("NR-42-1"));
            Assert.That(result.Failure, Is.Null);
            Assert.That(native.Calls, Has.Count.EqualTo(1));
            Assert.That(native.Calls[0].Id, Is.EqualTo("NR-42-1"));
            Assert.That(native.Calls[0].Definitions.Select(d => d.TargetMethod), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(native.Calls[0].Categories, Is.EqualTo(uint.MaxValue));
        });
    }

    [Test]
    public void Register_SameDefinitionsAgain_ReturnsAllPresent_NoNativeCall()
    {
        var (coordinator, native) = Create();
        coordinator.Register(new[] { Def("a"), Def("b") });

        var result = coordinator.Register(new[] { Def("a"), Def("b") });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.AllPresent));
            Assert.That(result.Requested, Is.EqualTo(2));
            Assert.That(result.Sent, Is.EqualTo(0));
            Assert.That(result.Id, Is.Null);
            Assert.That(native.Calls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Register_OverlappingBatch_SendsOnlyNew()
    {
        var (coordinator, native) = Create();
        coordinator.Register(new[] { Def("a"), Def("b") });

        var result = coordinator.Register(new[] { Def("b"), Def("c") });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.Sent));
            Assert.That(result.Requested, Is.EqualTo(2));
            Assert.That(result.Sent, Is.EqualTo(1));
            Assert.That(result.Id, Is.EqualTo("NR-42-2"));
            Assert.That(native.Calls[1].Definitions.Select(d => d.TargetMethod), Is.EqualTo(new[] { "c" }));
        });
    }

    [Test]
    public void Register_DuplicateInsideBatch_SentOnce()
    {
        var (coordinator, native) = Create();

        var result = coordinator.Register(new[] { Def("a"), Def("a") });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.Sent));
            Assert.That(result.Requested, Is.EqualTo(2));
            Assert.That(result.Sent, Is.EqualTo(1));
            Assert.That(native.Calls[0].Definitions, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void Register_NativeReturnsZero_FailedAndNotMarked()
    {
        var (coordinator, native) = Create();
        native.Behavior = _ => 0;

        var first = coordinator.Register(new[] { Def("a") });
        var second = coordinator.Register(new[] { Def("a") });

        Assert.Multiple(() =>
        {
            Assert.That(first.Outcome, Is.EqualTo(CoordinatorOutcome.Failed));
            Assert.That(first.Failure, Does.Contain("OTel native took 0 of 1"));
            Assert.That(first.Sent, Is.EqualTo(0));
            Assert.That(second.Outcome, Is.EqualTo(CoordinatorOutcome.Failed));
            Assert.That(native.Calls, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Register_NativeThrows_FailedWithTypeAndMessage_NotMarked()
    {
        var (coordinator, native) = Create();
        native.Behavior = _ => throw new DllNotFoundException("x");

        var first = coordinator.Register(new[] { Def("a") });
        native.Behavior = definitions => definitions.Length;
        var second = coordinator.Register(new[] { Def("a") });

        Assert.Multiple(() =>
        {
            Assert.That(first.Outcome, Is.EqualTo(CoordinatorOutcome.Failed));
            Assert.That(first.Failure, Is.EqualTo("DllNotFoundException: x"));
            Assert.That(native.Calls, Has.Count.EqualTo(2));
            Assert.That(second.Outcome, Is.EqualTo(CoordinatorOutcome.Sent));
        });
    }

    [Test]
    public void Register_NativePartial_MarksWholeBatchSent()
    {
        var (coordinator, native) = Create();
        native.Behavior = _ => 2;
        var batch = new[] { Def("a"), Def("b"), Def("c") };

        var first = coordinator.Register(batch);
        var second = coordinator.Register(batch);

        Assert.Multiple(() =>
        {
            Assert.That(first.Outcome, Is.EqualTo(CoordinatorOutcome.Sent));
            Assert.That(first.NativeTaken, Is.EqualTo(2));
            Assert.That(first.Sent, Is.EqualTo(3));
            Assert.That(second.Outcome, Is.EqualTo(CoordinatorOutcome.AllPresent));
            Assert.That(native.Calls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Register_Null_Failed()
    {
        var (coordinator, native) = Create();

        var result = coordinator.Register(null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.Failed));
            Assert.That(result.Failure, Does.Contain("no definitions"));
            Assert.That(native.Calls, Is.Empty);
        });
    }

    [Test]
    public void Register_Empty_AllPresent()
    {
        var (coordinator, native) = Create();

        var result = coordinator.Register(new CoordinatorDefinition[0]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.AllPresent));
            Assert.That(result.Requested, Is.EqualTo(0));
            Assert.That(native.Calls, Is.Empty);
        });
    }

    [Test]
    public void Register_NullElement_FailedNativeNotCalled()
    {
        var (coordinator, native) = Create();

        var result = coordinator.Register(new[] { Def("a"), null });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.Failed));
            Assert.That(result.Failure, Does.Contain("null definition"));
            Assert.That(result.Requested, Is.EqualTo(2));
            Assert.That(native.Calls, Is.Empty);
        });
    }

    [Test]
    public void Register_NullNative_ReturnsFailedInsteadOfThrowing()
    {
        var coordinator = new RegistrationCoordinator(null, ProcessId);

        var result = coordinator.Register(new[] { Def("a") });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.Failed));
            Assert.That(result.Failure, Does.StartWith("NullReferenceException"));
        });
    }

    [Test]
    public void Register_ConcurrentCalls_SameDefs_NativeOnce()
    {
        var (coordinator, native) = Create();
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return coordinator.Register(new[] { Def("a") });
        })).ToArray();

        start.Set();
        Task.WaitAll(tasks);

        Assert.Multiple(() =>
        {
            Assert.That(native.Calls, Has.Count.EqualTo(1));
            Assert.That(tasks.Count(t => t.Result.Outcome == CoordinatorOutcome.Sent), Is.EqualTo(1));
            Assert.That(tasks.Count(t => t.Result.Outcome == CoordinatorOutcome.AllPresent), Is.EqualTo(7));
        });
    }

    [Test]
    public void InitializeLifetimeService_ReturnsNull()
    {
        var (coordinator, _) = Create();

#pragma warning disable SYSLIB0010 // Obsolete on .NET (Core); the override is the .NET Framework remoting contract under test.
        Assert.That(coordinator.InitializeLifetimeService(), Is.Null);
#pragma warning restore SYSLIB0010
    }

    [Test]
    public void Version_IsAssemblyFullName()
    {
        var (coordinator, _) = Create();

        Assert.That(coordinator.Version, Is.EqualTo(typeof(RegistrationCoordinator).Assembly.FullName));
    }

    [Test]
    public void DefaultConstructor_RealNative_NoProfiler_ReturnsFailedNotMarked()
    {
        var coordinator = new RegistrationCoordinator();

        var result = coordinator.Register(new[] { Def("a") });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CoordinatorOutcome.Failed));
            Assert.That(result.Failure, Does.StartWith("DllNotFoundException").Or.StartWith("EntryPointNotFoundException"));
            Assert.That(result.Id, Is.EqualTo("NR-" + System.Diagnostics.Process.GetCurrentProcess().Id + "-1"));
        });
    }

    private static IEnumerable<TestCaseData> DifferingDefinitions()
    {
        yield return new TestCaseData(new CoordinatorDefinition("X", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_TargetAssembly");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "X", "m", new[] { "s" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_TargetType");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "X", new[] { "s" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_TargetMethod");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "X" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_SignatureTypes");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s", "t" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_SignatureTypesCount");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 9, 2, 3, 4, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_MinMajor");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 9, 3, 4, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_MinMinor");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 9, 4, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_MinPatch");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 9, 5, 6, "ia", "it", 7, 8)).SetName("Key_Differs_MaxMajor");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 9, 6, "ia", "it", 7, 8)).SetName("Key_Differs_MaxMinor");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 5, 9, "ia", "it", 7, 8)).SetName("Key_Differs_MaxPatch");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 5, 6, "X", "it", 7, 8)).SetName("Key_Differs_IntegrationAssembly");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 5, 6, "ia", "X", 7, 8)).SetName("Key_Differs_IntegrationType");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 5, 6, "ia", "it", 9, 8)).SetName("Key_Differs_Kind");
        yield return new TestCaseData(new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 9)).SetName("Key_Differs_Categories");
    }

    [TestCaseSource(nameof(DifferingDefinitions))]
    public void Key_DiffersByEveryField(CoordinatorDefinition other)
    {
        var baseline = new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8);

        Assert.That(other.Key, Is.Not.EqualTo(baseline.Key));
    }

    [Test]
    public void Key_SameFields_Equal()
    {
        var a = new CoordinatorDefinition("asm", "Type", "m", new[] { "s", "t" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8);
        var b = new CoordinatorDefinition("asm", "Type", "m", new[] { "s", "t" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8);

        Assert.That(a.Key, Is.EqualTo(b.Key));
    }

    [Test]
    public void Key_NullSignatureTypes_EqualsEmpty()
    {
        var withNull = new CoordinatorDefinition("asm", "Type", "m", null, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8);
        var withEmpty = new CoordinatorDefinition("asm", "Type", "m", new string[0], 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8);

        Assert.That(withNull.Key, Is.EqualTo(withEmpty.Key));
    }

    [Test]
    public void Definition_ExposesEveryConstructorArgument()
    {
        var d = new CoordinatorDefinition("asm", "Type", "m", new[] { "s" }, 1, 2, 3, 4, 5, 6, "ia", "it", 7, 8);

        Assert.Multiple(() =>
        {
            Assert.That(d.TargetAssembly, Is.EqualTo("asm"));
            Assert.That(d.TargetType, Is.EqualTo("Type"));
            Assert.That(d.TargetMethod, Is.EqualTo("m"));
            Assert.That(d.SignatureTypes, Is.EqualTo(new[] { "s" }));
            Assert.That(d.MinMajor, Is.EqualTo(1));
            Assert.That(d.MinMinor, Is.EqualTo(2));
            Assert.That(d.MinPatch, Is.EqualTo(3));
            Assert.That(d.MaxMajor, Is.EqualTo(4));
            Assert.That(d.MaxMinor, Is.EqualTo(5));
            Assert.That(d.MaxPatch, Is.EqualTo(6));
            Assert.That(d.IntegrationAssembly, Is.EqualTo("ia"));
            Assert.That(d.IntegrationType, Is.EqualTo("it"));
            Assert.That(d.Kind, Is.EqualTo(7));
            Assert.That(d.Categories, Is.EqualTo(8));
        });
    }

    [Test]
    public void Types_AreSerializable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(CoordinatorDefinition).IsDefined(typeof(SerializableAttribute), false), Is.True);
            Assert.That(typeof(CoordinatorResult).IsDefined(typeof(SerializableAttribute), false), Is.True);
        });
    }
}
