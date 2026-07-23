// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;
using OpenTelemetry.Proto.Profiles.V1Development;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class OtlpProfileBuilderTests
{
    private static ManagedThreadSample Sample(string name, long span, params string[] frames) =>
        new ManagedThreadSample(name, 1, 0, 0, span, frames, onCpu: false);

    [Test]
    public void Build_emits_one_sample_per_input_and_zero_index_empty_string()
    {
        var req = OtlpProfileBuilder.Build(
            new[] { Sample("t1", 0, "A()", "B()"), Sample("t2", 0, "C()") },
            startUnixNano: 1000, durationNano: 10_000_000_000, serviceName: "svc", periodNanos: 1_000_000L);

        var dict = req.Dictionary; // ProfilesDictionary
        Assert.That(dict.StringTable[0], Is.EqualTo("")); // index-0 invariant
        // Sample() helper is off-CPU, so both land in off_cpu (profiles[0]).
        var profile = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0];
        Assert.That(profile.Samples, Has.Count.EqualTo(2));
    }

    [Test]
    public void Build_drops_agent_own_thread_samples_when_includeAgentCode_false()
    {
        var samples = new[]
        {
            Sample("agent", 0, "NewRelic.Agent.Core.DataTransport.ConnectionManager.Connect()"),
            Sample("app", 0, "MyApp.Work()"),
        };

        var req = OtlpProfileBuilder.Build(samples, 1000, 1, "svc", periodNanos: 1_000_000L, includeAgentCode: false);

        var dict = req.Dictionary;
        // Sample() helper is off-CPU, so the surviving sample lands in off_cpu (profiles[0]).
        var profile = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0];
        Assert.Multiple(() =>
        {
            Assert.That(profile.Samples, Has.Count.EqualTo(1), "The agent-own-thread sample should be dropped.");
            Assert.That(dict.StringTable.Any(s => s.StartsWith("NewRelic.")), Is.False, "No agent frames should be interned.");
            Assert.That(dict.StringTable.Any(s => s.Contains("MyApp.Work")), Is.True, "The customer frame should survive.");
        });
    }

    [Test]
    public void Build_keeps_non_core_NewRelic_frames_when_includeAgentCode_false()
    {
        // Only agent-CORE frames mark an agent-own thread. The public API (NewRelic.Api.Agent.*) and the
        // integration-test dispatcher (NewRelic.Agent.IntegrationTests.*) sit on CUSTOMER stacks and must NOT
        // be dropped -- a broad "NewRelic." match wrongly discarded the correlated sample in the host tests.
        var samples = new[]
        {
            Sample("api", 0, "Customer.Work()", "NewRelic.Api.Agent.NewRelic.AddCustomAttribute(System.String, System.String)"),
            Sample("harness", 0, "Customer.Work()", "NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers.DynamicMethodExecutor.Execute()"),
            Sample("agent", 0, "NewRelic.Agent.Core.DataTransport.ConnectionManager.Connect()"),
        };

        var req = OtlpProfileBuilder.Build(samples, 1000, 1, "svc", periodNanos: 1_000_000L, includeAgentCode: false);

        // Sample() helper is off-CPU, so the surviving samples land in off_cpu (profiles[0]).
        var profile = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0];
        Assert.Multiple(() =>
        {
            Assert.That(profile.Samples, Has.Count.EqualTo(2), "Only the agent-core sample should be dropped.");
            Assert.That(req.Dictionary.StringTable.Any(s => s.Contains("NewRelic.Api.Agent")), Is.True, "Public-API frame must survive.");
            Assert.That(req.Dictionary.StringTable.Any(s => s.Contains("IntegrationTests")), Is.True, "Test-harness frame must survive.");
            Assert.That(req.Dictionary.StringTable.Any(s => s.StartsWith("NewRelic.Agent.Core.")), Is.False, "Agent-core frame must be dropped.");
        });
    }

    [Test]
    public void Build_keeps_agent_samples_when_includeAgentCode_true()
    {
        var samples = new[]
        {
            Sample("agent", 0, "NewRelic.Agent.Core.X.Y()"),
            Sample("app", 0, "MyApp.Work()"),
        };

        var req = OtlpProfileBuilder.Build(samples, 1000, 1, "svc", periodNanos: 1_000_000L, includeAgentCode: true);

        // Sample() helper is off-CPU, so both samples land in off_cpu (profiles[0]).
        var profile = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0];
        Assert.Multiple(() =>
        {
            Assert.That(profile.Samples, Has.Count.EqualTo(2));
            Assert.That(req.Dictionary.StringTable.Any(s => s == "NewRelic.Agent.Core.X.Y()"), Is.True);
        });
    }

    [Test]
    public void Build_distinct_span_contexts_produce_distinct_links()
    {
        var req = OtlpProfileBuilder.Build(
            new[] { Sample("t1", 0xAA, "A()"), Sample("t2", 0xBB, "A()") },
            1000, 10_000_000_000, "svc");
        Assert.That(req.Dictionary.LinkTable.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void Build_functions_are_name_only_no_filename_or_line()
    {
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "My.Frame()") }, 1000, 1, "svc");
        var fn = req.Dictionary.FunctionTable.First(f => f.NameStrindex != 0);
        Assert.That(fn.FilenameStrindex, Is.EqualTo(0)); // no file path
        Assert.That(fn.SystemNameStrindex, Is.EqualTo(0));
        Assert.That(fn.StartLine, Is.EqualTo(0));
    }

    [Test]
    public void Build_empty_samples_yields_no_profiles()
    {
        // Both partition sides are empty, so neither profile is emitted. An empty Profile message is
        // rejected by the OTLP profiles ingest as "no_samples", so we must not emit one.
        var req = OtlpProfileBuilder.Build(new List<ManagedThreadSample>(), 1000, 1, "svc", periodNanos: 1_000_000L);
        var profiles = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles;
        Assert.That(profiles, Is.Empty);
    }

    [Test]
    public void Build_all_off_cpu_emits_only_off_cpu_profile_no_empty_cpu()
    {
        // A sweep with nothing on-CPU must emit ONLY the off_cpu profile -- never an empty cpu profile,
        // which the OTLP profiles ingest drops as "no_samples".
        var samples = new[]
        {
            new ManagedThreadSample("off1", 1, 0, 0, 0, new[] { "A.b" }, onCpu: false),
            new ManagedThreadSample("off2", 2, 0, 0, 0, new[] { "C.d" }, onCpu: false),
        };
        var req = OtlpProfileBuilder.Build(samples, 0, 0, "svc", periodNanos: 1_000_000L, includeAgentCode: true);
        var p = req.ResourceProfiles[0].ScopeProfiles[0].Profiles;

        Assert.Multiple(() =>
        {
            Assert.That(p, Has.Count.EqualTo(1));
            Assert.That(req.Dictionary.StringTable[(int)p[0].SampleType.TypeStrindex], Is.EqualTo("off_cpu"));
            Assert.That(p[0].Samples, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Build_all_on_cpu_emits_only_cpu_profile_no_empty_off_cpu()
    {
        // A sweep with everything on-CPU emits ONLY the cpu profile. With off_cpu empty and skipped, cpu is
        // the sole profile at index 0 -- never an empty off_cpu profile.
        var samples = new[]
        {
            new ManagedThreadSample("on1", 1, 0, 0, 0, new[] { "A.b" }, onCpu: true),
            new ManagedThreadSample("on2", 2, 0, 0, 0, new[] { "C.d" }, onCpu: true),
        };
        var req = OtlpProfileBuilder.Build(samples, 0, 0, "svc", periodNanos: 1_000_000L, includeAgentCode: true);
        var p = req.ResourceProfiles[0].ScopeProfiles[0].Profiles;

        Assert.Multiple(() =>
        {
            Assert.That(p, Has.Count.EqualTo(1));
            Assert.That(req.Dictionary.StringTable[(int)p[0].SampleType.TypeStrindex], Is.EqualTo("cpu"));
            Assert.That(p[0].Samples, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Build_all_dictionary_tables_have_zero_value_at_index_zero()
    {
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "A()") }, 1000, 1, "svc");
        var dict = req.Dictionary;

        Assert.Multiple(() =>
        {
            Assert.That(dict.StringTable[0], Is.EqualTo(""));
            Assert.That(dict.FunctionTable[0].NameStrindex, Is.EqualTo(0));
            Assert.That(dict.LocationTable[0].Lines, Is.Empty);
            Assert.That(dict.StackTable[0].LocationIndices, Is.Empty);
            Assert.That(dict.AttributeTable[0].KeyStrindex, Is.EqualTo(0));
            Assert.That(dict.LinkTable[0].TraceId.Length, Is.EqualTo(16));
            Assert.That(dict.LinkTable[0].SpanId.Length, Is.EqualTo(8));
        });
    }

    [Test]
    public void Build_sets_profile_metadata_sample_type_time_and_duration()
    {
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "A()") }, 1234, 5678, "svc", periodNanos: 1_000_000L);
        var dict = req.Dictionary;
        // Sample() helper is off-CPU, so it lands in off_cpu (profiles[0]).
        var profile = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0];

        Assert.Multiple(() =>
        {
            Assert.That(profile.TimeUnixNano, Is.EqualTo(1234UL));
            Assert.That(profile.DurationNano, Is.EqualTo(5678UL));
            Assert.That(dict.StringTable[profile.SampleType.TypeStrindex], Is.EqualTo("off_cpu"));
            Assert.That(dict.StringTable[profile.SampleType.UnitStrindex], Is.EqualTo("nanoseconds"));
        });
    }

    [Test]
    public void Build_sets_period_type_and_period_when_interval_provided()
    {
        // periodNanos = 10 s sampling interval in nanoseconds. With a period supplied, Build emits two
        // profiles (off_cpu/cpu); [0] is off_cpu.
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "A()") }, 1234, 5678, "svc", periodNanos: 10_000_000_000L);
        var dict = req.Dictionary;
        var profile = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0];

        Assert.Multiple(() =>
        {
            Assert.That(profile.PeriodType, Is.Not.Null);
            Assert.That(dict.StringTable[profile.PeriodType.TypeStrindex], Is.EqualTo("cpu"));
            Assert.That(dict.StringTable[profile.PeriodType.UnitStrindex], Is.EqualTo("nanoseconds"));
            Assert.That(profile.Period, Is.EqualTo(10_000_000_000L));
        });
    }

    // profile.frame.type (OTel semconv, reading-2/per-frame): managed .NET frames -> "dotnet"; the synthetic
    // native thread-entry boundary frame -> "native".
    [Test]
    public void Build_tags_managed_frame_with_frame_type_dotnet()
    {
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "My.Managed.Method()") }, 1000, 1, "svc");
        Assert.That(FrameType(req, "My.Managed.Method()"), Is.EqualTo("dotnet"));
    }

    [Test]
    public void Build_tags_native_boundary_frame_with_frame_type_native()
    {
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "Native.Function Call") }, 1000, 1, "svc");
        Assert.That(FrameType(req, "Native.Function Call"), Is.EqualTo("native"));
    }

    [Test]
    public void Build_tags_unresolved_managed_frame_dotnet_not_native()
    {
        // A real-but-unresolved managed frame (e.g. a DynamicMethod, rendered UnknownMethod(<id>)) is still a
        // .NET frame -> "dotnet". Only the exact native boundary label is "native".
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "UnknownClass.UnknownMethod(12345)") }, 1000, 1, "svc");
        Assert.That(FrameType(req, "UnknownClass.UnknownMethod(12345)"), Is.EqualTo("dotnet"));
    }

    // Resolve the profile.frame.type attribute value for the location of a given frame name.
    private static string FrameType(OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest req, string frameName)
    {
        var dict = req.Dictionary;
        var fnIdx = -1;
        for (var i = 0; i < dict.FunctionTable.Count; i++)
            if (dict.StringTable[dict.FunctionTable[i].NameStrindex] == frameName) { fnIdx = i; break; }
        Assert.That(fnIdx, Is.GreaterThan(0), $"function '{frameName}' not found");

        var loc = dict.LocationTable.First(l => l.Lines.Count > 0 && l.Lines[0].FunctionIndex == fnIdx);
        foreach (var ai in loc.AttributeIndices)
        {
            var kv = dict.AttributeTable[ai];
            if (dict.StringTable[kv.KeyStrindex] == "profile.frame.type")
                return kv.Value.StringValue;
        }
        return null;
    }

    [Test]
    public void Build_each_sample_has_period_value_and_leaf_first_stack()
    {
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "A()", "B()") }, 1000, 1, "svc", periodNanos: 1_000_000L);
        var dict = req.Dictionary;
        // Sample() helper is off-CPU, so it lands in off_cpu (profiles[0]); value = periodNanos.
        var sample = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0].Samples.Single();

        Assert.That(sample.Values.ToArray(), Is.EqualTo(new long[] { 1_000_000L }));

        var stack = dict.StackTable[sample.StackIndex];
        var frameNames = stack.LocationIndices
            .Select(li => dict.LocationTable[li])
            .Select(loc => dict.StringTable[dict.FunctionTable[loc.Lines.Single().FunctionIndex].NameStrindex])
            .ToArray();
        Assert.That(frameNames, Is.EqualTo(new[] { "A()", "B()" })); // leaf-first preserved
    }

    [Test]
    public void Build_sample_carries_thread_id_and_thread_name_attributes()
    {
        var req = OtlpProfileBuilder.Build(
            new[] { new ManagedThreadSample("worker-7", 42, 0, 0, 0, new[] { "A()" }, onCpu: false) },
            1000, 1, "svc", periodNanos: 1_000_000L);
        var dict = req.Dictionary;
        // onCpu: false, so it lands in off_cpu (profiles[0]).
        var sample = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0].Samples.Single();

        var attrs = sample.AttributeIndices
            .Select(i => dict.AttributeTable[i])
            .ToDictionary(kv => dict.StringTable[kv.KeyStrindex], kv => kv.Value);

        Assert.Multiple(() =>
        {
            Assert.That(attrs.ContainsKey("thread.id"));
            Assert.That(attrs["thread.id"].IntValue, Is.EqualTo(42));
            Assert.That(attrs.ContainsKey("thread.name"));
            Assert.That(attrs["thread.name"].StringValue, Is.EqualTo("worker-7"));
        });
    }

    [Test]
    public void Build_link_encodes_trace_and_span_ids()
    {
        var req = OtlpProfileBuilder.Build(
            new[] { new ManagedThreadSample("t1", 1, 0x1122334455667788L, 0x0102030405060708L, unchecked((long)0xAABBCCDDEEFF0011UL), new[] { "A()" }, onCpu: false) },
            1000, 1, "svc", periodNanos: 1_000_000L);
        var dict = req.Dictionary;
        // onCpu: false, so it lands in off_cpu (profiles[0]).
        var sample = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0].Samples.Single();

        Assert.That(sample.LinkIndex, Is.Not.EqualTo(0));
        var link = dict.LinkTable[sample.LinkIndex];

        var expectedTrace = new byte[]
        {
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08
        };
        var expectedSpan = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11 };

        Assert.Multiple(() =>
        {
            Assert.That(link.TraceId.ToByteArray(), Is.EqualTo(expectedTrace));
            Assert.That(link.SpanId.ToByteArray(), Is.EqualTo(expectedSpan));
        });
    }

    [Test]
    public void Build_zero_context_sample_has_no_link()
    {
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "A()") }, 1000, 1, "svc", periodNanos: 1_000_000L);
        // Sample() helper is off-CPU, so it lands in off_cpu (profiles[0]).
        var sample = req.ResourceProfiles.Single().ScopeProfiles.Single().Profiles[0].Samples.Single();
        Assert.That(sample.LinkIndex, Is.EqualTo(0)); // 0 == no link per proto
    }

    [Test]
    public void Build_interns_reused_strings_functions_locations_and_stacks()
    {
        // Two samples with the identical stack should reuse function/location/stack table entries.
        var req = OtlpProfileBuilder.Build(
            new[] { Sample("t1", 0, "A()", "B()"), Sample("t1", 0, "A()", "B()") },
            1000, 1, "svc");
        var dict = req.Dictionary;

        // index 0 (zero) + A() + B()
        Assert.That(dict.FunctionTable, Has.Count.EqualTo(3));
        Assert.That(dict.LocationTable, Has.Count.EqualTo(3));
        // index 0 (zero) + one interned stack
        Assert.That(dict.StackTable, Has.Count.EqualTo(2));
    }

    [Test]
    public void Build_interns_reused_link_across_samples()
    {
        var req = OtlpProfileBuilder.Build(
            new[]
            {
                new ManagedThreadSample("t1", 1, 1, 2, 3, new[] { "A()" }, onCpu: false),
                new ManagedThreadSample("t2", 1, 1, 2, 3, new[] { "B()" }, onCpu: false)
            },
            1000, 1, "svc");
        // index 0 (zero) + one shared link (same trace/span context)
        Assert.That(req.Dictionary.LinkTable, Has.Count.EqualTo(2));
    }

    [Test]
    public void Build_shared_frame_across_different_stacks_reuses_function_and_location()
    {
        var req = OtlpProfileBuilder.Build(
            new[] { Sample("t1", 0, "Shared()", "A()"), Sample("t2", 0, "Shared()", "B()") },
            1000, 1, "svc");
        var dict = req.Dictionary;

        // index 0 + Shared() + A() + B()
        Assert.That(dict.FunctionTable, Has.Count.EqualTo(4));
        Assert.That(dict.LocationTable, Has.Count.EqualTo(4));
        // index 0 + two distinct stacks
        Assert.That(dict.StackTable, Has.Count.EqualTo(3));
    }

    [Test]
    public void Build_sets_service_name_resource_attribute_and_scope()
    {
        var req = OtlpProfileBuilder.Build(new[] { Sample("t1", 0, "A()") }, 1000, 1, "my-service");
        var resourceProfiles = req.ResourceProfiles.Single();

        var serviceName = resourceProfiles.Resource.Attributes
            .Single(a => a.Key == "service.name").Value.StringValue;
        Assert.That(serviceName, Is.EqualTo("my-service"));

        var scope = resourceProfiles.ScopeProfiles.Single().Scope;
        Assert.That(scope.Name, Is.Not.Empty);
    }

    [Test]
    public void Build_withPeriod_emitsTwoProfiles_offCpuThenCpu()
    {
        var samples = new[]
        {
            new ManagedThreadSample("on", 1, 0, 0, 0, new[] { "A.b" }, onCpu: true),
            new ManagedThreadSample("off", 2, 0, 0, 0, new[] { "C.d" }, onCpu: false),
        };
        var req = OtlpProfileBuilder.Build(samples, 0, 0, "svc", periodNanos: 1_000_000L, includeAgentCode: true);
        var p = req.ResourceProfiles[0].ScopeProfiles[0].Profiles;
        string TypeName(Profile x) => req.Dictionary.StringTable[(int)x.SampleType.TypeStrindex];

        Assert.That(p, Has.Count.EqualTo(2));
        Assert.That(p.Select(TypeName), Does.Not.Contain("samples")); // legacy dropped
        Assert.That(TypeName(p[0]), Is.EqualTo("off_cpu"));
        Assert.That(p[0].Samples, Has.Count.EqualTo(1)); // parked
        Assert.That(p[0].Samples[0].Values[0], Is.EqualTo(1_000_000L));
        Assert.That(TypeName(p[1]), Is.EqualTo("cpu"));
        Assert.That(p[1].Samples, Has.Count.EqualTo(1)); // on-CPU
        Assert.That(p[1].Samples[0].Values[0], Is.EqualTo(1_000_000L));
    }

    [Test]
    public void Build_cpuAndOffCpu_partitionAllIncludedSamples()
    {
        var samples = new[]
        {
            new ManagedThreadSample("on1", 1, 0, 0, 0, new[] { "A.b" }, onCpu: true),
            new ManagedThreadSample("off1", 2, 0, 0, 0, new[] { "C.d" }, onCpu: false),
            new ManagedThreadSample("off2", 3, 0, 0, 0, new[] { "E.f" }, onCpu: false),
        };
        var req = OtlpProfileBuilder.Build(samples, 0, 0, "svc", periodNanos: 1_000_000L, includeAgentCode: true);
        var p = req.ResourceProfiles[0].ScopeProfiles[0].Profiles;
        // off_cpu[0] + cpu[1] == the number of included (agent-filtered) input samples, no overlap.
        Assert.That(p[0].Samples.Count + p[1].Samples.Count, Is.EqualTo(samples.Length));
        Assert.That(p[0].Samples, Has.Count.EqualTo(2)); // off
        Assert.That(p[1].Samples, Has.Count.EqualTo(1)); // on
    }

    [Test]
    public void Build_withoutPeriod_emitsNoProfiles()
    {
        var samples = new[] { new ManagedThreadSample("on", 1, 0, 0, 0, new[] { "A.b" }, onCpu: true) };
        var req = OtlpProfileBuilder.Build(samples, 0, 0, "svc", periodNanos: 0, includeAgentCode: true);
        Assert.That(req.ResourceProfiles[0].ScopeProfiles[0].Profiles, Is.Empty);
    }

    [Test]
    public void Build_sharesDictionary_internsStackOnce()
    {
        // Same frame across two samples -> exactly one stack whether referenced by 1 or 2 profiles.
        var samples = new[]
        {
            new ManagedThreadSample("on", 1, 0, 0, 0, new[] { "A.b" }, onCpu: true),
            new ManagedThreadSample("on2", 2, 0, 0, 0, new[] { "A.b" }, onCpu: true),
        };
        var req = OtlpProfileBuilder.Build(samples, 0, 0, "svc", periodNanos: 1_000_000L, includeAgentCode: true);
        // StackTable[0] zero value + 1 interned stack for "A.b".
        Assert.That(req.Dictionary.StackTable, Has.Count.EqualTo(2));
    }

    [Test]
    public void Build_excludeAgentCode_appliesToBothProfiles()
    {
        // Mix of on/off-CPU non-agent samples plus one agent-own-thread sample (on-CPU, so it would otherwise
        // land in cpu[1]) -- the agent sample must be dropped from every profile, including cpu.
        var samples = new[]
        {
            new ManagedThreadSample("agent", 1, 0, 0, 0, new[] { "NewRelic.Agent.Core.Foo.Bar" }, onCpu: true),
            new ManagedThreadSample("app-on", 2, 0, 0, 0, new[] { "App.Work" }, onCpu: true),
            new ManagedThreadSample("app-off", 3, 0, 0, 0, new[] { "App.Idle" }, onCpu: false),
        };
        var req = OtlpProfileBuilder.Build(samples, 0, 0, "svc", periodNanos: 1_000_000L, includeAgentCode: false);
        var profiles = req.ResourceProfiles[0].ScopeProfiles[0].Profiles;
        Assert.That(profiles, Has.Count.EqualTo(2));
        Assert.That(profiles[0].Samples, Has.Count.EqualTo(1)); // off_cpu: agent dropped, only app-off remains
        Assert.That(profiles[1].Samples, Has.Count.EqualTo(1)); // cpu: agent dropped, only app-on remains
    }
}
