// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KafkaTestApp.Controllers
{
    [ApiController]
    [Route("kafka")]
    public class KafkaController : ControllerBase
    {
        private readonly ILogger<KafkaController> _logger;
        private readonly Producer _producer;
        private readonly IConsumerSignalService _consumerSignal;

        public KafkaController(ILogger<KafkaController> logger,
                               Producer producer,
                               IConsumerSignalService consumerSignal)
        {
            _logger = logger;
            _producer = producer;
            _consumerSignal = consumerSignal;
        }

        [HttpGet("produce")]
        public async Task<string> Produce()
        {
            await _producer.Produce();
            return "Complete";
        }

        [HttpGet("produceasync")]
        public async Task<string> ProduceAsync()
        {
            await _producer.ProduceAsync();
            return "Complete";
        }

        [HttpGet("bootstrap_server")]
        public string GetBootstrapServer() => Program.GetBootstrapServer();

        [HttpGet("consumewithtimeout")]
        public async Task<string> ConsumeWithTimeoutAsync()
        {
            await _consumerSignal.RequestConsumeAsync(ConsumptionMode.Timeout);
            return "Complete";
        }

        [HttpGet("consumewithcancellationtoken")]
        public async Task<string> ConsumeWithCancellationTokenAsync()
        {
            await _consumerSignal.RequestConsumeAsync(ConsumptionMode.CancellationToken);
            return "Complete";
        }
    }
}
