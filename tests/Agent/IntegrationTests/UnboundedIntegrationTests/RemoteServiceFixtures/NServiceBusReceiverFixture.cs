/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using DotNet_Msmq_Shared;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class NServiceBusReceiverFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"NServiceBusReceiverHost";
        private const string ExecutableName = @"NServiceBusReceiverHost.exe";
        private const string TargetFramework = "net452";

        public NServiceBusReceiverFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Unbounded, false))
        {
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.error", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.retries", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.timeouts", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.timeoutsdispatcher", true);
        }

        public void SendValidAndInvalidMessages()
        {
            using (var messageSender = new NServiceBusBasicMvcApplicationFixture())
            {
                //This app fixture is only used to trigger the sending of messages, it is not the
                //app being profiled for the test. If it fails to start, the calls that
                //send the test messages will fail instead.
                messageSender.RemoteApplication.ValidateHostedWebCoreOutput = false;
                messageSender.TestLogger = TestLogger;

                //Saving methods to variables here to prevent access to a disposable object
                //in the exerciseApplication lambda. This just prevents the resharper warning,
                //because those methods will be called before the object is disposed anyways.
                Action sendValidMessage = messageSender.GetMessageQueue_NServiceBus_SendValid;
                Action sendInvalidMessage = messageSender.GetMessageQueue_NServiceBus_SendInvalid;

                messageSender.Actions(
                    exerciseApplication: () =>
                    {
                        sendValidMessage();
                        sendInvalidMessage();
                    }
                );

                messageSender.Initialize();
            }
        }
    }
}
