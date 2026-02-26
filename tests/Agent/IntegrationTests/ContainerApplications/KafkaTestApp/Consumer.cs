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
                        // Errors are now only logged (caller is not notified).
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
            // Drain any leftover queued requests (they are fire-and-forget now).
            while (_requests.Reader.TryRead(out _)) { }
        }
    }

    [Transaction]
    private async Task ConsumeOneWithTimeoutAsync()
    {
        // Add statistics configuration to enable our metrics collection
        var configDict = _configuration.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        configDict["statistics.interval.ms"] = "5000"; // Enable statistics with 5 second interval
        configDict["group.id"] = "test-consumer-group"; // Ensure group.id is set

        using var consumer = new ConsumerBuilder<string, string>(configDict).Build();
        consumer.Subscribe(_topic);

        // Keep consumer alive for 15 seconds to allow multiple statistics callbacks (every 5 seconds)
        var startTime = DateTime.UtcNow;
        var maxDuration = TimeSpan.FromSeconds(15);
        var messagesConsumed = 0;
        var targetMessages = 1; // Still consume at least one message for test logic

        try
        {
            _logger.LogInformation("ConsumeOneWithTimeoutAsync: Starting long-lived consumer (15 seconds) to collect statistics");

            while (DateTime.UtcNow - startTime < maxDuration)
            {
                try
                {
                    // Poll for messages with shorter timeout to keep consumer active
                    var result = consumer.Consume(TimeSpan.FromSeconds(2));

                    if (result != null)
                    {
                        messagesConsumed++;
                        _logger.LogInformation("ConsumeOneWithTimeoutAsync: Consumed message '{MessageValue}' at: '{ResultTopicPartitionOffset}' (#{Count})",
                            result.Message.Value, result.TopicPartitionOffset, messagesConsumed);

                        // After consuming target messages, just keep polling to maintain connection
                        if (messagesConsumed >= targetMessages)
                        {
                            _logger.LogInformation("ConsumeOneWithTimeoutAsync: Target messages consumed, maintaining consumer for statistics collection...");
                        }

                        // Simulate processing time
                        await Task.Delay(Random.Shared.Next(500, 1000));
                    }
                    else
                    {
                        // No message available, but keep consumer alive for statistics
                        await Task.Delay(1000); // Wait 1 second before next poll
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(ex, "ConsumeOneWithTimeoutAsync: Consume exception (continuing)");
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("ConsumeOneWithTimeoutAsync: Completed long-lived consumer session. Messages consumed: {Count}", messagesConsumed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConsumeOneWithTimeoutAsync: Consumer error");
        }
        finally
        {
            consumer.Close();
        }
    }

    [Transaction]
    private async Task ConsumeOneWithCancellationTokenAsync()
    {
        // Add statistics configuration to enable our metrics collection
        var configDict = _configuration.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        configDict["statistics.interval.ms"] = "5000"; // Enable statistics with 5 second interval
        configDict["group.id"] = "test-consumer-group"; // Ensure group.id is set

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)); // 15 second timeout for long-lived consumer
        using var consumer = new ConsumerBuilder<string, string>(configDict).Build();
        consumer.Subscribe(_topic);

        // Keep consumer alive for 15 seconds to allow multiple statistics callbacks (every 5 seconds)
        var startTime = DateTime.UtcNow;
        var maxDuration = TimeSpan.FromSeconds(15);
        var messagesConsumed = 0;
        var targetMessages = 1; // Still consume at least one message for test logic

        try
        {
            _logger.LogInformation("ConsumeOneWithCancellationToken: Starting long-lived consumer (15 seconds) to collect statistics");

            while (DateTime.UtcNow - startTime < maxDuration && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Poll for messages with shorter timeout to keep consumer active
                    var result = consumer.Consume(TimeSpan.FromSeconds(2));

                    if (result != null)
                    {
                        messagesConsumed++;
                        _logger.LogInformation("ConsumeOneWithCancellationToken: Consumed message '{MessageValue}' at: '{ResultTopicPartitionOffset}' (#{Count})",
                            result.Message.Value, result.TopicPartitionOffset, messagesConsumed);

                        // After consuming target messages, just keep polling to maintain connection
                        if (messagesConsumed >= targetMessages)
                        {
                            _logger.LogInformation("ConsumeOneWithCancellationToken: Target messages consumed, maintaining consumer for statistics collection...");
                        }

                        // Simulate processing time - longer than timeout version to ensure different transaction timing
                        await Task.Delay(Random.Shared.Next(1000, 2000), cts.Token);
                    }
                    else
                    {
                        // No message available, but keep consumer alive for statistics
                        await Task.Delay(1000, cts.Token); // Wait 1 second before next poll
                    }
                }
                catch (ConsumeException ex) when (!cts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "ConsumeOneWithCancellationToken: Consume exception (continuing)");
                    await Task.Delay(1000, cts.Token);
                }
            }

            _logger.LogInformation("ConsumeOneWithCancellationToken: Completed long-lived consumer session. Messages consumed: {Count}", messagesConsumed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ConsumeOneWithCancellationToken: Consumer operation canceled after {Count} messages.", messagesConsumed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConsumeOneWithCancellationToken: Consumer error");
        }
        finally
        {
            consumer.Close();
        }
    }
}