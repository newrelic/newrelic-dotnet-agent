// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
    private readonly IProducer<string, string> _customStatsProducer;
    private readonly ILogger _logger;

    // Background produce loop keeps librdkafka's per-topic batchsize window populated.
    // librdkafka's topic.batchsize is a ~5-second rolling window; with only the HTTP-triggered
    // produces (5 main + 2 custom-stats over ~15s of exercise), no stats snapshot's window
    // reliably contains a batch send, so producer-metrics/batch-size-avg ends up filtered at
    // AddMetric's WindowAvg>0 gate. A slow trickle of background sends ensures every 5-second
    // window sees at least one batch.
    //
    // These produces are NOT counted in messageBrokerProduce / TraceContext/Create/Success
    // metrics because KafkaProducerWrapper.IsTransactionRequired == true and Task.Run lacks a
    // transaction. They go to a SEPARATE topic from _topic so the work consumer — which
    // subscribes only to _topic — never sees them; otherwise the burst would fill the consume
    // buffer with DT-header-less messages, starving the real DT-header-bearing produces and
    // breaking Supportability/TraceContext/Accept/Success. The same _producer client handles
    // both topics, so librdkafka aggregates batchsize across them in stats.topics, which is
    // what AddProducerMetrics averages → batch-size-avg reliably > 0.
    private const string BurstTopicSuffix = "-burst";
    private readonly string _burstTopic;
    private readonly CancellationTokenSource _burstCts = new();
    private readonly Task _burstTask;

    public Producer(IConfiguration configuration, string topic, ILogger logger)
    {
        _topic = topic;
        _burstTopic = topic + BurstTopicSuffix;
        _logger = logger;

        var configDict = configuration.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        configDict["statistics.interval.ms"] = "5000";

        _producer = new ProducerBuilder<string, string>(configDict).Build();

        // Long-lived producer with a customer-installed statistics handler — exercises the
        // composite-handler path in KafkaBuilderWrapper. Built at construction so librdkafka
        // stats callbacks accumulate for the full lifetime of the test container; the
        // customstatisticsstatus endpoint can be queried at any time and will see a
        // monotonically growing ProducerCallbackCount.
        var customStatsBuilder = new ProducerBuilder<string, string>(configDict);
        customStatsBuilder.SetStatisticsHandler(CustomerStatisticsCallbacks.ProducerStatisticsHandler);
        _customStatsProducer = customStatsBuilder.Build();

        _burstTask = Task.Run(() => BackgroundBurstProduceLoop(_burstCts.Token));
    }

    private async Task BackgroundBurstProduceLoop(CancellationToken ct)
    {
        try
        {
            var counter = 0;
            while (!ct.IsCancellationRequested)
            {
                // Five rapid fire-and-forget produces per pass. linger.ms defaults to 5ms, so
                // librdkafka coalesces them into a single message set → batchsize.cnt > 0 in
                // the enclosing stats window.
                for (int i = 0; i < 5 && !ct.IsCancellationRequested; i++)
                {
                    try
                    {
                        _producer.Produce(_burstTopic,
                            new Message<string, string> { Key = "burst", Value = "burst-" + (++counter) },
                            null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("BackgroundBurstProduceLoop: Produce threw: {Message}", ex.Message);
                    }
                }

                try { await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BackgroundBurstProduceLoop: unexpected error — loop exiting");
        }
    }

    public void CreateTopic(IConfiguration configuration)
    {
        using (var adminClient = new AdminClientBuilder(configuration.AsEnumerable()).Build())
        {
            try
            {
                adminClient.CreateTopicsAsync(new TopicSpecification[] {
                    new TopicSpecification { Name = _topic, ReplicationFactor = 1, NumPartitions = 1 },
                    new TopicSpecification { Name = _burstTopic, ReplicationFactor = 1, NumPartitions = 1 }
                }).Wait(10 * 1000);
            }
            catch (CreateTopicsException e)
            {
                foreach (var result in e.Results)
                {
                    _logger.LogInformation($"An error occured creating topic {result.Topic}: {result.Error.Reason}");
                }
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
    /// Produces via the long-lived producer whose builder had a customer statistics handler
    /// installed before Build(). The long-lived client means librdkafka statistics callbacks
    /// have been firing on the customer's handler since container startup — so the callback
    /// count visible via /customstatisticsstatus is reliably > 0 by the time the test reads it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task ProduceWithCustomStatistics()
    {
        var user = "customStatsUser";
        var item = "customStatsItem";
        await _customStatsProducer.ProduceAsync(_topic, new Message<string, string> { Key = user, Value = item });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task ProduceAsyncWithExistingHeaders()
    {
        var user = "asyncExistingHeadersUser";
        var item = "asyncExistingHeadersItem";

        var message = new Message<string, string>
        {
            Key = user,
            Value = item,
            Headers = new Headers
            {
                { "traceparent", Encoding.ASCII.GetBytes("00-stale0000000000000000000000000-stale000000000-01") },
                { "tracestate", Encoding.ASCII.GetBytes("stale=value") },
                { "newrelic", Encoding.ASCII.GetBytes("stale-newrelic-payload") }
            }
        };

        await _producer.ProduceAsync(_topic, message);
    }
}

/// <summary>
/// Tracks customer statistics callback invocations for the composite-handler integration test.
/// Counters are monotonic across the process lifetime — incremented with Interlocked so
/// callbacks on the librdkafka poll thread stay correct under concurrent producer+consumer use.
/// The integration test asserts only that counts are > 0, so never resetting is safe.
/// </summary>
public static class CustomerStatisticsCallbacks
{
    private static int _producerCallbackCount;
    private static int _consumerCallbackCount;

    public static int ProducerCallbackCount => Volatile.Read(ref _producerCallbackCount);
    public static int ConsumerCallbackCount => Volatile.Read(ref _consumerCallbackCount);

    public static void ProducerStatisticsHandler(IProducer<string, string> producer, string statistics)
    {
        var count = Interlocked.Increment(ref _producerCallbackCount);
        Console.WriteLine($"Customer Producer Statistics Handler Called: Count={count}, JSON length={statistics?.Length ?? 0}");
    }

    public static void ConsumerStatisticsHandler(IConsumer<string, string> consumer, string statistics)
    {
        var count = Interlocked.Increment(ref _consumerCallbackCount);
        Console.WriteLine($"Customer Consumer Statistics Handler Called: Count={count}, JSON length={statistics?.Length ?? 0}");
    }
}
