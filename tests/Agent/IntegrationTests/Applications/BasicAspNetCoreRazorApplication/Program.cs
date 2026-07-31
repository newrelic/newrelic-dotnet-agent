// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using ApplicationLifecycle;

namespace BasicAspNetCoreRazorApplication;

public class Program
{
    private static string _port;

    public static async Task Main(string[] args)
    {

        _port = AppLifecycleManager.GetPortFromArgs(args);

        var ct = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        var enableResponseCompression = Environment.GetEnvironmentVariable("ENABLE_RESPONSE_COMPRESSION");
        if (enableResponseCompression == "1")
            builder.Services.AddResponseCompression();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        if (enableResponseCompression == "1")
            app.UseResponseCompression();

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();

        app.MapGet("/foo", async context =>
        {
            var subscriptions = new
            {
                Foo = 1, Bar = "Something"
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(subscriptions,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }));
        });

        // Writes an HTML document in two explicit writes, split exactly on the closing '>'
        // of the opening <head> tag. That puts the RUM injection index exactly at the end of
        // the first write's buffer - see BrowserAgentAutoInjectionSplitHead.
        app.MapGet("/splithead", async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("<html><head>");
            await context.Response.WriteAsync("</head><body>split head page</body></html>");
        });

        app.Urls.Add($"http://127.0.0.1:{_port}");

        var task = app.RunAsync(ct.Token);

        AppLifecycleManager.CreatePidFile();

        AppLifecycleManager.WaitForTestCompletion(_port);

        await ct.CancelAsync();

        await task;
    }
}