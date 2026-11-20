// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using NewRelic.Agent.Core.OtelDistro.Xml;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// A content-derived id for a set of call-target definitions handed to OTel's registration, so that each different set gets
/// its own id (OTel's native side ignores a second registration under an id it has already processed) and the same set
/// always gets the same one.
/// </summary>
public static class DistroPayloadId
{
    private const string FirstSeed = "NRIDT-A";
    private const string SecondSeed = "NRIDT-B";

    /// <summary>
    /// 32 uppercase hex characters: FNV-1a 64 of the distinct function ids in ascending order (8 bytes each, little-endian),
    /// after the UTF-8 seed "NRIDT-A", then the same after "NRIDT-B". Independent of input order and duplicates. Null for an
    /// empty or null set. Never throws.
    /// </summary>
    public static string For(IEnumerable<ulong> functionIds)
    {
        if (functionIds == null)
        {
            return null;
        }

        // Sort and dedupe in place: no LINQ, so start-up JITs no Set/OrderedEnumerable instantiations over ulong.
        var sorted = new List<ulong>(functionIds).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        Array.Sort(sorted);
        var distinct = 1;
        for (var i = 1; i < sorted.Length; i++)
        {
            if (sorted[i] != sorted[distinct - 1])
            {
                sorted[distinct++] = sorted[i];
            }
        }

        return Hash(FirstSeed, sorted, distinct).ToString("X16") + Hash(SecondSeed, sorted, distinct).ToString("X16");
    }

    /// <summary>
    /// The run-time registration id: <see cref="For"/> plus "-AD" and the AppDomain id (invariant culture). Each AppDomain
    /// gets its own id for the same set, so OTel's native side takes every domain's registration instead of ignoring the
    /// second as already processed. Null when <see cref="For"/> is null. Never throws.
    /// </summary>
    public static string ForAppDomain(IEnumerable<ulong> functionIds, int appDomainId)
    {
        var contentId = For(functionIds);
        return contentId == null ? null : contentId + "-AD" + appDomainId.ToString(CultureInfo.InvariantCulture);
    }

    private static ulong Hash(string seed, ulong[] sorted, int count)
    {
        var hash = Fnv1a64.AddUtf8(Fnv1a64.OffsetBasis, seed);
        for (var i = 0; i < count; i++)
        {
            var id = sorted[i];
            for (var shift = 0; shift < 64; shift += 8)
            {
                hash = Fnv1a64.Add(hash, (byte)(id >> shift));
            }
        }

        return hash;
    }
}
