// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassTransitTestApp;

public class Program
{
    private static string _kafkaTopic;
    private static string _kafkaBroker;
    private static string _rabbitMqHost;
    private static string _rabbitMqQueueName;

    public static async Task Main(string[] args)
    {
        _kafkaTopic = Environment.GetEnvironmentVariable("MASSTRANSIT_KAFKA_TOPIC") ?? "masstransit-test-topic";
        _kafkaBroker = GetKafkaBootstrapServer();
        _rabbitMqHost = Environment.GetEnvironmentVariable("MASSTRANSIT_RABBITMQ_HOST") ?? "rabbitmq";
        _rabbitMqQueueName = Environment.GetEnvironmentVariable("MASSTRANSIT_RABBITMQ_QUEUE") ?? "masstransit-test-queue";

        // Pre-create the Kafka topic before MassTransit starts
        await CreateKafkaTopicAsync();

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();

        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<RabbitMqMessageConsumer>();

            // RabbitMQ as the primary bus transport
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(_rabbitMqHost, "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                cfg.ReceiveEndpoint(_rabbitMqQueueName, e =>
                {
                    e.ConfigureConsumer<RabbitMqMessageConsumer>(context);
                });
            });

            // Kafka Rider on top of the RabbitMQ bus
            x.AddRider(rider =>
            {
                rider.AddConsumer<KafkaMessageConsumer>();
                rider.AddProducer<KafkaMessage>(_kafkaTopic);

                rider.UsingKafka((context, k) =>
                {
                    k.Host(_kafkaBroker);

                    k.TopicEndpoint<KafkaMessage>(_kafkaTopic, "masstransit-test-group", e =>
                    {
                        e.ConfigureConsumer<KafkaMessageConsumer>(context);
                    });
                });
            });
        });

        // InMemory as a second bus via MultiBus (registered through DI so it gets instrumented)
        builder.Services.AddMassTransit<IInMemoryBus>(x =>
        {
            x.AddConsumer<InMemoryMessageConsumer>();

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        var configuredPort = ResolvePort();
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(configuredPort);
        });

        var app = builder.Build();

        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;
        app.Logger.LogInformation("Effective server addresses: {Addresses}",
            string.Join(", ", addresses ?? Array.Empty<string>()));

        CreatePidFile();

        // Wait for shutdown signal
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var tcs = new TaskCompletionSource();
        lifetime.ApplicationStopping.Register(() => tcs.SetResult());
        await tcs.Task;
    }

    public static string GetKafkaBootstrapServer()
    {
        var broker = Environment.GetEnvironmentVariable("MASSTRANSIT_KAFKA_BROKER") ?? "kafka-broker";
        return $"{broker}:9092";
    }

    public static string GetRabbitMqQueueName() => _rabbitMqQueueName;

    private static async Task CreateKafkaTopicAsync()
    {
        var config = new AdminClientConfig { BootstrapServers = _kafkaBroker };
        using var adminClient = new AdminClientBuilder(config).Build();
        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = _kafkaTopic, ReplicationFactor = 1, NumPartitions = 1 }
            });
        }
        catch (CreateTopicsException e)
        {
            Console.WriteLine($"Topic creation note: {e.Results[0].Error.Reason}");
        }
    }

    private static int ResolvePort()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("MASSTRANSIT_TESTAPP_PORT"), out var p) && p > 0 && p < 65536)
            return p;
        return 80;
    }

    public static void CreatePidFile()
    {
        var pidFileNameAndPath = Path.Combine(
            Environment.GetEnvironmentVariable("NEW_RELIC_LOG_DIRECTORY") ?? "/app/logs",
            "containerizedapp.pid");
        var pid = Environment.ProcessId;
        using var file = File.CreateText(pidFileNameAndPath);
        file.WriteLine(pid);
    }
}
