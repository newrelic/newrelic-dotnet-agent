/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using Amazon.Lambda.Core;
using System;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class Logger
    {
        public static void Log(string message, bool rawLogging = true, string level = "INFO")
        {
            if (rawLogging)
            {
                LambdaLogger.Log(message + Environment.NewLine);
            }
            else
            {
                LambdaLogger.Log($"{Timestamp()} NewRelic {level}: {message}{Environment.NewLine}");
            }

            // 2019-02-12 16:06:25,422 NewRelic INFO: 
            string Timestamp()
            {
                return DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss,fff");
            }

        }
    }
}
