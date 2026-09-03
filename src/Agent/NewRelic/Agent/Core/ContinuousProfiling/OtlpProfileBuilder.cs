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
    private const string EntityGuidKey = "entity.guid";
    private const string HostKey = "host";
    private const string ThreadIdKey = "thread.id";
    private const string ThreadNameKey = "thread.name";

    // profiles.proto only defines cpu/off_cpu/allocated_objects/allocated_space as sample types, and
    // Profile.sample_type is singular -- so cpu and off_cpu are separate Profile messages sharing this
    // request's ProfilesDictionary (stacks/strings interned once). cpu = on-CPU samples, off_cpu = parked.
    private const string OffCpuSampleTypeName = "off_cpu";
    private const string CpuSampleTypeName = "cpu";
    private const string NanosecondsUnit = "nanoseconds"; // == PeriodTypeUnit

    // period = the configured sampling interval in nanoseconds (an all-thread snapshot is taken every interval).
    // The on- and off-CPU profiles are each sampled on that same wall-clock cadence, but only the cpu profile's
    // values represent CPU time -- an off-cpu sample's value is parked wall-clock time, so it gets its own,
    // truthful period_type rather than reusing "cpu".
    private const string PeriodTypeName = "cpu";
    private const string OffCpuPeriodTypeName = "wall_clock";
    private const string PeriodTypeUnit = "nanoseconds";

    // profile.frame.type: the managed walk yields "dotnet" frames except the synthetic native-thread-entry
    // marker (functionId == 0), tagged "native". That marker isn't only the terminal frame -- it can also
    // appear mid-stack at managed/unmanaged transitions.
    private const string FrameTypeKey = "profile.frame.type";
    private const string FrameTypeDotnet = "dotnet";
    private const string FrameTypeNative = "native";

    // MUST match the native thread-entry label produced for functionId == 0 in
    // Profiler/ContinuousProfiler/ContinuousProfiler.h. A change there without a matching change here
    // silently mis-tags that frame as "dotnet".
    private const string NativeFrameName = "Native.Function Call";

    // The agent's own background threads run agent-core code, so their OWNING frame (see IsAgentThreadSample)
    // is under "NewRelic.Agent.Core.". Deliberately Core-specific, not a broad "NewRelic." match: that would
    // also catch the public API ("NewRelic.Api.Agent."), wrapper frames on customer threads
    // ("NewRelic.Providers."), and the integration-test harness ("NewRelic.Agent.IntegrationTests."),
    // wrongly dropping legitimate customer/test samples.
    private const string AgentFramePrefix = "NewRelic.Agent.Core.";

    // CLR thread/timer/threadpool dispatch scaffolding sits OUTWARD of the frame that owns the work
    // (Thread.StartCallback, PortableThreadPool+WorkerThread.WorkerThreadStart, TimerQueueTimer.Fire, ...)
    // and is skipped when locating a sample's owning frame. Matched by namespace rather than an enumerated
    // frame list, since new BCL dispatch shapes would silently evade a fixed list. Kept narrow to
    // "System.Threading." only -- a broader System.*/Microsoft.* match would misattribute e.g. an ASP.NET
    // Core request thread (rooted in "Microsoft.AspNetCore.*") to the agent.
    private const string ThreadPlumbingPrefix = "System.Threading.";

    // includeAgentCode: when false, samples taken on the agent's own threads (owning frame under
    // "NewRelic.Agent.Core.") are dropped so the profile carries only the customer application. Defaults to
    // true (no filtering) for callers/tests that don't care; the CP service passes the configured value,
    // which defaults to false.
    public static ExportProfilesServiceRequest Build(IReadOnlyList<ManagedThreadSample> samples, long startUnixNano, long durationNano, string serviceName, string entityGuid = null, string host = null, long periodNanos = 0, bool includeAgentCode = true)
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

        // Register the zero-value entries above in the interning caches too, so a real value that happens to
        // match the zero value (an empty-name frame, a zero-frame stack, an unset attribute) resolves back to
        // index 0 instead of being re-added as a duplicate entry that a naive "index 0 means empty" reader
        // downstream wouldn't recognize as such. Function/attribute are keyed the same way InternFunction/
        // InternAttribute key them; stack is keyed by InternStack's empty-frames join ("").
        functionTable[string.Empty] = 0;
        stackTable[string.Empty] = 0;
        attributeTable[(0, 0L, null)] = 0;

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
            // Drop agent-own-thread samples unless explicitly included. Two independent signals, either
            // sufficient, both gated by the same includeAgentCode toggle: the frame-text match
            // (AgentFramePrefix, catches customer threads mid-instrumented-call) and the native
            // thread-identity flag (IsAgentWork, catches agent threads parked with no agent frame on the
            // stack at all, which frame text alone can't see).
            if (!includeAgentCode && (IsAgentThreadSample(sample.Frames) || sample.IsAgentWork))
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

        // off_cpu + cpu in nanoseconds, only when a real interval is known -- both sample types are
        // time-valued, so no period means nothing meaningful to emit.
        //
        // Emit a profile only for a non-empty partition side: the OTLP profiles ingest rejects a Profile
        // with zero samples ("no_samples" drop), and a side is legitimately empty on a sweep that caught
        // nothing on- or off-CPU.
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
                    OffCpuSampleTypeName, NanosecondsUnit, OffCpuPeriodTypeName, resolved, valueForSample: _ => periodNanos, includeSample: r => !r.OnCpu));

            // cpu:nanoseconds -- on-CPU threads only.
            if (anyOnCpu)
                scopeProfiles.Profiles.Add(BuildProfile(dictionary, stringTable, startUnixNano, durationNano, periodNanos,
                    CpuSampleTypeName, NanosecondsUnit, PeriodTypeName, resolved, valueForSample: _ => periodNanos, includeSample: r => r.OnCpu));
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
        // host mirrors the "host" field of the connect payload (ConnectModel.HostName) -- always present
        // there, so always emitted here too, unlike entity.guid below.
        resourceProfiles.Resource.Attributes.Add(new KeyValue
        {
            Key = HostKey,
            Value = new AnyValue { StringValue = host ?? string.Empty },
        });
        // entity.guid is only known post-connect; omit the attribute entirely (not an empty string) until
        // then, matching the entity.guid metadata gating in Agent.cs.
        if (!string.IsNullOrEmpty(entityGuid))
        {
            resourceProfiles.Resource.Attributes.Add(new KeyValue
            {
                Key = EntityGuidKey,
                Value = new AnyValue { StringValue = entityGuid },
            });
        }
        resourceProfiles.ScopeProfiles.Add(scopeProfiles);

        var request = new ExportProfilesServiceRequest { Dictionary = dictionary };
        request.ResourceProfiles.Add(resourceProfiles);
        return request;
    }

    // A sample is "the agent's own" when the OWNING frame -- the outermost frame that is neither the native
    // thread-entry marker nor runtime thread plumbing -- is under "NewRelic.Agent.Core.". Frames is leaf-first
    // (see ManagedThreadSample.Frames), so scan inward from the end.
    //
    // Match the owning frame, not any frame: a customer thread executing instrumented code legitimately has
    // agent-core frames as leaf frames while the tracer runs (AgentShim, wrapper Finish/Start), and an
    // any-frame match would wrongly discard those customer samples. Both skips (native marker and thread
    // plumbing) must apply together -- skipping only the native marker still leaves a plumbing frame as the
    // apparent "owner", so the same agent thread is misattributed inconsistently between sweeps.
    //
    // A stack consisting solely of plumbing (e.g. an idle threadpool/timer thread) yields no owning frame and
    // is NOT treated as the agent's -- it's ordinary BCL idle time, not something to filter out.
    //
    // Recall is limited: agent threads mostly park in runtime/BCL code with no agent-core frame on the walked
    // stack, which frame text can't see. This narrows false-drops of customer data; it is not a reliable
    // agent-thread detector.
    private static bool IsAgentThreadSample(IReadOnlyList<string> frames)
    {
        for (var i = frames.Count - 1; i >= 0; i--)
        {
            var frame = frames[i];
            if (frame == null || frame == NativeFrameName || frame.StartsWith(ThreadPlumbingPrefix, System.StringComparison.Ordinal))
                continue;

            return frame.StartsWith(AgentFramePrefix, System.StringComparison.Ordinal);
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

    // Emits one Profile from the already-resolved samples, sharing the caller's dictionary/interning caches
    // -- no re-interning happens here.
    private static Profile BuildProfile(ProfilesDictionary dictionary, Dictionary<string, int> stringTable,
        long startUnixNano, long durationNano, long periodNanos, string sampleTypeName, string sampleTypeUnit,
        string periodTypeName, List<ResolvedSample> resolved, System.Func<ResolvedSample, long> valueForSample, System.Func<ResolvedSample, bool> includeSample)
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
                TypeStrindex = InternString(dictionary, stringTable, periodTypeName),
                UnitStrindex = InternString(dictionary, stringTable, PeriodTypeUnit),
            };
            profile.Period = periodNanos;
        }

        // profiles.proto: a Sample's identity is {stack_index, set_of(attribute_indices), link_index} --
        // samples sharing an identity SHOULD be combined rather than emitted as separate entries. A drain can
        // read multiple sweeps whose samples share identity (e.g. the same thread parked on the same stack
        // across sweeps), so aggregate by identity before emitting.
        var aggregatedValues = new Dictionary<(int Stack, int Link, int ThreadIdAttr, int ThreadNameAttr), long>();
        var order = new List<(int Stack, int Link, int ThreadIdAttr, int ThreadNameAttr)>();
        foreach (var r in resolved)
        {
            if (!includeSample(r))
                continue;

            var identity = (r.StackIndex, r.LinkIndex, r.ThreadIdAttr, r.ThreadNameAttr);
            var value = valueForSample(r);
            if (aggregatedValues.TryGetValue(identity, out var existing))
            {
                aggregatedValues[identity] = existing + value;
            }
            else
            {
                aggregatedValues[identity] = value;
                order.Add(identity);
            }
        }

        foreach (var identity in order)
        {
            var protoSample = new Sample { StackIndex = identity.Stack, LinkIndex = identity.Link };
            protoSample.Values.Add(aggregatedValues[identity]);
            protoSample.AttributeIndices.Add(identity.ThreadIdAttr);
            protoSample.AttributeIndices.Add(identity.ThreadNameAttr);
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
