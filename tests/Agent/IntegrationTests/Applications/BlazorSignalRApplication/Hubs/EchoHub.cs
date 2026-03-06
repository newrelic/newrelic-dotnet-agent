// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using NewRelic.Api.Agent;

namespace BlazorSignalRApplication.Hubs;

public class EchoHub : Hub
{
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task SendEcho(string phrase)
    {
        Console.WriteLine($"[EchoHub] Echo: {phrase}");
        await Clients.Caller.SendAsync("ReceiveEcho", phrase);
    }
}
