// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using NewRelic.Agent.IntegrationTests.Shared;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

/// <summary>
/// Creates a Service Bus queue for the lifetime of one fixture and deletes it afterward, so a test that
/// leaves a message in the queue cannot affect the next test.
/// </summary>
public class ServiceBusQueueScope : IDisposable
{
    // Backstop for a run that dies before Dispose. The lowest value the service accepts is 5 minutes,
    // which is close to the waits these tests use, so this allows more headroom.
    private static readonly TimeSpan QueueAutoDeleteOnIdle = TimeSpan.FromMinutes(15);

    private readonly ServiceBusAdministrationClient _client;

    public string QueueName { get; }

    private ServiceBusQueueScope(ServiceBusAdministrationClient client, string queueName)
    {
        _client = client;
        QueueName = queueName;
    }

    /// <summary>
    /// Creates the queue. The caller supplies the name so that name generation, which needs no I/O, can
    /// happen in a fixture constructor while creation happens in Initialize.
    /// </summary>
    public static ServiceBusQueueScope Create(string queueName)
    {
        var client = new ServiceBusAdministrationClient(AzureServiceBusConfiguration.ConnectionString);
        var options = new CreateQueueOptions(queueName) { AutoDeleteOnIdle = QueueAutoDeleteOnIdle };

        try
        {
            client.CreateQueueAsync(options).GetAwaiter().GetResult();
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Treat an existing queue as created. The name carries a GUID, so the only way to reach this
            // is a repeated create for the same run, and the scope still owns the queue either way.
        }
        catch (Exception exception)
        {
            throw new Exception(
                $"Could not create Service Bus queue '{queueName}'. The connection string in the AzureServiceBusTests test configuration needs Manage rights on the namespace.",
                exception);
        }

        return new ServiceBusQueueScope(client, queueName);
    }

    public void Dispose()
    {
        try
        {
            _client.DeleteQueueAsync(QueueName).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Throwing here would replace the test result with a cleanup failure. AutoDeleteOnIdle
            // removes the queue if this delete does not.
        }
    }
}
