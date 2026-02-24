// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.WebHost.UseUrls($"http://{IPAddress.Any}:8080");

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();

app.Run();
