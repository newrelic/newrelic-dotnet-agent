// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Profiles.V1Development;
using OpenTelemetry.Proto.Resource.V1;
using ProtoValueType = OpenTelemetry.Proto.Profiles.V1Development.ValueType;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Maps collected <see cref="ManagedThreadSample"/>s into an OTLP
/// <see cref="ExportProfilesServiceRequest"/>. Strings, functions, locations, stacks,
/// attributes, and links are interned into the shared <see cref="ProfilesDictionary"/>
/// tables (index 0 of every table is the zero value per the OTLP spec).
/// </summary>
public static class OtlpProfileBuilder
{
    private const string ScopeName = "newrelic.dotnet";
    private const string ServiceNameKey = "service.name";
    private const string ThreadIdKey = "thread.id";
    private const string ThreadNameKey = "thread.name";

    // OTLP-idiomatic on/off-CPU split: profiles.proto documents ONLY `cpu`/`off_cpu`/`allocated_objects`/
    // `allocated_space` as sample types (:291-295, :317) -- no Google-pprof-style "samples:count" convention.
    // `Profile.sample_type` is singular, so cpu + off_cpu are two separate Profile messages that SHARE this
    // request's ProfilesDictionary (stacks/strings interned once). The two partition the threads: cpu = on-CPU
    // samples, off_cpu = parked samples.
    private const string OffCpuSampleTypeName = "off_cpu";
    private const string CpuSampleTypeName = "cpu";
    private const string NanosecondsUnit = "nanoseconds"; // == PeriodTypeUnit

    // period_type describes the cadence of periodic sampling. We take an all-thread stack snapshot every
    // configured interval, so period = interval in nanoseconds. Type "cpu" / unit "nanoseconds" mirrors the
    // OpenTelemetry .NET auto-instrumentation continuous profiler's own output for the same style of
    // timer-driven all-thread sampler (kept for OTLP consumer parity).
    private const string PeriodTypeName = "cpu";
    private const string PeriodTypeUnit = "nanoseconds";

    // profile.frame.type (OTel profiles semantic conventions): per-frame origin. Our managed stack walk
    // yields .NET frames ("dotnet") except the single synthetic native thread-entry boundary frame the
    // native sampler emits for functionId == 0, which is the one unmanaged frame ("native"). See
    // https://opentelemetry.io/docs/specs/semconv/registry/attributes/profile/.
    private const string FrameTypeKey = "profile.frame.type";
    private const string FrameTypeDotnet = "dotnet";
    private const string FrameTypeNative = "native";

    // MUST match the native thread-entry label produced for functionId == 0 in
    // Profiler/ContinuousProfiler/ContinuousProfiler.h. A change there without a matching change here
    // silently mis-tags that frame as "dotnet".
    private const string NativeFrameName = "Native.Function Call";

    // The agent's own background threads (connect/harvest/scheduler/CP-drain) run agent-core code, so their
    // stacks contain frames under "NewRelic.Agent.Core.". A sample with such a frame was taken on one of the
    // agent's OWN threads -- a customer thread executing instrumented code does NOT have agent-core frames on
    // its stack mid-method (the tracer runs before/after, not during). Used to drop agent-self samples unless
    // includeAgentCode.
    //
    // Deliberately Core-specific, NOT a broad "NewRelic." match: (1) "NewRelic.Api.Agent." is the public API a
    // customer thread may legitimately be executing (keep it); (2) "NewRelic.Providers." wrappers sit on
    // customer threads during instrumentation (keep them); (3) the integration-test harness dispatches
    // exercisers via "NewRelic.Agent.IntegrationTests.*", which a broad match wrongly dropped -- taking the
    // correlated sample with it. The agent's own threads are always rooted in Core, so this still catches them.
    private const string AgentFramePrefix = "NewRelic.Agent.Core.";

    // includeAgentCode: when false, samples taken on the agent's own threads (any frame under "NewRelic.") are
    // dropped so the profile carries only the customer application. Defaults to true (no filtering) for
    // callers/tests that don't care; the CP service passes the configured value, which defaults to false.
    public static ExportProfilesServiceRequest Build(IReadOnlyList<ManagedThreadSample> samples, long startUnixNano, long durationNano, string serviceName, long periodNanos = 0, bool includeAgentCode = true)
    {
        var dictionary = new ProfilesDictionary();

        // Interning caches. Every table reserves index 0 for its zero value.
        var stringTable = new Dictionary<string, int>();
        var functionTable = new Dictionary<string, int>();
        var locationTable = new Dictionary<string, int>();
        var stackTable = new Dictionary<string, int>();
        var attributeTable = new Dictionary<(int keyStrindex, long intValue, string stringValue), int>();
        var linkTable = new Dictionary<(long high, long low, long span), int>();

        // string_table[0] == "".
        InternString(dictionary, stringTable, string.Empty);

        // mapping_table[0], location_table[0], function_table[0], link_table[0],
        // attribute_table[0], stack_table[0] MUST all be the zero value.
        dictionary.MappingTable.Add(new Mapping());
        dictionary.FunctionTable.Add(new Function());
        dictionary.LocationTable.Add(new Location());
        dictionary.StackTable.Add(new Stack());
        dictionary.AttributeTable.Add(new KeyValueAndUnit());

        // link_table[0] is the reserved "no linked span" sentinel and is REQUIRED by the OTLP profiles
        // spec: profiles.proto states `link_table[0] MUST be the zero value (Link{}) and present`, and
        // `Sample.link_index == 0 means no link exists`. So EVERY sample that was NOT captured during a
        // live transaction/span points at this index-0 entry -- expect the vast majority of samples to have
        // link_index 0 and this all-zero link to dominate the table. It is NOT garbage or a correlation
        // failure; real correlations appear as ADDITIONAL entries (index >= 1) via InternLink below. The
        // 16/8 zeroed byte arrays are the spec-RECOMMENDED form (better codec compatibility than empty
        // byte strings). Do not remove it, and do not read its presence as "trace/span data is missing".
        dictionary.LinkTable.Add(new Link
        {
            TraceId = ByteString.CopyFrom(new byte[16]),
            SpanId = ByteString.CopyFrom(new byte[8]),
        });

        // Resolve every included sample's shared-dictionary indices ONCE. All emitted profiles reference these.
        var resolved = new List<ResolvedSample>(samples.Count);
        foreach (var sample in samples)
        {
            // Drop the agent's own-thread samples unless explicitly included (default). See AgentFramePrefix.
            if (!includeAgentCode && IsAgentThreadSample(sample.Frames))
                continue;

            var stackIndex = InternStack(dictionary, stringTable, functionTable, locationTable, stackTable, attributeTable, sample.Frames);
            var threadIdAttr = InternAttribute(dictionary, stringTable, attributeTable, ThreadIdKey, new AnyValue { IntValue = sample.OsThreadId });
            var threadNameAttr = InternAttribute(dictionary, stringTable, attributeTable, ThreadNameKey, new AnyValue { StringValue = sample.ThreadName ?? string.Empty });
            var linkIndex = InternLink(dictionary, linkTable, sample.TraceIdHigh, sample.TraceIdLow, sample.SpanId);
            resolved.Add(new ResolvedSample(stackIndex, threadIdAttr, threadNameAttr, linkIndex, sample.OnCpu));
        }

        var scopeProfiles = new ScopeProfiles
        {
            Scope = new InstrumentationScope
            {
                Name = ScopeName,
                Version = AgentInstallConfiguration.AgentVersion ?? string.Empty,
            },
        };

        // off_cpu + cpu in nanoseconds, only when a real interval is known (mirror period_type gating).
        // No period -> nothing meaningful to emit (both sample types are time-valued), so zero profiles.
        //
        // Emit a profile ONLY for a non-empty partition side. A partition half is legitimately empty on a
        // sweep that caught nothing on-CPU (common on a parked-heavy app) or nothing off-CPU -- but the OTLP
        // profiles ingest rejects a Profile carrying zero samples ("no_samples" drop), so an empty side must
        // not be emitted. Order stays off_cpu-then-cpu when both are present; when off_cpu is empty, cpu is
        // the sole profile at index 0.
        if (periodNanos > 0)
        {
            var anyOffCpu = false;
            var anyOnCpu = false;
            foreach (var r in resolved)
            {
                if (r.OnCpu) anyOnCpu = true; else anyOffCpu = true;
                if (anyOffCpu && anyOnCpu) break;
            }

            // off_cpu:nanoseconds -- parked (off-CPU) threads only; value = off-CPU time attributed this sweep.
            if (anyOffCpu)
                scopeProfiles.Profiles.Add(BuildProfile(dictionary, stringTable, startUnixNano, durationNano, periodNanos,
                    OffCpuSampleTypeName, NanosecondsUnit, resolved, valueForSample: _ => periodNanos, includeSample: r => !r.OnCpu));

            // cpu:nanoseconds -- on-CPU threads only.
            if (anyOnCpu)
                scopeProfiles.Profiles.Add(BuildProfile(dictionary, stringTable, startUnixNano, durationNano, periodNanos,
                    CpuSampleTypeName, NanosecondsUnit, resolved, valueForSample: _ => periodNanos, includeSample: r => r.OnCpu));
        }

        var resourceProfiles = new ResourceProfiles
        {
            Resource = new Resource(),
        };
        resourceProfiles.Resource.Attributes.Add(new KeyValue
        {
            Key = ServiceNameKey,
            Value = new AnyValue { StringValue = serviceName ?? string.Empty },
        });
        resourceProfiles.ScopeProfiles.Add(scopeProfiles);

        var request = new ExportProfilesServiceRequest { Dictionary = dictionary };
        request.ResourceProfiles.Add(resourceProfiles);
        return request;
    }

    // A sample is "the agent's own" when any frame is under the NewRelic.* namespace. Customer application
    // stacks never carry agent frames mid-method, so this cleanly identifies the agent's connect / harvest /
    // scheduler / CP-worker threads (validated against real captures: no stack mixed customer + agent frames).
    private static bool IsAgentThreadSample(IReadOnlyList<string> frames)
    {
        foreach (var frame in frames)
        {
            if (frame != null && frame.StartsWith(AgentFramePrefix, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    // A single sample's shared-dictionary indices, resolved once and reused by every emitted profile.
    private readonly struct ResolvedSample
    {
        public readonly int StackIndex;
        public readonly int ThreadIdAttr;
        public readonly int ThreadNameAttr;
        public readonly int LinkIndex;
        public readonly bool OnCpu;

        public ResolvedSample(int stackIndex, int threadIdAttr, int threadNameAttr, int linkIndex, bool onCpu)
        {
            StackIndex = stackIndex;
            ThreadIdAttr = threadIdAttr;
            ThreadNameAttr = threadNameAttr;
            LinkIndex = linkIndex;
            OnCpu = onCpu;
        }
    }

    // Emits one Profile (off_cpu:nanoseconds or cpu:nanoseconds) from the already-resolved samples, applying
    // a per-profile value and inclusion filter. All emitted profiles share the caller's dictionary/interning
    // caches -- no re-interning happens here.
    private static Profile BuildProfile(ProfilesDictionary dictionary, Dictionary<string, int> stringTable,
        long startUnixNano, long durationNano, long periodNanos, string sampleTypeName, string sampleTypeUnit,
        List<ResolvedSample> resolved, System.Func<ResolvedSample, long> valueForSample, System.Func<ResolvedSample, bool> includeSample)
    {
        var profile = new Profile
        {
            TimeUnixNano = (ulong)startUnixNano,
            DurationNano = (ulong)durationNano,
            SampleType = new ProtoValueType
            {
                TypeStrindex = InternString(dictionary, stringTable, sampleTypeName),
                UnitStrindex = InternString(dictionary, stringTable, sampleTypeUnit),
            },
        };

        // period_type / period are informational (proto: "do not affect interpretation of results"), so only
        // emit them when a real sampling interval was supplied; otherwise leave both unset.
        if (periodNanos > 0)
        {
            profile.PeriodType = new ProtoValueType
            {
                TypeStrindex = InternString(dictionary, stringTable, PeriodTypeName),
                UnitStrindex = InternString(dictionary, stringTable, PeriodTypeUnit),
            };
            profile.Period = periodNanos;
        }

        foreach (var r in resolved)
        {
            if (!includeSample(r))
                continue;

            var protoSample = new Sample { StackIndex = r.StackIndex, LinkIndex = r.LinkIndex };
            protoSample.Values.Add(valueForSample(r));
            protoSample.AttributeIndices.Add(r.ThreadIdAttr);
            protoSample.AttributeIndices.Add(r.ThreadNameAttr);
            profile.Samples.Add(protoSample);
        }

        return profile;
    }

    private static int InternString(ProfilesDictionary dictionary, Dictionary<string, int> cache, string value)
    {
        value ??= string.Empty;
        if (cache.TryGetValue(value, out var index))
            return index;

        index = dictionary.StringTable.Count;
        dictionary.StringTable.Add(value);
        cache[value] = index;
        return index;
    }

    private static int InternFunction(ProfilesDictionary dictionary, Dictionary<string, int> stringCache, Dictionary<string, int> functionCache, string frameName)
    {
        if (functionCache.TryGetValue(frameName, out var index))
            return index;

        // Name-only function: filename/system-name/start-line all left at their zero values.
        var function = new Function { NameStrindex = InternString(dictionary, stringCache, frameName) };
        index = dictionary.FunctionTable.Count;
        dictionary.FunctionTable.Add(function);
        functionCache[frameName] = index;
        return index;
    }

    private static int InternLocation(ProfilesDictionary dictionary, Dictionary<string, int> stringCache, Dictionary<string, int> functionCache, Dictionary<string, int> locationCache, Dictionary<(int, long, string), int> attributeCache, string frameName)
    {
        if (locationCache.TryGetValue(frameName, out var index))
            return index;

        var functionIndex = InternFunction(dictionary, stringCache, functionCache, frameName);
        var location = new Location();
        location.Lines.Add(new Line { FunctionIndex = functionIndex });

        // Tag the frame's origin (profile.frame.type). Everything the managed walk names is a .NET frame;
        // only the synthetic native thread-entry boundary frame is "native".
        var frameType = frameName == NativeFrameName ? FrameTypeNative : FrameTypeDotnet;
        location.AttributeIndices.Add(InternAttribute(dictionary, stringCache, attributeCache, FrameTypeKey, new AnyValue { StringValue = frameType }));

        index = dictionary.LocationTable.Count;
        dictionary.LocationTable.Add(location);
        locationCache[frameName] = index;
        return index;
    }

    private static int InternStack(ProfilesDictionary dictionary, Dictionary<string, int> stringCache, Dictionary<string, int> functionCache, Dictionary<string, int> locationCache, Dictionary<string, int> stackCache, Dictionary<(int, long, string), int> attributeCache, IReadOnlyList<string> frames)
    {
        var locationIndices = new int[frames.Count];
        for (var i = 0; i < frames.Count; i++)
            locationIndices[i] = InternLocation(dictionary, stringCache, functionCache, locationCache, attributeCache, frames[i]);

        var key = string.Join(",", locationIndices);
        if (stackCache.TryGetValue(key, out var index))
            return index;

        var stack = new Stack();
        stack.LocationIndices.AddRange(locationIndices); // leaf-first
        index = dictionary.StackTable.Count;
        dictionary.StackTable.Add(stack);
        stackCache[key] = index;
        return index;
    }

    private static int InternAttribute(ProfilesDictionary dictionary, Dictionary<string, int> stringCache, Dictionary<(int, long, string), int> attributeCache, string key, AnyValue value)
    {
        var keyStrindex = InternString(dictionary, stringCache, key);
        var cacheKey = (keyStrindex, value.IntValue, value.HasStringValue ? value.StringValue : null);
        if (attributeCache.TryGetValue(cacheKey, out var index))
            return index;

        var attribute = new KeyValueAndUnit
        {
            KeyStrindex = keyStrindex,
            Value = value,
        };
        index = dictionary.AttributeTable.Count;
        dictionary.AttributeTable.Add(attribute);
        attributeCache[cacheKey] = index;
        return index;
    }

    private static int InternLink(ProfilesDictionary dictionary, Dictionary<(long, long, long), int> linkCache, long traceIdHigh, long traceIdLow, long spanId)
    {
        // A fully-zero context means "no linked span" -> return the reserved link_table[0] sentinel
        // (Sample.link_index == 0 encodes "no link" per the OTLP profiles spec). This is the common case:
        // any sample not taken on a thread with a live pushed trace/span (idle threads, background threads,
        // and the traced thread outside its transaction) lands here. Only a genuinely non-zero context
        // allocates/reuses a real entry at index >= 1 below.
        if (traceIdHigh == 0 && traceIdLow == 0 && spanId == 0)
            return 0;

        var cacheKey = (traceIdHigh, traceIdLow, spanId);
        if (linkCache.TryGetValue(cacheKey, out var index))
            return index;

        var link = new Link
        {
            // 16-byte trace id: high 8 bytes then low 8 bytes, each big-endian (most-significant first).
            TraceId = ByteString.CopyFrom(ToBigEndian16(traceIdHigh, traceIdLow)),
            // 8-byte span id, big-endian.
            SpanId = ByteString.CopyFrom(ToBigEndian8(spanId)),
        };
        index = dictionary.LinkTable.Count;
        dictionary.LinkTable.Add(link);
        linkCache[cacheKey] = index;
        return index;
    }

    private static byte[] ToBigEndian16(long high, long low)
    {
        var bytes = new byte[16];
        WriteBigEndian(bytes, 0, high);
        WriteBigEndian(bytes, 8, low);
        return bytes;
    }

    private static byte[] ToBigEndian8(long value)
    {
        var bytes = new byte[8];
        WriteBigEndian(bytes, 0, value);
        return bytes;
    }

    private static void WriteBigEndian(byte[] destination, int offset, long value)
    {
        for (var i = 0; i < 8; i++)
            destination[offset + i] = (byte)(value >> (8 * (7 - i)));
    }
}
