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

        public KafkaController(ILogger<KafkaController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("produce")]
        public async Task<string> Produce()
        {
            await Program.Producer.Produce();
            return "Complete";
        }

        [HttpGet]
        [Route("produceasync")]
        public async Task<string> ProduceAsync()
        {
            await Program.Producer.ProduceAsync();
            return "Complete";
        }
    }
}
