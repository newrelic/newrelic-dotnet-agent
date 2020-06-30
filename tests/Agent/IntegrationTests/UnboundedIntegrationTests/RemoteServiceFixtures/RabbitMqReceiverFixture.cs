/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class RabbitMqReceiverFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "RabbitMqReceiverHost";
        private const string ExecutableName = "RabbitMqReceiverHost.exe";
        private const string TargetFramework = "net452";

        public string QueueName { get; }

        public RabbitMqReceiverFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Unbounded))
        {
            QueueName = $"integrationTestQueue-{Guid.NewGuid()}";
        }

        public void CreateQueueAndSendMessage()
        {
            IntegrationTestHelpers.RabbitMqUtils.CreateQueueAndSendMessage(QueueName);
        }

        public override void Dispose()
        {
            base.Dispose();
            IntegrationTestHelpers.RabbitMqUtils.DeleteQueuesAndExchanges(new List<string> { QueueName }, new List<string>());
        }
    }
}
