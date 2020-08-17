// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading;
using NewRelic.Agent.IntegrationTests.Shared;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMqLegacyReceiverHost
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"Process Id: {Process.GetCurrentProcess().Id}");
            Console.WriteLine($"Executing with arguments: {string.Join(" ", args)}");

            var queueNameArg = args.FirstOrDefault(x => x.StartsWith("--queue="));
            if (queueNameArg == null)
                throw new ArgumentException("Argument --queue={queueName} must be supplied");

            var portArg = args.FirstOrDefault(x => x.StartsWith("--port="));
            if (portArg == null)
                throw new ArgumentException("Argument --port={port} must be supplied");

            var queueName = queueNameArg.Split('=')[1];
            var port = portArg.Split('=')[1];

            var factory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: queueName,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine(" [x] Received {0}", message);
                };
                channel.BasicConsume(queue: queueName,
                    noAck: true,
                    consumer: consumer);

                var ewh = new EventWaitHandle(false, EventResetMode.ManualReset, "app_server_wait_for_all_request_done_" + port);
                CreatePidFile();
                ewh.WaitOne(TimeSpan.FromMinutes(5));
            }
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            var thisAssemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var pidFilePath = thisAssemblyPath + ".pid";
            var file = File.CreateText(pidFilePath);
            file.WriteLine(pid);
        }
    }
}
