// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApplicationLifecycle;

namespace BasicAspNetCoreRazorApplication
{
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

            app.Urls.Add($"http://*:{_port}");

            var task = app.RunAsync(ct.Token);

            AppLifecycleManager.CreatePidFile();

            AppLifecycleManager.WaitForTestCompletion(_port);

            ct.Cancel();

            await task;
        }
    }
}
