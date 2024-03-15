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
        public void Setup(string queueNameRoot)
        {
            MessageQueueUtil.CreateEmptyQueue($"{queueNameRoot}", true);
            MessageQueueUtil.CreateEmptyQueue($"{queueNameRoot}.audit", true);
            MessageQueueUtil.CreateEmptyQueue($"{queueNameRoot}.error", true);
            MessageQueueUtil.CreateEmptyQueue($"{queueNameRoot}.retries", true);
            MessageQueueUtil.CreateEmptyQueue($"{queueNameRoot}.timeouts", true);
            MessageQueueUtil.CreateEmptyQueue($"{queueNameRoot}.timeoutsdispatcher", true);

            var defaultFactory = LogManager.Use<DefaultFactory>();
            defaultFactory.Level(LogLevel.Error);
        }
    }
}

#endif
