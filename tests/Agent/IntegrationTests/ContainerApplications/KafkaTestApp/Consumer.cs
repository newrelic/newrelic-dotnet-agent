// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;

namespace KafkaTestApp
{
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
            using var consumer = new ConsumerBuilder<string, string>(_configuration.AsEnumerable()).Build();
            consumer.Subscribe(_topic);
            try
            {
                var result = consumer.Consume(120 * 1000);
                // Simulate some processing time
                int delay = Random.Shared.Next(500, 1000);
                await Task.Delay(delay);

                if (result != null)
                {
                    _logger.LogInformation("ConsumeOneWithTimeoutAsync: Consumed message '{MessageValue}' at: '{ResultTopicPartitionOffset}'.",
                        result.Message.Value, result.TopicPartitionOffset);
                }
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
            var cts = new CancellationTokenSource();
            using var consumer = new ConsumerBuilder<string, string>(_configuration.AsEnumerable()).Build();
            consumer.Subscribe(_topic);
            try
            {
                var result = consumer.Consume(cts.Token);
                // Simulate some processing time - longer than the timeout version to ensure this transaction is sampled
                int delay = Random.Shared.Next(1000, 2000);
                await Task.Delay(delay, cts.Token);

                _logger.LogInformation("ConsumeOneWithCancellationToken: Consumed message '{MessageValue}' at: '{ResultTopicPartitionOffset}'.",
                    result.Message.Value, result.TopicPartitionOffset);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("ConsumeOneWithCancellationToken: Consume operation canceled.");
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
}
