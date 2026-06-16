// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Api.Agent;

namespace NewRelic.SignalRPoc.Blazor.Services;

// In-memory autocomplete index. A real implementation would hit a database,
// search index, or cache; we simulate a small I/O latency so the segment
// duration is meaningful when the agent traces it.
public sealed class SearchService : ISearchService
{
    private static readonly string[] _corpus =
    [
        "alpha", "albatross", "alphabet", "alpine",
        "beacon", "beagle", "beaker", "berserk",
        "candle", "candy", "canyon", "capable",
        "delta", "denim", "dental", "destiny",
        "echo", "edge", "elite", "ember",
        "fjord", "flame", "flock", "fluent",
    ];

    private const int MaxResults = 8;
    private const int SimulatedLookupDelayMs = 15;

    // Returns Task<T> rather than ValueTask<T> on purpose: the .NET agent's
    // async-wrapper machinery does not instrument ValueTask state machines,
    // so a [Trace]'d ValueTask method silently produces no segment.
    [Trace]
    public async Task<IReadOnlyList<string>> FindAsync(string prefix, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return Array.Empty<string>();
        }

        // Simulate the latency of a real backing store (DB, search index, cache).
        await Task.Delay(SimulatedLookupDelayMs, cancellationToken);

        var matches = new List<string>(MaxResults);
        foreach (var word in _corpus)
        {
            if (word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(word);
                if (matches.Count == MaxResults)
                {
                    break;
                }
            }
        }
        return matches;
    }
}
