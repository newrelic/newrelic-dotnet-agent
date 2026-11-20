// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

// Linked into Core (see Core.csproj), which defines NR_OTELDISTRO_CORE; the generator build compiles the other branch.
// Internal in both: each assembly compiles its own copy, and tests reach it through the public hashers.
#if NR_OTELDISTRO_CORE
namespace NewRelic.Agent.Core.OtelDistro.Xml;
#else
namespace NewRelic.OtelDistro.Generators;
#endif

/// <summary>The one FNV-1a 64 loop the distro's hashes share (function ids, point keys, XML content, payload ids).</summary>
internal static class Fnv1a64
{
    public const ulong OffsetBasis = 14695981039346656037UL;
    public const ulong Prime = 1099511628211UL;

    public static ulong Add(ulong hash, byte value) => (hash ^ value) * Prime;

    /// <summary>Folds the UTF-8 bytes of <paramref name="text"/> into <paramref name="hash"/>.</summary>
    public static ulong AddUtf8(ulong hash, string text)
    {
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash = Add(hash, b);
        }

        return hash;
    }
}
