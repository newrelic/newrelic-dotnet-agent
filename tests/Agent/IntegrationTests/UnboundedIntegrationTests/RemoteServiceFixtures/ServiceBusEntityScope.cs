// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using NewRelic.Agent.IntegrationTests.Shared;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;

/// <summary>
/// Creates a Service Bus queue or topic for the lifetime of one fixture and deletes it afterward, so a
/// test run that leaves the entity behind cannot leak it.
/// </summary>
public class ServiceBusEntityScope : IDisposable
{
    // Backstop for a run that dies before Dispose. The lowest value the service accepts is 5 minutes,
    // which is close to the waits these tests use, so this allows more headroom.
    private static readonly TimeSpan EntityAutoDeleteOnIdle = TimeSpan.FromMinutes(15);

    private readonly ServiceBusAdministrationClient _client;
    private readonly bool _isTopic;
    private bool _disposed;

    public string EntityName { get; }

    private ServiceBusEntityScope(ServiceBusAdministrationClient client, string entityName, bool isTopic)
    {
        _client = client;
        EntityName = entityName;
        _isTopic = isTopic;
    }

    /// <summary>
    /// Creates the queue or topic named by destinationType ("Queue" or "Topic"). The caller supplies the
    /// name so that name generation, which needs no I/O, can happen in a fixture constructor while
    /// creation happens in Initialize.
    /// </summary>
    public static ServiceBusEntityScope Create(string destinationType, string entityName)
    {
        var isTopic = destinationType switch
        {
            "Queue" => false,
            "Topic" => true,
            _ => throw new ArgumentException($"Unknown destination type '{destinationType}'.", nameof(destinationType))
        };

        var client = new ServiceBusAdministrationClient(AzureServiceBusConfiguration.ConnectionString);

        try
        {
            if (isTopic)
            {
                var options = new CreateTopicOptions(entityName) { AutoDeleteOnIdle = EntityAutoDeleteOnIdle };
                client.CreateTopicAsync(options).GetAwaiter().GetResult();
                client.CreateSubscriptionAsync(entityName, AzureServiceBusConfiguration.SubscriptionName).GetAwaiter().GetResult();
            }
            else
            {
                var options = new CreateQueueOptions(entityName) { AutoDeleteOnIdle = EntityAutoDeleteOnIdle };
                client.CreateQueueAsync(options).GetAwaiter().GetResult();
            }
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Treat an existing entity as created. The name carries a GUID, so the only way to reach this
            // is a repeated create for the same run, and the scope still owns the entity either way.
        }
        catch (Exception exception)
        {
            throw new Exception(
                $"Could not create Service Bus {destinationType.ToLowerInvariant()} '{entityName}'. The connection string in the AzureServiceBusTests test configuration needs Manage rights on the namespace.",
                exception);
        }

        return new ServiceBusEntityScope(client, entityName, isTopic);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_isTopic)
            {
                // Deleting the topic also removes its subscriptions.
                _client.DeleteTopicAsync(EntityName).GetAwaiter().GetResult();
            }
            else
            {
                _client.DeleteQueueAsync(EntityName).GetAwaiter().GetResult();
            }
        }
        catch (Exception)
        {
            // Throwing here would replace the test result with a cleanup failure. AutoDeleteOnIdle
            // removes the entity if this delete does not.
        }
    }
}
