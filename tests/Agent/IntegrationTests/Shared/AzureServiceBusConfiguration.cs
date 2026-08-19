// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.IntegrationTests.Shared;

public class AzureServiceBusConfiguration
{
    public const string FuncTestQueueName = "azure_func_test_queue";

    // The failure-path tests get a queue per run. A timed-out invocation never completes, so its message
    // stays live in the queue; on a shared queue the next listener receives it and records an extra
    // transaction. The Functions host resolves %setting% in binding metadata from app settings, and the
    // fixture supplies the per-run value through an environment variable.
    public const string FuncTestFailureQueueNameSetting = "FuncTestFailureQueueName";
    public const string FuncTestFailureQueueNamePlaceholder = "%" + FuncTestFailureQueueNameSetting + "%";

    // Log messages written by the Azure Function Service Bus trigger test functions. The tests assert
    // on these to determine which log events reach the collector, so the app and the tests share them.
    public const string FuncTestSendMessageLogMessage = "AzureFunctionServiceBusTrigger-send-side-log-message";
    public const string FuncTestPreExceptionLogMessage = "AzureFunctionServiceBusTrigger-pre-exception-log-message";
    public const string FuncTestPreTimeoutLogMessage = "AzureFunctionServiceBusTrigger-pre-timeout-log-message";

    private static string _connectionString;

    public static string ConnectionString
    {
        get
        {
            if (_connectionString == null)
            {
                try
                {
                    var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("AzureServiceBusTests");
                    _connectionString = testConfiguration["ConnectionString"];
                }
                catch (Exception ex)
                {
                    throw new Exception("Azure Service Bus configuration is invalid.", ex);
                }
            }

            return _connectionString;
        }
    }
}