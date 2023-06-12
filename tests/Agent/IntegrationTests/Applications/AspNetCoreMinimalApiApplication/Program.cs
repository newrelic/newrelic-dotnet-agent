// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApplicationLifecycle;

var _port = AppLifecycleManager.GetPortFromArgs(args);

var builder = WebApplication.CreateBuilder(args);


var app = builder.Build();
app.Urls.Add($@"http://localhost:{_port}/");

// set up identical routes with different request methods
app.MapGet("/minimalapi", () => Results.Ok());
app.MapPost("/minimalapi", () => Results.Ok());

var ct = new CancellationTokenSource();
var task = app.RunAsync(ct.Token);

AppLifecycleManager.CreatePidFile();

AppLifecycleManager.WaitForTestCompletion(_port);

ct.Cancel();

task.GetAwaiter().GetResult();


