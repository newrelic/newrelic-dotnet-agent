// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GrpcGreet;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Grpc;

[Library]
public class GrpcExerciser
{
    [LibraryMethod]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task SayHello(string port)
    {
        var actualPort = port == "0" ? GetAvailablePort() : int.Parse(port);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(actualPort, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });
        builder.Services.AddGrpc();

        var app = builder.Build();
        app.MapGrpcService<GreeterService>();

        await app.StartAsync();

        try
        {
            await MakeGrpcClientCall(actualPort);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    private static async Task MakeGrpcClientCall(int port)
    {
        using var channel = GrpcChannel.ForAddress($"http://localhost:{port}", new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
            }
        });

        var client = new Greeter.GreeterClient(channel);
        var reply = await client.SayHelloAsync(new HelloRequest { Name = "TestGrpc" });

        Console.WriteLine($"[GrpcExerciser] Reply: {reply.Message}");
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

#endif
