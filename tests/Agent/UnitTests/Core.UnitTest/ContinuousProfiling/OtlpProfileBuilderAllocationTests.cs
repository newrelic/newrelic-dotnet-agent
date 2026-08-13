// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Profiles.V1Development;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

/// <summary>
/// Covers the allocation half of <see cref="OtlpProfileBuilder"/>: the allocated_objects / allocated_space
/// profiles emitted from <see cref="AllocationSample"/>s into the SAME request/dictionary as the cpu/off_cpu
/// profiles. The cpu/off_cpu behavior itself is covered by <see cref="OtlpProfileBuilderTests"/>.
/// </summary>
[TestFixture]
public class OtlpProfileBuilderAllocationTests
{
    private const long PeriodNanos = 1_000_000L;

    private static AllocationSample Allocation(string typeName, ulong size, long span = 0, params string[] frames) =>
        new AllocationSample("alloc-thread", 42, 0, 0, span, 1_700_000_000_000L, size, typeName,
            frames.Length == 0 ? new[] { "MyApp.Widget.Create()" } : frames);

    private static ManagedThreadSample ThreadSample(bool onCpu) =>
        new ManagedThreadSample("worker", 7, 0, 0, 0, new[] { "MyApp.Work()" }, onCpu);

    private static string TypeNameOf(ExportProfilesServiceRequest req, Profile p) =>
        req.Dictionary.StringTable[p.SampleType.TypeStrindex];

    private static string UnitOf(ExportProfilesServiceRequest req, Profile p) =>
        req.Dictionary.StringTable[p.SampleType.UnitStrindex];

    private static Profile ProfileOfType(ExportProfilesServiceRequest req, string sampleTypeName) =>
        req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles.SingleOrDefault(p => TypeNameOf(req, p) == sampleTypeName);

    private static Dictionary<string, OpenTelemetry.Proto.Common.V1.AnyValue> AttributesOf(ExportProfilesServiceRequest req, Sample s) =>
        s.AttributeIndices
            .Select(i => req.Dictionary.AttributeTable[i])
            .ToDictionary(kv => req.Dictionary.StringTable[kv.KeyStrindex], kv => kv.Value);

    [Test]
    public void Build_with_thread_and_allocation_samples_emits_all_four_profiles_in_one_request()
    {
        var req = OtlpProfileBuilder.Build(
            new[] { ThreadSample(onCpu: true), ThreadSample(onCpu: false) },
            startUnixNano: 1000, durationNano: 5000, serviceName: "svc", periodNanos: PeriodNanos,
            includeAgentCode: true,
            allocationSamples: new[] { Allocation("MyApp.Widget", 65536UL) });

        // One request, one resource, one scope, one shared dictionary -- four profiles inside it.
        var profiles = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles;

        Assert.Multiple(() =>
        {
            Assert.That(profiles.Select(p => TypeNameOf(req, p)),
                Is.EqualTo(new[] { "off_cpu", "cpu", "allocated_objects", "allocated_space" }));
            Assert.That(UnitOf(req, profiles[2]), Is.EqualTo("count"));
            Assert.That(UnitOf(req, profiles[3]), Is.EqualTo("bytes"));
            Assert.That(req.Dictionary, Is.Not.Null);
        });
    }

    [Test]
    public void Build_allocation_and_thread_samples_share_one_interned_stack()
    {
        // An allocation sample whose stack is identical to a thread sample's must NOT be re-interned: both
        // sample kinds intern through the same string/function/location/stack caches.
        var req = OtlpProfileBuilder.Build(
            new[] { ThreadSample(onCpu: true) },
            0, 0, "svc", PeriodNanos, includeAgentCode: true,
            allocationSamples: new[] { Allocation("MyApp.Widget", 8UL, 0, "MyApp.Work()") });

        var dict = req.Dictionary;
        Assert.Multiple(() =>
        {
            // index 0 zero value + the single shared "MyApp.Work()" entry.
            Assert.That(dict.FunctionTable, Has.Count.EqualTo(2));
            Assert.That(dict.LocationTable, Has.Count.EqualTo(2));
            Assert.That(dict.StackTable, Has.Count.EqualTo(2));
            // Both sample kinds point at the same stack index.
            Assert.That(ProfileOfType(req, "cpu").Samples.Single().StackIndex,
                Is.EqualTo(ProfileOfType(req, "allocated_objects").Samples.Single().StackIndex));
        });
    }

    [Test]
    public void Build_allocated_objects_values_are_all_one_and_allocated_space_values_are_the_allocated_sizes()
    {
        var allocations = new[]
        {
            Allocation("MyApp.A", 16UL, 0, "MyApp.A.Create()"),
            Allocation("MyApp.B", 1_048_576UL, 0, "MyApp.B.Create()"),
            Allocation("MyApp.C", 24UL, 0, "MyApp.C.Create()"),
        };

        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: allocations);

        var objects = ProfileOfType(req, "allocated_objects");
        var space = ProfileOfType(req, "allocated_space");

        Assert.Multiple(() =>
        {
            // Not a partition: every allocation sample appears in BOTH profiles.
            Assert.That(objects.Samples, Has.Count.EqualTo(3));
            Assert.That(space.Samples, Has.Count.EqualTo(3));
            Assert.That(objects.Samples.Select(s => s.Values.Single()), Is.EqualTo(new[] { 1L, 1L, 1L }));
            Assert.That(space.Samples.Select(s => s.Values.Single()), Is.EqualTo(new[] { 16L, 1_048_576L, 24L }));
            // Same order, same stacks -- the two profiles are two measurements of the same event set.
            Assert.That(objects.Samples.Select(s => s.StackIndex), Is.EqualTo(space.Samples.Select(s => s.StackIndex)));
        });
    }

    [Test]
    public void Build_allocation_profiles_emitted_without_a_period_because_allocation_is_event_driven()
    {
        // Allocation sampling fires on AllocationTick, not on a timer -- there is no period to report, and the
        // allocation profiles must not be gated on periodNanos the way the time-valued cpu/off_cpu ones are.
        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 1234, 5678, "svc", periodNanos: 0,
            includeAgentCode: true, allocationSamples: new[] { Allocation("MyApp.Widget", 8UL) });

        var profiles = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles;
        Assert.Multiple(() =>
        {
            Assert.That(profiles.Select(p => TypeNameOf(req, p)), Is.EqualTo(new[] { "allocated_objects", "allocated_space" }));
            foreach (var p in profiles)
            {
                Assert.That(p.PeriodType, Is.Null, "allocation profiles must not carry a period_type");
                Assert.That(p.Period, Is.EqualTo(0L), "allocation profiles must not carry a period");
                Assert.That(p.TimeUnixNano, Is.EqualTo(1234UL));
                Assert.That(p.DurationNano, Is.EqualTo(5678UL));
            }
        });
    }

    [Test]
    public void Build_allocation_sample_link_round_trips_the_trace_and_span_ids()
    {
        var traceHigh = 0x1122334455667788L;
        var traceLow = 0x0102030405060708L;
        var spanId = unchecked((long)0xAABBCCDDEEFF0011UL);

        var allocation = new AllocationSample("alloc-thread", 42, traceHigh, traceLow, spanId,
            1_700_000_000_000L, 4096UL, "MyApp.Widget", new[] { "MyApp.Widget.Create()" });

        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: new[] { allocation });

        var objectsSample = ProfileOfType(req, "allocated_objects").Samples.Single();
        var spaceSample = ProfileOfType(req, "allocated_space").Samples.Single();

        var expectedTrace = new byte[]
        {
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08
        };
        var expectedSpan = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11 };

        Assert.Multiple(() =>
        {
            Assert.That(objectsSample.LinkIndex, Is.Not.EqualTo(0), "trace/span context is required for allocation samples");
            Assert.That(spaceSample.LinkIndex, Is.EqualTo(objectsSample.LinkIndex), "both profiles reference the same interned link");

            var link = req.Dictionary.LinkTable[objectsSample.LinkIndex];
            Assert.That(link.TraceId.ToByteArray(), Is.EqualTo(expectedTrace));
            Assert.That(link.SpanId.ToByteArray(), Is.EqualTo(expectedSpan));

            // index 0 zero-link sentinel + exactly one real link (interned once for both profiles).
            Assert.That(req.Dictionary.LinkTable, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Build_allocation_sample_without_trace_context_gets_the_zero_link_sentinel()
    {
        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: new[] { Allocation("MyApp.Widget", 8UL) });

        Assert.That(ProfileOfType(req, "allocated_objects").Samples.Single().LinkIndex, Is.EqualTo(0));
    }

    [Test]
    public void Build_allocation_sample_carries_type_name_thread_id_and_thread_name_attributes()
    {
        var allocation = new AllocationSample("alloc-worker-3", 4242, 0, 0, 0, 1_700_000_000_000L, 2048UL,
            "MyApp.Widget", new[] { "MyApp.Widget.Create()" });

        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: new[] { allocation });

        foreach (var sampleTypeName in new[] { "allocated_objects", "allocated_space" })
        {
            var attrs = AttributesOf(req, ProfileOfType(req, sampleTypeName).Samples.Single());
            Assert.Multiple(() =>
            {
                Assert.That(attrs["thread.id"].IntValue, Is.EqualTo(4242L), sampleTypeName);
                Assert.That(attrs["thread.name"].StringValue, Is.EqualTo("alloc-worker-3"), sampleTypeName);
                Assert.That(attrs["type.name"].StringValue, Is.EqualTo("MyApp.Widget"), sampleTypeName);
            });
        }
    }

    [Test]
    public void Build_allocation_sample_with_null_type_name_and_thread_name_interns_empty_strings()
    {
        var allocation = new AllocationSample(null, 1, 0, 0, 0, 0, 8UL, null, new[] { "MyApp.Widget.Create()" });

        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: new[] { allocation });

        var attrs = AttributesOf(req, ProfileOfType(req, "allocated_objects").Samples.Single());
        Assert.Multiple(() =>
        {
            Assert.That(attrs["thread.name"].StringValue, Is.EqualTo(string.Empty));
            Assert.That(attrs["type.name"].StringValue, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Build_repeated_allocations_of_the_same_shape_reuse_interned_attributes()
    {
        // Two allocations of the same type on the same thread with the same stack: attributes/stack interned once.
        var allocations = new[] { Allocation("MyApp.Widget", 16UL), Allocation("MyApp.Widget", 32UL) };

        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: allocations);

        var objects = ProfileOfType(req, "allocated_objects");
        Assert.Multiple(() =>
        {
            Assert.That(objects.Samples, Has.Count.EqualTo(2));
            Assert.That(objects.Samples[0].AttributeIndices, Is.EqualTo(objects.Samples[1].AttributeIndices));
            Assert.That(req.Dictionary.StackTable, Has.Count.EqualTo(2)); // zero value + one shared stack
            // zero value + profile.frame.type + thread.id + thread.name + type.name
            Assert.That(req.Dictionary.AttributeTable, Has.Count.EqualTo(5));
            // Values still differ per sample in allocated_space.
            Assert.That(ProfileOfType(req, "allocated_space").Samples.Select(s => s.Values.Single()),
                Is.EqualTo(new[] { 16L, 32L }));
        });
    }

    [Test]
    public void Build_saturates_an_allocated_size_larger_than_long_max_instead_of_wrapping_negative()
    {
        // Sample.values is int64; AllocatedSize is uint64. No real allocation is this large, but a
        // garbage/misparsed size must not be emitted as a negative byte count.
        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: new[] { Allocation("MyApp.Widget", ulong.MaxValue) });

        Assert.That(ProfileOfType(req, "allocated_space").Samples.Single().Values.Single(), Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void Build_with_null_allocation_samples_emits_only_cpu_profiles()
    {
        var req = OtlpProfileBuilder.Build(new[] { ThreadSample(onCpu: true) }, 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: null);

        Assert.That(req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles.Select(p => TypeNameOf(req, p)),
            Is.EqualTo(new[] { "cpu" }));
    }

    [Test]
    public void Build_with_empty_allocation_samples_emits_no_allocation_profiles()
    {
        // An empty Profile is rejected by the OTLP profiles ingest ("no_samples"), so nothing must be emitted.
        var req = OtlpProfileBuilder.Build(new[] { ThreadSample(onCpu: true) }, 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: new List<AllocationSample>());

        Assert.That(req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles.Select(p => TypeNameOf(req, p)),
            Is.EqualTo(new[] { "cpu" }));
    }

    [Test]
    public void Build_with_only_allocation_samples_and_no_thread_samples_emits_just_the_two_allocation_profiles()
    {
        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: false, allocationSamples: new[] { Allocation("MyApp.Widget", 8UL) });

        Assert.That(req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles.Select(p => TypeNameOf(req, p)),
            Is.EqualTo(new[] { "allocated_objects", "allocated_space" }));
    }

    [Test]
    public void Build_skips_an_allocation_sample_with_null_frames_rather_than_throwing()
    {
        // Defensive: a malformed sample must not take the whole drain's payload (thread samples included) down
        // with an exception. Only the malformed sample is dropped.
        var allocations = new[]
        {
            new AllocationSample("t", 1, 0, 0, 0, 0, 8UL, "MyApp.Widget", null),
            Allocation("MyApp.Widget", 16UL),
        };

        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: allocations);

        Assert.That(ProfileOfType(req, "allocated_objects").Samples, Has.Count.EqualTo(1));
    }

    [Test]
    public void Build_emits_no_allocation_profiles_when_every_allocation_sample_is_skipped()
    {
        // All inputs malformed -> nothing resolvable -> no empty Profile may be emitted.
        var allocations = new[] { new AllocationSample("t", 1, 0, 0, 0, 0, 8UL, "MyApp.Widget", null) };

        var req = OtlpProfileBuilder.Build(new[] { ThreadSample(onCpu: true) }, 0, 0, "svc", PeriodNanos,
            includeAgentCode: true, allocationSamples: allocations);

        Assert.That(req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles.Select(p => TypeNameOf(req, p)),
            Is.EqualTo(new[] { "cpu" }));
    }

    [Test]
    public void Build_allocation_frames_are_leaf_first_and_tagged_with_frame_type()
    {
        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: true,
            allocationSamples: new[] { Allocation("MyApp.Widget", 8UL, 0, "Leaf()", "Middle()", "Native.Function Call") });

        var dict = req.Dictionary;
        var stack = dict.StackTable[ProfileOfType(req, "allocated_objects").Samples.Single().StackIndex];
        var frameNames = stack.LocationIndices
            .Select(li => dict.LocationTable[li])
            .Select(loc => dict.StringTable[dict.FunctionTable[loc.Lines.Single().FunctionIndex].NameStrindex])
            .ToArray();

        string FrameTypeAt(int locationIndex) => dict.LocationTable[locationIndex].AttributeIndices
            .Select(ai => dict.AttributeTable[ai])
            .Where(kv => dict.StringTable[kv.KeyStrindex] == "profile.frame.type")
            .Select(kv => kv.Value.StringValue)
            .SingleOrDefault();

        Assert.Multiple(() =>
        {
            Assert.That(frameNames, Is.EqualTo(new[] { "Leaf()", "Middle()", "Native.Function Call" }));
            Assert.That(FrameTypeAt(stack.LocationIndices[0]), Is.EqualTo("dotnet"));
            Assert.That(FrameTypeAt(stack.LocationIndices[2]), Is.EqualTo("native"));
        });
    }

    [Test]
    public void Build_allocation_samples_are_not_subject_to_the_agent_code_filter()
    {
        // includeAgentCode governs the thread sampler's own-thread noise. Allocation samples are attributed to
        // the allocating call site and are reported as-is; this documents that intent.
        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 0, 0, "svc", PeriodNanos,
            includeAgentCode: false,
            allocationSamples: new[] { Allocation("System.Byte[]", 8UL, 0, "NewRelic.Agent.Core.Foo.Bar()") });

        Assert.That(ProfileOfType(req, "allocated_objects").Samples, Has.Count.EqualTo(1));
    }
}
