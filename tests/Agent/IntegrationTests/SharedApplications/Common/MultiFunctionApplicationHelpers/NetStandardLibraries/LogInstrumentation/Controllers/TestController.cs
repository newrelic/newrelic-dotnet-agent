// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET6_0

using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {

        private readonly ILogger<TestController> _logger;

        public TestController(ILogger<TestController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public string Get(string logLevel, string message)
        {
            switch (logLevel.ToUpper())
            {
                case "INFO":
                    _logger.LogInformation(message);
                    break;
                case "DEBUG":
                    _logger.LogDebug(message);
                    break;
                case "WARN":
                    _logger.LogWarning(message);
                    break;
                case "ERROR":
                    _logger.LogError(message);
                    break;
                case "FATAL":
                    _logger.LogCritical(message);
                    break;
                default:
                    Console.WriteLine("Log level '" + logLevel + "' is not a tested log level.");
                    break;
            }

            return message;
        }
    }
}

#endif
