// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Providers.Wrapper.AzureFunction;

public class InProcessFunctionDetails
{
    public string TriggerType { get; set; }
    public bool IsWebTrigger => TriggerType == "http";
    public string FunctionName { get; set; }
    public ServiceBusTriggerDetails ServiceBusTriggerDetails { get; set; }
}
public  class ServiceBusTriggerDetails
{
    public string QueueName { get; set; }
    public string TopicName { get; set; }
    public string SubscriptionName { get; set; }
    public string Connection { get; set; } // not a connection string; just the name of the appSetting that contains the connection string

    public ServiceBusDestinationType DestinationType => !string.IsNullOrEmpty(QueueName) ? ServiceBusDestinationType.Queue : ServiceBusDestinationType.Topic;
}

public enum ServiceBusDestinationType
{
    Queue,
    Topic
}

