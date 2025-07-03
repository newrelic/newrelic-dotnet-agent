// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureServiceBus;

public abstract class AzureServiceBusWrapperBase : IWrapper
{
    protected const string BrokerVendorName = "ServiceBus";

    public virtual bool IsTransactionRequired => true; // only instrument service bus methods if we're already in a transaction

    public abstract CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo);

    public abstract AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction);

    /// <summary>
    /// Determines the type of message broker destination based on the provided name.
    /// </summary>
    /// <param name="queueOrTopicName">The name of the queue or topic to evaluate. Must not be null or empty.</param>
    /// <returns>A <see cref="MessageBrokerDestinationType"/> value indicating whether the destination is a queue or a topic.</returns>
    internal static MessageBrokerDestinationType GetMessageBrokerDestinationType(string queueOrTopicName)
    {
        // Default to Queue if the name is null or empty
        if (string.IsNullOrWhiteSpace(queueOrTopicName))
        {
            return MessageBrokerDestinationType.Queue;
        }

        // Topics have a subscription, queues do not
        var destinationType = queueOrTopicName.Contains("Subscriptions")
            ? MessageBrokerDestinationType.Topic
            : MessageBrokerDestinationType.Queue;

        return destinationType;
    }

    /// <summary>
    /// Retrieves the name of a queue or topic based on the specified destination type.
    /// </summary>
    /// <remarks>This method is useful for extracting the base name of a topic when working with message
    /// broker destinations  that include subscription paths.</remarks>
    /// <param name="destinationType">The type of the message broker destination, indicating whether the name represents a queue or a topic.</param>
    /// <param name="queueOrTopicName">The full name of the queue or topic. For topics, this may include a subscription path.</param>
    /// <returns>The name of the queue or topic. If the destination type is <see cref="MessageBrokerDestinationType.Topic"/>, 
    /// the subscription path is removed, returning only the topic name. Otherwise, the original name is returned.</returns>
    internal static string GetQueueOrTopicName(MessageBrokerDestinationType destinationType, string queueOrTopicName)
    {
        if (destinationType == MessageBrokerDestinationType.Topic)
        {
            // remove the "/Subscriptions/*" part to get just the topic name
            var index = queueOrTopicName.IndexOf("/Subscriptions/", StringComparison.Ordinal);
            if (index > 0)
            {
                return queueOrTopicName.Substring(0, index);
            }
        }

        return queueOrTopicName;
    }
}
