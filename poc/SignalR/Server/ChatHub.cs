// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;

namespace NewRelic.SignalRPoc.Server;

public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    private static readonly string[] _searchCorpus =
    [
        "alpha", "albatross", "alphabet", "alpine",
        "beacon", "beagle", "beaker", "berserk",
        "candle", "candy", "canyon", "capable",
        "delta", "denim", "dental", "destiny",
        "echo", "edge", "elite", "ember",
        "fjord", "flame", "flock", "fluent",
    ];

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    // Autocomplete-shaped hub method: high-frequency, cheap, small payload.
    // Used by the burst-mode load test to characterize per-invoke bridge cost
    // and transaction-name cardinality.
    public string[] Search(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return Array.Empty<string>();
        }

        var matches = new List<string>(8);
        foreach (var word in _searchCorpus)
        {
            if (word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(word);
                if (matches.Count == 8)
                {
                    break;
                }
            }
        }
        return matches.ToArray();
    }

    // Acceptance criterion 1 + 3: regular invocation, single connected DT.
    public async Task<string> SendMessage(string user, string message)
    {
        _logger.LogInformation("SendMessage from {User}: {Message}", user, message);
        await Clients.All.SendAsync("ReceiveMessage", user, message);
        return $"echo:{message}";
    }

    // Acceptance criterion 2: error attribution on a hub method.
    public Task ThrowSomething(string what)
    {
        throw new InvalidOperationException($"Intentional POC failure: {what}");
    }

    // Acceptance criterion 4: streaming hub method, transaction spans the
    // producing IAsyncEnumerable's lifetime.
    public async IAsyncEnumerable<int> Counter(
        int count,
        int delayMs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Delay(delayMs, cancellationToken);
        }
    }

    // Acceptance criterion 6: lifecycle activities visible.
    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
