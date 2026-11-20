// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The instrumentation sites of a read NRIDT table, by site index (= record index; see
/// InstrumentationDefinitionsTableFormat.cs). Built only by <see cref="InstrumentationDefinitionsTable.TryRead"/>, from
/// a table it has fully validated.
///
/// Lazy: start-up stores one struct per record (references to the strings the reader already decoded, plus a few
/// numbers), not one heap object per record. A <see cref="DistroInstrumentationSite"/> is built on the first
/// <see cref="TryGet"/> for its index and published with Interlocked.CompareExchange, so every later call for that
/// index is an array read and a null check. Two threads making the first call at once may both build a site; one is
/// published and both get the published one.
///
/// Per-site gate: <see cref="IsOn"/> says whether the site's tracer should run. It starts as "on unless the record is
/// default-off" and is replaced as a whole by <see cref="SetOn"/>, which publishes a new array, so a reader sees either
/// the old state or the new one for every site, never a half-written mix, and pays one volatile read.
/// </summary>
public sealed class DistroInstrumentationSites
{
    private readonly SiteData[] _data;
    private readonly DistroInstrumentationSite[] _sites;

    // Never mutated after it is published (SetOn swaps in a copy). Always Count long.
    private bool[] _on;

    public int Count => _data.Length;

    // Internal: only the reader may fill one, and it fills every index before handing it out.
    internal DistroInstrumentationSites(int count)
    {
        _data = new SiteData[count];
        _sites = new DistroInstrumentationSite[count];
        _on = new bool[count];
    }

    /// <summary>
    /// The site at <paramref name="index"/>. Returns false, with a null <paramref name="site"/>, when the index is
    /// negative or not less than <see cref="Count"/>. Never throws.
    /// </summary>
    public bool TryGet(int index, out DistroInstrumentationSite site)
    {
        if (index < 0 || index >= _sites.Length)
        {
            site = null;
            return false;
        }

        site = Volatile.Read(ref _sites[index]) ?? Publish(index);
        return true;
    }

    /// <summary>
    /// Whether the site at <paramref name="index"/> is on. Called on every instrumented call, so it is one volatile
    /// read of the current state array plus a bounds check. False for an index out of range. Never throws.
    /// </summary>
    public bool IsOn(int index)
    {
        var on = Volatile.Read(ref _on);
        return (uint)index < (uint)on.Length && on[index];
    }

    /// <summary>
    /// Replaces the on-state of every site at once with a copy of <paramref name="on"/> (on[i] for site i). Throws
    /// ArgumentNullException for null and ArgumentException when its length is not <see cref="Count"/>; the state is
    /// unchanged then.
    /// </summary>
    public void SetOn(bool[] on)
    {
        if (on == null)
        {
            throw new ArgumentNullException(nameof(on));
        }

        if (on.Length != _data.Length)
        {
            throw new ArgumentException("on has " + on.Length + " entries but there are " + _data.Length + " sites", nameof(on));
        }

        Volatile.Write(ref _on, (bool[])on.Clone());
    }

    /// <summary>
    /// The function id of the site at <paramref name="index"/>, read without building the site. Throws
    /// ArgumentOutOfRangeException out of range.
    /// </summary>
    public ulong FunctionId(int index) => _data[CheckIndex(index)].FunctionId;

    /// <summary>The point key of the site at <paramref name="index"/>. Throws ArgumentOutOfRangeException out of range.</summary>
    public ulong PointKey(int index) => _data[CheckIndex(index)].PointKey;

    /// <summary>
    /// The index of the shipped XML file (<see cref="DistroShippedXmlManifest"/>) the site's point came from, or -1 for
    /// a point from the plugin's own XML. Throws ArgumentOutOfRangeException out of range.
    /// </summary>
    public int SourceFile(int index) => _data[CheckIndex(index)].SourceFile;

    /// <summary>Whether the site's point is disabled in the shipped XML. Throws ArgumentOutOfRangeException out of range.</summary>
    public bool IsDefaultOff(int index) => _data[CheckIndex(index)].DefaultOff;

    /// <summary>
    /// Whether the site is an NR log-forwarding wrapper. Read without building the site, so the call target can bracket
    /// a log-forwarding call with the dedupe counter at one array read. False (never throws) for an index out of range,
    /// so a bad index simply skips dedupe.
    /// </summary>
    public bool IsLogForwarding(int index) => (uint)index < (uint)_data.Length && _data[index].LogForwarding;

    // Called by the reader once per record, after the record passed every check, so the site constructor's own checks
    // (non-empty tracer factory, assembly, type and method) always hold and it cannot throw. Also sets the record's
    // initial on-state; the array is not published yet, so a plain write is enough.
    internal void Set(int index, string tracerFactoryName, string metricName, uint tracerArguments, string assemblyName, string typeName, string methodName, string argumentSignature, ulong functionId, ulong pointKey, int sourceFile, bool defaultOff, bool logForwarding)
    {
        _data[index] = new SiteData(tracerFactoryName, metricName, tracerArguments, assemblyName, typeName, methodName, argumentSignature, functionId, pointKey, sourceFile, defaultOff, logForwarding);
        _on[index] = !defaultOff;
    }

    private int CheckIndex(int index)
    {
        if ((uint)index >= (uint)_data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "site index must be at least 0 and less than Count (" + _data.Length + ")");
        }

        return index;
    }

    // First call for this index: build a site, publish it unless another thread already did, and return whichever
    // site is published.
    private DistroInstrumentationSite Publish(int index)
    {
        var data = _data[index];
        var built = new DistroInstrumentationSite(
            data.TracerFactoryName,
            data.MetricName,
            data.TracerArguments,
            data.AssemblyName,
            data.TypeName,
            data.MethodName,
            data.ArgumentSignature,
            data.FunctionId,
            data.PointKey,
            data.SourceFile,
            data.DefaultOff,
            data.LogForwarding);
        Interlocked.CompareExchange(ref _sites[index], built, null);
        return Volatile.Read(ref _sites[index]);
    }

    // One record's site fields. The strings are the reader's shared instances, so a struct holds only references.
    private readonly struct SiteData
    {
        public readonly string TracerFactoryName;
        public readonly string MetricName;
        public readonly uint TracerArguments;
        public readonly string AssemblyName;
        public readonly string TypeName;
        public readonly string MethodName;
        public readonly string ArgumentSignature;
        public readonly ulong FunctionId;
        public readonly ulong PointKey;
        public readonly int SourceFile;
        public readonly bool DefaultOff;
        public readonly bool LogForwarding;

        public SiteData(string tracerFactoryName, string metricName, uint tracerArguments, string assemblyName, string typeName, string methodName, string argumentSignature, ulong functionId, ulong pointKey, int sourceFile, bool defaultOff, bool logForwarding)
        {
            TracerFactoryName = tracerFactoryName;
            MetricName = metricName;
            TracerArguments = tracerArguments;
            AssemblyName = assemblyName;
            TypeName = typeName;
            MethodName = methodName;
            ArgumentSignature = argumentSignature;
            FunctionId = functionId;
            PointKey = pointKey;
            SourceFile = sourceFile;
            DefaultOff = defaultOff;
            LogForwarding = logForwarding;
        }
    }
}
