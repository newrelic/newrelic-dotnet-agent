// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;                 // Added
using Microsoft.AspNetCore.Hosting.Server.Features;        // Added
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KafkaTestApp
{
    public class Program
    {
        private const int TopicNameLength = 15;
        private static string _topic;
        private static IConfiguration _kafkaConfig;

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add controllers.
            builder.Services.AddControllers();

            // Build Kafka config + topic before DI registrations that depend on them.
            _kafkaConfig = BuildKafkaConfiguration();
            _topic = GenerateTopic();

            // Register Producer + Consumer (BackgroundService) + signal service.
            builder.Services.AddSingleton(_kafkaConfig);
            builder.Services.AddSingleton<Producer>(sp =>
                new Producer(_kafkaConfig, _topic, sp.GetRequiredService<ILogger<Producer>>()));
            builder.Services.AddSingleton<IConsumerSignalService>(sp =>
                new Consumer(_kafkaConfig, _topic, sp.GetRequiredService<ILogger<Consumer>>()));
            builder.Services.AddHostedService(sp => (Consumer)sp.GetRequiredService<IConsumerSignalService>());

            var configuredPort = ResolvePort();
            builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(configuredPort);
            });


            var app = builder.Build();

            // Create topic once Producer is resolved.
            var producer = app.Services.GetRequiredService<Producer>();
            producer.CreateTopic(_kafkaConfig);

            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();

            // Post-start diagnostic logging of bound addresses.
            var addresses = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses;
            app.Logger.LogInformation("Effective server addresses: {Addresses}", string.Join(", ", addresses ?? Array.Empty<string>()));
            if (addresses is { } && !ContainsPort(addresses, configuredPort))
            {
                app.Logger.LogWarning("Expected to be listening on port {Port}, but reported addresses are: {Addresses}", configuredPort, string.Join(", ", addresses));
            }

            CreatePidFile();

            await app.WaitForShutdownAsync();
        }

        private static bool ContainsPort(IEnumerable<string> addresses, int port)
        {
            foreach (var a in addresses)
            {
                if (a.EndsWith($":{port}", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static int ResolvePort()
        {
            // Optional override (defaults to 80). If invalid, fallback.
            if (int.TryParse(Environment.GetEnvironmentVariable("KAFKA_TESTAPP_PORT"), out var p) && p > 0 && p < 65536)
            {
                return p;
            }
            return 80;
        }

        public static string GetBootstrapServer()
        {
            var broker = Environment.GetEnvironmentVariable("NEW_RELIC_KAFKA_BROKER_NAME");
            return $"{broker}:9092";
        }

        private static IConfiguration BuildKafkaConfiguration()
        {
            var dict = new Dictionary<string, string>
            {
                ["bootstrap.servers"] = GetBootstrapServer(),
                ["group.id"] = "kafka-dotnet-getting-started",
                ["auto.offset.reset"] = "earliest",
                ["dotnet.cancellation.delay.max.ms"] = "10000"
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        public static void CreatePidFile()
        {
            var pidFileNameAndPath = Path.Combine(Environment.GetEnvironmentVariable("NEW_RELIC_LOG_DIRECTORY"), "containerizedapp.pid");
            var pid = Environment.ProcessId;
            using var file = File.CreateText(pidFileNameAndPath);
            file.WriteLine(pid);
        }

        private static string GenerateTopic()
        {
            var providedTopic = Environment.GetEnvironmentVariable("NEW_RELIC_KAFKA_TOPIC");
            if (!string.IsNullOrEmpty(providedTopic))
            {
                return providedTopic;
            }

            var builder = new StringBuilder();
            var rnd = new Random();
            for (int i = 0; i < TopicNameLength; i++)
            {
                var shifter = Convert.ToInt32(Math.Floor(25 * rnd.NextDouble()));
                builder.Append(Convert.ToChar(shifter + 65));
            }
            return builder.ToString();
        }
    }
}
