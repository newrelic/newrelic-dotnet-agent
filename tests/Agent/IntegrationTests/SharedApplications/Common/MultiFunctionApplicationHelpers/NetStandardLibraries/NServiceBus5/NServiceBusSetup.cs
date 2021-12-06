// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET462

using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Util;
using NServiceBus;
using NServiceBus.Logging;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5
{
    [Library]
    public class NServiceBusSetup
    {
        [LibraryMethod]
        public void Setup()
        {
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.error", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.retries", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.timeouts", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.timeoutsdispatcher", true);

            var defaultFactory = LogManager.Use<DefaultFactory>();
            defaultFactory.Level(LogLevel.Error);
        }
    }
}

#endif
