// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KafkaTestApp
{
    public class Consumer
    {
        private readonly string _topic;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public Consumer(IConfiguration configuration, string topic, ILogger logger)
        {
            _topic = topic;
            _configuration = configuration;
            _logger = logger;
        }

        public Task StartConsuming()
        {
            using (var consumer = new ConsumerBuilder<string, string>(_configuration.AsEnumerable()).Build())
            {
                consumer.Subscribe(_topic);
                try
                {
                    while (true)
                    {
                        _ = consumer.Consume(120 * 1000);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Consume operation canceled.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Consumer error");
                }
                finally
                {
                    consumer.Close();
                }
            }

            return Task.CompletedTask;
        }
    }
}
