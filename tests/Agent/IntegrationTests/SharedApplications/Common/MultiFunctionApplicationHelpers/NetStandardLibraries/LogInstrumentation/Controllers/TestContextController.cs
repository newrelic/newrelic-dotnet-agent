// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET8_0_OR_GREATER

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestContextController : ControllerBase
    {

        private readonly ILogger<TestController> _logger;

        public TestContextController(ILogger<TestController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public string Get(string message, string contextData)
        {

            var contextDataDict = GetDictionaryFromString(contextData);
            var loggerConfig = new LoggerConfiguration();

            loggerConfig
                .MinimumLevel.Information()
                .Enrich.With(new ContextDataEnricher(contextDataDict))
                .WriteTo.Console();

            var logger = loggerConfig.CreateLogger();

            logger.Information(message);
            return message;
        }

        private Dictionary<string, object> GetDictionaryFromString(string data)
        {
            var dict = new Dictionary<string, object>();
            if (data != null)
            {
                foreach (var item in data.Split(','))
                {
                    dict[item.Split('=')[0]] = item.Split('=')[1];
                }
            }
            return dict;
        }

    }
}

#endif
