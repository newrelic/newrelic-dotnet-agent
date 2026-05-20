// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.SignalRPoc.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();

var app = builder.Build();

app.MapHub<ChatHub>("/chathub");
app.MapGet("/", () => "SignalR POC server. Hub at /chathub.");

app.Run();
