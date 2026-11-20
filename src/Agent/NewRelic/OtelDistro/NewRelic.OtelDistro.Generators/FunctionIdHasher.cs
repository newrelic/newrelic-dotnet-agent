// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Linked into Core (see Core.csproj), which defines NR_OTELDISTRO_CORE; the generator build compiles the other branch.
#if NR_OTELDISTRO_CORE
namespace NewRelic.Agent.Core.OtelDistro.Xml;
#else
namespace NewRelic.OtelDistro.Generators;
#endif

/// <summary>
/// Derives a stable, synthetic function id for a generated distro integration from its target method shape.
/// Uses FNV-1a 64 over the UTF-8 bytes of <c>assembly|type|method|parameters</c>, then sets the high bit so the
/// result never collides with a real CLR FunctionID (which never sets that bit).
/// </summary>
public static class FunctionIdHasher
{
    private const ulong HighBit = 0x8000000000000000UL;

    public static ulong Compute(string assembly, string type, string method, string parameters) =>
        Compute(assembly, type, method, parameters, string.Empty);

    /// <summary>
    /// As <see cref="Compute(string, string, string, string)"/>, plus the entry's version range key
    /// (<see cref="VersionRangeConversion.RangeKey"/>). The default range (<c>""</c>) adds nothing, so unranged entries
    /// keep the ids and class names they had before ranges were emitted; any other range appends <c>"|" + rangeKey</c>,
    /// so version-split siblings of one method get distinct ids.
    /// </summary>
    public static ulong Compute(string assembly, string type, string method, string parameters, string rangeKey)
    {
        var input = assembly + "|" + type + "|" + method + "|" + parameters;
        if (rangeKey.Length > 0)
        {
            input += "|" + rangeKey;
        }

        return Fnv1a64.AddUtf8(Fnv1a64.OffsetBasis, input) | HighBit;
    }
}
