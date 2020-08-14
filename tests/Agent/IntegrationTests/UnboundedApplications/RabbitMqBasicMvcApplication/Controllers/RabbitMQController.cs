// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMqBasicMvcApplication.Controllers
{
    public class RabbitMQController : Controller
    {
        [HttpGet]
        public string RabbitMQ_SendReceive(string queueName, string message)
        {
            var receiveMessage = string.Empty;
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            MvcApplication.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            MvcApplication.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var basicGetResult = MvcApplication.Channel.BasicGet(queueName, true);

            receiveMessage = Encoding.UTF8.GetString(basicGetResult.Body);

            return string.Format("method=Send,message={0},queueName={1}", receiveMessage, queueName);
        }

        [HttpGet]
        public string RabbitMQ_SendReceive_HeaderExists(string queueName, string message)
        {
            var receiveMessage = string.Empty;
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            MvcApplication.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            MvcApplication.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var basicGetResult = MvcApplication.Channel.BasicGet(queueName, true);
            var headerExists = basicGetResult.BasicProperties.Headers.Any(header => header.Key.ToLowerInvariant() == "newrelic");

            receiveMessage = Convert.ToString(headerExists);

            return receiveMessage;
        }

        [HttpGet]
        public string RabbitMQ_SendReceive_HeaderValue(string queueName, string message)
        {
            var receiveMessage = string.Empty;
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            MvcApplication.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            MvcApplication.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var basicGetResult = MvcApplication.Channel.BasicGet(queueName, true);
            var headerValue = basicGetResult.BasicProperties.Headers.FirstOrDefault(header => header.Key.ToLowerInvariant() == "newrelic").Value;
            receiveMessage = Encoding.UTF8.GetString((byte[])headerValue);

            return receiveMessage;
        }

        [HttpGet]
        public async Task<string> RabbitMQ_SendReceiveWithEventingConsumer(string queueName, string message)
        {
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            MvcApplication.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            MvcApplication.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            using (var client = new HttpClient())
            {
                var requestUrl = HttpContext.Request.Url;
                var url = $"{requestUrl.Scheme}://{requestUrl.Host}:{requestUrl.Port}/RabbitMQ/RabbitMQ_ReceiveWithEventingConsumer?queueName={queueName}";
                return await client.GetStringAsync(url);
            }
        }

        [HttpGet]
        public string RabbitMQ_ReceiveWithEventingConsumer(string queueName)
        {
            using (var manualResetEvent = new ManualResetEventSlim(false))
            {
                var receivedMessage = string.Empty;
                var consumer = new EventingBasicConsumer(MvcApplication.Channel);
                consumer.Received += handler;
                MvcApplication.Channel.BasicConsume(queueName, true, consumer);
                manualResetEvent.Wait();
                return receivedMessage;

                void handler(object ch, BasicDeliverEventArgs basicDeliverEventArgs)
                {
                    receivedMessage = Encoding.UTF8.GetString(basicDeliverEventArgs.Body);
                    manualResetEvent.Set();
                }
            }
        }

        [HttpGet]
        public string RabbitMQ_SendReceiveTopic(string exchangeName, string topicName, string message)
        {
            //Publish
            MvcApplication.Channel.ExchangeDeclare(exchange: exchangeName,
                                    type: "topic");

            var routingKey = topicName;
            var body = Encoding.UTF8.GetBytes(message);
            MvcApplication.Channel.BasicPublish(exchange: exchangeName,
                                    routingKey: routingKey,
                                    basicProperties: null,
                                    body: body);

            //Consume
            var queueName = MvcApplication.Channel.QueueDeclare().QueueName;

            MvcApplication.Channel.QueueBind(queue: queueName,
                                exchange: exchangeName,
                                routingKey: routingKey);

            var basicGetResult = MvcApplication.Channel.BasicGet(queueName, true);

            return $"method=SendReceiveTopic,exchangeName={exchangeName},queueName={queueName},topicName={topicName},message={message}";
        }

        [HttpGet]
        public string RabbitMQ_SendReceiveTempQueue(string message)
        {
            var resultMessage = string.Empty;
            var queueName = MvcApplication.Channel.QueueDeclare().QueueName;

            var body = Encoding.UTF8.GetBytes(message);

            MvcApplication.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var basicGetResult = MvcApplication.Channel.BasicGet(queueName, true);
            resultMessage = Encoding.UTF8.GetString(basicGetResult.Body);

            return $"method=SendReceiveTempQueue,queueName={queueName}message={resultMessage}";
        }

        [HttpGet]
        public string RabbitMQ_QueuePurge(string queueName)
        {
            MvcApplication.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes("I will be purged");

            MvcApplication.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var countMessages = MvcApplication.Channel.QueuePurge(queueName);

            return $"Purged {countMessages} message from queue: {queueName}";
        }
    }
}
