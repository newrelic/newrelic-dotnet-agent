// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApplicationLifecycle;
using BlazorSignalRApplication.Components;
using BlazorSignalRApplication.Hubs;

namespace BlazorSignalRApplication;

public class Program
{
    private static string _port;

    public static async Task Main(string[] args)
    {
        _port = AppLifecycleManager.GetPortFromArgs(args);

        var ct = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.MapHub<EchoHub>("/echohub");

        app.Urls.Add($"http://127.0.0.1:{_port}");

        var task = app.RunAsync(ct.Token);

        AppLifecycleManager.CreatePidFile();

        AppLifecycleManager.WaitForTestCompletion(_port);

        await ct.CancelAsync();

        await task;
    }
}
