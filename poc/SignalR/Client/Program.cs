// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.SignalR.Client;

var hubUrl = args.Length > 0 ? args[0] : "http://localhost:5050/chathub";
var iterations = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;

Console.WriteLine($"Connecting to {hubUrl} (iterations={iterations})");

var connection = new HubConnectionBuilder()
    .WithUrl(hubUrl)
    .WithAutomaticReconnect()
    .Build();

connection.On<string, string>("ReceiveMessage", (user, message) =>
    Console.WriteLine($"  <- ReceiveMessage: {user}: {message}"));

await connection.StartAsync();
Console.WriteLine($"Connected. ConnectionId={connection.ConnectionId}");

for (var i = 0; i < iterations; i++)
{
    Console.WriteLine($"--- Iteration {i + 1}/{iterations} ---");

    // 1. Regular invoke (acceptance criteria 1 + 3: transaction + DT)
    var echo = await connection.InvokeAsync<string>("SendMessage", "poc-client", $"hello #{i}");
    Console.WriteLine($"  -> SendMessage returned: {echo}");

    // 2. Error path (acceptance criterion 2)
    try
    {
        await connection.InvokeAsync("ThrowSomething", $"iter-{i}");
        Console.WriteLine("  !! Expected exception, none thrown");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  -> ThrowSomething (expected): {ex.GetType().Name}: {ex.Message}");
    }

    // 3. Streaming (acceptance criterion 4)
    await foreach (var item in connection.StreamAsync<int>("Counter", 5, 100))
    {
        Console.WriteLine($"  -> Counter yielded: {item}");
    }
}

// 4. Disconnect (acceptance criterion 6)
await connection.StopAsync();
await connection.DisposeAsync();
Console.WriteLine("Disconnected. Done.");
