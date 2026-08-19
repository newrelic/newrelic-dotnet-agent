// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NewRelic.Agent.IntegrationTests.Shared;

namespace AzureFunctionApplication;

public class ServiceBusTriggerFunction
{
    // Longer than the functionTimeout the timeout fixture sets, so the host cancels the invocation
    // while the function is still running.
    private static readonly TimeSpan TimeoutFunctionSleepDuration = TimeSpan.FromMinutes(3);

    private readonly ILogger<ServiceBusTriggerFunction> _logger;

    // The isolated worker does not bind an ILogger function parameter; it injects the logger.
    public ServiceBusTriggerFunction(ILogger<ServiceBusTriggerFunction> logger)
    {
        _logger = logger;
    }

    [Function("ServiceBusTriggerFunction")]
    public void Run([ServiceBusTrigger(AzureServiceBusConfiguration.FuncTestQueueName)] ServiceBusReceivedMessage message)
    {
        var jsonMessage = JsonSerializer.Serialize(message, new JsonSerializerOptions() { WriteIndented = true });

        _logger.LogInformation($"C# ServiceBus queue trigger function processed message: {jsonMessage}");
    }

    /// <summary>
    /// Writes a log message and then throws. The agent ends the transaction in the wrapper's finally
    /// block, so the log written before the exception should still reach the collector.
    /// </summary>
    [Function("ServiceBusTriggerFunction_Throws")]
    public void RunAndThrow([ServiceBusTrigger(AzureServiceBusConfiguration.FuncTestFailureQueueNamePlaceholder)] ServiceBusReceivedMessage message)
    {
        _logger.LogInformation(AzureServiceBusConfiguration.FuncTestPreExceptionLogMessage);

        throw new InvalidOperationException("Intentional unhandled exception from ServiceBusTriggerFunction_Throws");
    }

    /// <summary>
    /// Writes a log message and then blocks past the host's functionTimeout. The invocation never
    /// returns, so the agent never ends the transaction that holds the log event.
    /// </summary>
    [Function("ServiceBusTriggerFunction_Timeout")]
    public void RunAndTimeout([ServiceBusTrigger(AzureServiceBusConfiguration.FuncTestFailureQueueNamePlaceholder)] ServiceBusReceivedMessage message)
    {
        _logger.LogInformation(AzureServiceBusConfiguration.FuncTestPreTimeoutLogMessage);

        Thread.Sleep(TimeoutFunctionSleepDuration);
    }

    /// <summary>
    /// Takes input from an HTTP trigger and sends a Service Bus message, which should then trigger the
    /// Service Bus trigger function the fixture started.
    /// </summary>
    [Function("HttpTrigger_SendServiceBusMessage")]
    [ServiceBusOutput(AzureServiceBusConfiguration.FuncTestFailureQueueNamePlaceholder)]
    public async Task<ServiceBusMessage> ServiceBusOutput([HttpTrigger(AuthorizationLevel.Admin, "post", Route = null)] HttpRequestData requestData)
    {
        string input;
        using (var reader = new StreamReader(requestData.Body))
        {
            input = await reader.ReadToEndAsync();
        }

        var serviceBusMessage = new ServiceBusMessage(input);

        _logger.LogInformation($"{AzureServiceBusConfiguration.FuncTestSendMessageLogMessage}: C# function processed: {input} and sent a ServiceBus message ");
        return serviceBusMessage;
    }
}
