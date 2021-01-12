// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETFRAMEWORK
using System;
using System.Linq;
using System.Messaging;

namespace NewRelic.Agent.IntegrationTests.Shared.Util
{
    public static class MessageQueueUtil
    {
        /// <summary>
        /// Creates a instance of an MSMQ queue - this can only be done for local queues not distributed
        /// If the queue already exists, purge it.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="isTransactional"></param>
        public static void CreateEmptyQueue(string queueName, bool isTransactional = false)
        {
            //We create the queue name here because this operation is only allowed on the current host and not remote ones.
            var privateQueueName = "private$\\" + queueName;

            MessageQueue queueToCreate =
                MessageQueue.GetPrivateQueuesByMachine(Environment.MachineName)
                    .SingleOrDefault(x => x.QueueName.EndsWith(queueName.ToLower()));

            if (queueToCreate == null)
            {
                var localQueue = MessageQueue.Create(".\\" + privateQueueName, isTransactional);
                localQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow);
            }
            else
            {
                queueToCreate.Purge();
            }
        }
    }
}
#endif
