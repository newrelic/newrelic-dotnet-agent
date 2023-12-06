// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
        public static Producer Producer;
        public static Consumer Consumer;

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            // listen to any ip on port 80 for http
            IPEndPoint ipEndPointHttp = new IPEndPoint(IPAddress.Any, 80);
            builder.WebHost.UseUrls($"http://{ipEndPointHttp}");

            var app = builder.Build();

            SetupKafka(app.Logger);
            var cTask = Task.Run(() => Consumer.StartConsuming());

            // Configure the HTTP request pipeline.
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();

            CreatePidFile();

            await app.WaitForShutdownAsync();
        }

        public static void SetupKafka(ILogger logger)
        {
            Thread.Sleep(15 * 1000); // Waiting for Kafka to get ready

            var broker = Environment.GetEnvironmentVariable("NEW_RELIC_KAFKA_BROKER_NAME");
            var kafkaConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
            kafkaConfig["bootstrap.servers"] = $"{broker}:9092";
            kafkaConfig["group.id"] = "kafka-dotnet-getting-started";
            kafkaConfig["auto.offset.reset"] = "earliest";
            kafkaConfig["dotnet.cancellation.delay.max.ms"] = "10000";

            _topic = GenerateTopic();
            Producer = new Producer(kafkaConfig, _topic, logger);
            Producer.CreateTopic(kafkaConfig);
            Consumer = new Consumer(kafkaConfig, _topic, logger);
        }

        public static void CreatePidFile()
        {
            var pidFileNameAndPath = Path.Combine(Environment.GetEnvironmentVariable("NEWRELIC_LOG_DIRECTORY"), "containerizedapp.pid");
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
