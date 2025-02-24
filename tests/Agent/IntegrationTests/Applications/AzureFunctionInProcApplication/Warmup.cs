// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureFunctionInProcApplication
{
    public static class Warmup
    {
        [FunctionName(nameof(Warmup))]
        public static void Run([WarmupTrigger] WarmupContext context, ILogger logger, PIDFileCreator pidFileCreator)
        {
            logger.LogInformation($"Warmup function triggered. Waiting for test completion.");
            pidFileCreator.WaitForTestCompletion();
        }
    }
}
