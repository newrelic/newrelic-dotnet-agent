// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.IO;
using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerizedAspNetCoreApp;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();

        // listen to any ip on port 80 for http
        IPEndPoint ipEndPointHttp = new IPEndPoint(IPAddress.Any, 80);
        builder.WebHost.UseUrls($"http://{ipEndPointHttp}");

        var app = builder.Build();

        // Configure the HTTP request pipeline.

        app.UseAuthorization();

        app.MapControllers();

        var task = app.RunAsync();

        CreatePidFile();

        task.GetAwaiter().GetResult();
    }

    public static void CreatePidFile()
    {
        var pidFileNameAndPath = Path.Combine(Environment.GetEnvironmentVariable("NEWRELIC_LOG_DIRECTORY"), "containerizedapp.pid");
        var pid = Process.GetCurrentProcess().Id;
        var file = File.CreateText(pidFileNameAndPath);
        file.WriteLine(pid);
    }
}
