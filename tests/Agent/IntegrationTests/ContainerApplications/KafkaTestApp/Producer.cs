// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KafkaTestApp;

public class Producer
{
    private readonly string _topic;
    private readonly IProducer<string, string> _producer;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public Producer(IConfiguration configuration, string topic, ILogger logger)
    {
        _topic = topic;
        _configuration = configuration;

        // Add statistics configuration to enable our metrics collection
        var configDict = configuration.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        configDict["statistics.interval.ms"] = "5000"; // Enable statistics with 5 second interval

        _producer = new ProducerBuilder<string, string>(configDict).Build();
        _logger = logger;
    }

    public void CreateTopic(IConfiguration configuration)
    {
        using (var adminClient = new AdminClientBuilder(configuration.AsEnumerable()).Build())
        {
            try
            {
                adminClient.CreateTopicsAsync(new TopicSpecification[] {
                    new TopicSpecification { Name = _topic, ReplicationFactor = 1, NumPartitions = 1 }
                }).Wait(10 * 1000);
            }
            catch (CreateTopicsException e)
            {
                _logger.LogInformation($"An error occured creating topic {e.Results[0].Topic}: {e.Results[0].Error.Reason}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task Produce()
    {
        var user = "syncTestUser";
        var item = "syncTestItem";

        _producer.Produce(_topic, new Message<string, string> { Key = user, Value = item },
            (deliveryReport) =>
            {
                if (deliveryReport.Error.Code != ErrorCode.NoError)
                {
                    _logger.LogInformation($"Failed to deliver message: {deliveryReport.Error.Reason}");
                }
            });

        await Task.CompletedTask;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task ProduceAsync()
    {
        var user = "asyncTestUser";
        var item = "asyncTestItem";

        await _producer.ProduceAsync(_topic, new Message<string, string> { Key = user, Value = item });
    }

    /// <summary>
    /// Test method to verify that customer statistics handlers work alongside our metrics collection.
    /// This creates a producer with a custom statistics handler to test our composite handler pattern.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task ProduceWithCustomStatistics()
    {
        // Track if the customer's statistics handler was called
        CustomerStatisticsCallbacks.ResetCounters();

        // Add statistics configuration using same config as main producer
        var configDict = _configuration.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        configDict["statistics.interval.ms"] = "5000"; // Enable statistics with 5 second interval

        // Create producer builder with customer statistics handler
        var builder = new ProducerBuilder<string, string>(configDict);

        // Set up customer's statistics handler BEFORE Build() - this tests our composite pattern
        builder.SetStatisticsHandler(CustomerStatisticsCallbacks.ProducerStatisticsHandler);

        using var customProducer = builder.Build();

        // Produce a message
        var user = "customStatsUser";
        var item = "customStatsItem";
        await customProducer.ProduceAsync(_topic, new Message<string, string> { Key = user, Value = item });

        _logger.LogInformation($"ProduceWithCustomStatistics: Produced message with custom statistics handler. Customer callback count: {CustomerStatisticsCallbacks.ProducerCallbackCount}");

        // Keep producer alive for at least one statistics interval to ensure callbacks trigger
        await Task.Delay(6000); // Wait 6 seconds to ensure statistics callback fires

        _logger.LogInformation($"ProduceWithCustomStatistics: Completed. Final customer callback count: {CustomerStatisticsCallbacks.ProducerCallbackCount}");
    }
}

/// <summary>
/// Helper class to track customer statistics callback invocations for testing.
/// </summary>
public static class CustomerStatisticsCallbacks
{
    public static int ProducerCallbackCount { get; private set; }
    public static int ConsumerCallbackCount { get; private set; }
    public static string LastProducerStatistics { get; private set; }
    public static string LastConsumerStatistics { get; private set; }

    public static void ResetCounters()
    {
        ProducerCallbackCount = 0;
        ConsumerCallbackCount = 0;
        LastProducerStatistics = null;
        LastConsumerStatistics = null;
    }

    public static void ProducerStatisticsHandler(IProducer<string, string> producer, string statistics)
    {
        ProducerCallbackCount++;
        LastProducerStatistics = statistics;

        // Log that customer handler was called (simulating customer behavior)
        Console.WriteLine($"Customer Producer Statistics Handler Called: Count={ProducerCallbackCount}, JSON length={statistics?.Length ?? 0}");
    }

    public static void ConsumerStatisticsHandler(IConsumer<string, string> consumer, string statistics)
    {
        ConsumerCallbackCount++;
        LastConsumerStatistics = statistics;

        // Log that customer handler was called (simulating customer behavior)
        Console.WriteLine($"Customer Consumer Statistics Handler Called: Count={ConsumerCallbackCount}, JSON length={statistics?.Length ?? 0}");
    }
}