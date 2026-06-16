// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.SignalRPoc.Blazor.Components;
using NewRelic.SignalRPoc.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// MapStaticAssets only loads the static-web-assets manifest in Development by
// default. The agent's host context may not be Development at attach time, so
// opt in explicitly - otherwise _framework/blazor.web.js 404s.
builder.WebHost.UseStaticWebAssets();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Singleton - the in-memory corpus has no per-user state.
builder.Services.AddSingleton<ISearchService, SearchService>();

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
