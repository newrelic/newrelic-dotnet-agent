// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.Linq;
using System.Messaging;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    public class MSMQExerciser
    {
        private string GetQueueCreationName(int num) => string.Format(@"private$\nrtestqueue{0}", num);

        private string GetQueueName(int num) => string.Format(@"FormatName:DIRECT=OS:{0}\{1}", Environment.MachineName, GetQueueCreationName(num));

        [LibraryMethod]
        public void Create(int queueNum)
        {
            string queueName = GetQueueCreationName(queueNum);

            if (
                MessageQueue.GetPrivateQueuesByMachine(Environment.MachineName)
                    .SingleOrDefault(x => x.QueueName.EndsWith(queueName.ToLower())) == null)
            {
                var localQueue = MessageQueue.Create(".\\" + queueName);
                localQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow);
            }
        }

        [LibraryMethod]
        [Transaction]
        public void Send(int queueNum, bool ignoreThisTransaction = false)
        {
            if (ignoreThisTransaction)
                NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
            var queue = new MessageQueue(GetQueueName(queueNum));
            var message = new Message { Body = "Message Queues Testing" };
            queue.Send(message);
            queue.Close();

            ConsoleMFLogger.Info("Sent a message via MSMQ");
        }

        [LibraryMethod]
        [Transaction]
        public void Receive(int queueNum)
        {
            string messageReceived;

            var queue = new MessageQueue(GetQueueName(queueNum));
            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

            var message = queue.Receive();
            queue.Close();

            messageReceived = message.Body.ToString();

            ConsoleMFLogger.Info(messageReceived);
        }

        [LibraryMethod]
        [Transaction]
        public void Peek(int queueNum)
        {
            string messageReceived;
            var queue = new MessageQueue(GetQueueName(queueNum));
            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

            var message = queue.Peek();

            queue.Close();
            messageReceived = message.Body.ToString();

            ConsoleMFLogger.Info(messageReceived);
        }

        [LibraryMethod]
        [Transaction]
        public void Purge(int queueNum)
        {
            var queue = new MessageQueue(GetQueueName(queueNum));
            queue.Purge();
            queue.Close();

            ConsoleMFLogger.Info("Purged MSMQ queue");
        }
    }
}
#endif
