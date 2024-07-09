// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using AwsSdkTestApp.SQSBackgroundService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AwsSdkTestApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // Add services to the container.
        builder.Services.AddControllers();

        // add the SQS receiver service and the request and response queues
        builder.Services.AddHostedService<SQSReceiverService>();
        builder.Services.AddSingleton<ISQSRequestQueue, SQSRequestQueue>();
        builder.Services.AddSingleton<ISQSResponseQueue, SQSResponseQueue>();

        // listen to any ip on port 80 for http
        IPEndPoint ipEndPointHttp = new IPEndPoint(IPAddress.Any, 80);
        builder.WebHost.UseUrls($"http://{ipEndPointHttp}");

        var app = builder.Build();

        // Configure the HTTP request pipeline.

        app.UseAuthorization();

        app.MapControllers();

        await app.StartAsync();

        CreatePidFile();

        await app.WaitForShutdownAsync();
    }

    static void CreatePidFile()
    {
        var pidFileNameAndPath = Path.Combine(Environment.GetEnvironmentVariable("NEWRELIC_LOG_DIRECTORY"), "containerizedapp.pid");
        var pid = Environment.ProcessId;
        using var file = File.CreateText(pidFileNameAndPath);
        file.WriteLine(pid);
    }
}
