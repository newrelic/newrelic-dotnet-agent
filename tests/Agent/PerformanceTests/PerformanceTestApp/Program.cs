// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using MongoDB.Driver;
using RabbitMQ.Client;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, config) => config.WriteTo.Console());

builder.Services.AddControllers();

var mongoConnectionString = builder.Configuration["MONGODB_CONNECTION_STRING"]
    ?? "mongodb://localhost:27017";
builder.Services.AddSingleton<IMongoDatabase>(
    new MongoClient(mongoConnectionString).GetDatabase("perftest"));

var redisConnectionString = builder.Configuration["REDIS_CONNECTION_STRING"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

var rabbitHost = builder.Configuration["RABBITMQ_HOST"] ?? "localhost";
var rabbitConnection = new ConnectionFactory { HostName = rabbitHost }.CreateConnection();
using (var ch = rabbitConnection.CreateModel())
    ch.QueueDeclare("perf", durable: true, exclusive: false, autoDelete: false, arguments: null);
builder.Services.AddSingleton<IConnection>(rabbitConnection);

builder.WebHost.UseUrls($"http://{IPAddress.Any}:8080");

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();

app.Run();
