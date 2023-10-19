// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KafkaTestApp
{
    public class Producer
    {
        private readonly string _topic;
        private readonly IProducer<string, string> _producer;
        private readonly ILogger _logger;

        public Producer(IConfiguration configuration, string topic, ILogger logger)
        {
            _topic = topic;
            _producer = new ProducerBuilder<string, string>(configuration.AsEnumerable()).Build();
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

            _ = _producer.ProduceAsync(_topic, new Message<string, string> { Key = user, Value = item }).Result;
            await Task.CompletedTask;
        }
    }
}
