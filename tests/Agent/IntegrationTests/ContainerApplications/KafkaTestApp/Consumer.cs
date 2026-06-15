// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;

namespace KafkaTestApp;

public class Consumer : BackgroundService, IConsumerSignalService
{
    private readonly string _topic;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Consumer> _logger;

    // Work consumer: serves the [Transaction]-scoped HTTP paths (timeout / cancellation).
    // Long-lived so successive HTTP requests reuse the same librdkafka client and stats
    // callbacks accumulate across drain cycles.
    private IConsumer<string, string> _workConsumer;

    // Custom-stats consumer: exercises the composite-handler path in KafkaBuilderWrapper by
    // installing a customer statistics handler before Build(). Runs a continuous background
    // poll loop so librdkafka statistics callbacks fire throughout the test. Not decorated
    // with [Transaction] — its purpose is callback coverage, not span/transaction emission.
    // Uses a distinct group.id so it reads independently of the work consumer.
    private IConsumer<string, string> _customStatsConsumer;
    private Task _customStatsPollTask;
    private CancellationTokenSource _customStatsPollCts;

    private sealed record ConsumeRequest(ConsumptionMode Mode);

    private readonly Channel<ConsumeRequest> _requests =
        Channel.CreateUnbounded<ConsumeRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        });

    public Consumer(IConfiguration configuration, string topic, ILogger<Consumer> logger)
    {
        _topic = topic;
        _configuration = configuration;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeWorkConsumer();
        InitializeCustomStatsConsumer();
        return base.StartAsync(cancellationToken);
    }

    private void InitializeWorkConsumer()
    {
        var configDict = _configuration.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        configDict["statistics.interval.ms"] = "5000";
        configDict["group.id"] = "test-consumer-group-work";

        _workConsumer = new ConsumerBuilder<string, string>(configDict).Build();
        _workConsumer.Subscribe(_topic);
        _logger.LogInformation("Work consumer initialized (group=test-consumer-group-work, topic={Topic})", _topic);
    }

    private void InitializeCustomStatsConsumer()
    {
        var configDict = _configuration.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        configDict["statistics.interval.ms"] = "5000";
        configDict["group.id"] = "test-consumer-group-customstats";

        var builder = new ConsumerBuilder<string, string>(configDict);
        // Customer handler installed BEFORE Build() so the KafkaBuilderWrapper sees an
        // existing StatisticsHandler and takes the Delegate.Combine composite path.
        builder.SetStatisticsHandler(CustomerStatisticsCallbacks.ConsumerStatisticsHandler);

        _customStatsConsumer = builder.Build();
        _customStatsConsumer.Subscribe(_topic);
        _logger.LogInformation("Custom-stats consumer initialized (group=test-consumer-group-customstats, topic={Topic})", _topic);

        _customStatsPollCts = new CancellationTokenSource();
        _customStatsPollTask = Task.Run(() => CustomStatsPollLoop(_customStatsPollCts.Token));
    }

    // Continuous non-[Transaction] poll. KafkaConsumerWrapper.IsTransactionRequired == true,
    // so these Consume() calls create no segments or consume metrics — the poll exists solely
    // to keep the librdkafka client active and firing statistics callbacks.
    private void CustomStatsPollLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _customStatsConsumer.Consume(TimeSpan.FromSeconds(1));
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(ex, "CustomStatsPollLoop: consume exception (continuing)");
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CustomStatsPollLoop: unexpected error — loop exiting");
        }
    }

    public Task RequestConsumeAsync(ConsumptionMode mode)
    {
        _logger.LogInformation("Queueing consume request ({Mode}).", mode);
        if (!_requests.Writer.TryWrite(new ConsumeRequest(mode)))
        {
            _logger.LogError("Unable to queue consume request ({Mode}) - channel is closed.", mode);
            return Task.FromException(new InvalidOperationException("Channel is closed."));
        }

        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Consumer background service is starting.");
            while (await _requests.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_requests.Reader.TryRead(out var req))
                {
                    _logger.LogInformation("Processing consume request ({Mode}).", req.Mode);
                    try
                    {
                        switch (req.Mode)
                        {
                            case ConsumptionMode.Timeout:
                                await ConsumeOneWithTimeoutAsync();
                                break;
                            case ConsumptionMode.CancellationToken:
                                await ConsumeOneWithCancellationTokenAsync();
                                break;
                        }
                        _logger.LogInformation("Completed consume request ({Mode}).", req.Mode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing consume request ({Mode}).", req.Mode);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            _logger.LogInformation("Consumer background service is stopping.");
            while (_requests.Reader.TryRead(out _)) { }

            if (_customStatsPollCts != null)
            {
                try { await _customStatsPollCts.CancelAsync().ConfigureAwait(false); } catch { /* best effort */ }
            }
            // Poll task was started via Task.Run on the threadpool; VSTHRD003 flags awaiting it here,
            // but there is no UI / single-threaded sync context in a BackgroundService so the deadlock
            // the analyzer guards against cannot occur. Suppressing is the correct action.
#pragma warning disable VSTHRD003
            try { if (_customStatsPollTask != null) await _customStatsPollTask.ConfigureAwait(false); } catch { /* best effort */ }
#pragma warning restore VSTHRD003

            try { _workConsumer?.Close(); } catch { /* best effort */ }
            try { _workConsumer?.Dispose(); } catch { /* best effort */ }
            try { _customStatsConsumer?.Close(); } catch { /* best effort */ }
            try { _customStatsConsumer?.Dispose(); } catch { /* best effort */ }
            try { _customStatsPollCts?.Dispose(); } catch { /* best effort */ }
        }
    }

    [Transaction]
    private async Task ConsumeOneWithTimeoutAsync()
    {
        var startTime = DateTime.UtcNow;
        var maxDuration = TimeSpan.FromSeconds(5);
        var messagesConsumed = 0;

        try
        {
            _logger.LogInformation("ConsumeOneWithTimeoutAsync: Polling work consumer for up to {Duration}s", maxDuration.TotalSeconds);

            while (DateTime.UtcNow - startTime < maxDuration)
            {
                try
                {
                    var result = _workConsumer.Consume(TimeSpan.FromSeconds(2));

                    if (result != null)
                    {
                        messagesConsumed++;
                        _logger.LogInformation("ConsumeOneWithTimeoutAsync: Consumed message '{MessageValue}' at: '{ResultTopicPartitionOffset}' (#{Count})",
                            result.Message.Value, result.TopicPartitionOffset, messagesConsumed);

                        await Task.Delay(Random.Shared.Next(500, 1000));
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(ex, "ConsumeOneWithTimeoutAsync: Consume exception (continuing)");
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("ConsumeOneWithTimeoutAsync: Completed. Messages consumed: {Count}", messagesConsumed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConsumeOneWithTimeoutAsync: Consumer error");
        }
    }

    [Transaction]
    private async Task ConsumeOneWithCancellationTokenAsync()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var startTime = DateTime.UtcNow;
        var maxDuration = TimeSpan.FromSeconds(5);
        var messagesConsumed = 0;

        try
        {
            _logger.LogInformation("ConsumeOneWithCancellationToken: Polling work consumer for up to {Duration}s", maxDuration.TotalSeconds);

            while (DateTime.UtcNow - startTime < maxDuration && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = _workConsumer.Consume(TimeSpan.FromSeconds(2));

                    if (result != null)
                    {
                        messagesConsumed++;
                        _logger.LogInformation("ConsumeOneWithCancellationToken: Consumed message '{MessageValue}' at: '{ResultTopicPartitionOffset}' (#{Count})",
                            result.Message.Value, result.TopicPartitionOffset, messagesConsumed);

                        await Task.Delay(Random.Shared.Next(1000, 2000), cts.Token);
                    }
                    else
                    {
                        await Task.Delay(1000, cts.Token);
                    }
                }
                catch (ConsumeException ex) when (!cts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "ConsumeOneWithCancellationToken: Consume exception (continuing)");
                    await Task.Delay(1000, cts.Token);
                }
            }

            _logger.LogInformation("ConsumeOneWithCancellationToken: Completed. Messages consumed: {Count}", messagesConsumed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ConsumeOneWithCancellationToken: Consumer operation canceled after {Count} messages.", messagesConsumed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConsumeOneWithCancellationToken: Consumer error");
        }
    }
}
