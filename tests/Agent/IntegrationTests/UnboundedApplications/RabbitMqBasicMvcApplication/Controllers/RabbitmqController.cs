using System;
using System.Text;
using System.Web.Mvc;
using NewRelic.Agent.IntegrationTests.Shared;
using RabbitMQ.Client;

namespace RabbitMqBasicMvcApplication.Controllers
{
    public class RabbitMQController : Controller
    {
        private static readonly ConnectionFactory Factory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };

        [HttpGet]
        public String RabbitMQ_SendReceive(String queueName, String message)
        {
            var receiveMessage = String.Empty;
            if (String.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            using (var connection = Factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: queueName,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "",
                    routingKey: queueName,
                    basicProperties: null,
                    body: body);

                var basicGetResult = channel.BasicGet(queueName, true);

                receiveMessage = Encoding.UTF8.GetString(basicGetResult.Body);
            }

            return String.Format("method=Send,message={0},queueName={1}", receiveMessage, queueName);
        }

        [HttpGet]
        public String RabbitMQ_SendReceiveTopic(String exchangeName, String topicName, String message)
        {
            using (var connection = Factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                //Publish
                channel.ExchangeDeclare(exchange: exchangeName,
                                        type: "topic");

                var routingKey = topicName;
                var body = Encoding.UTF8.GetBytes(message);
                channel.BasicPublish(exchange: exchangeName,
                                     routingKey: routingKey,
                                     basicProperties: null,
                                     body: body);

                //Consume
                var queueName = channel.QueueDeclare().QueueName;

                channel.QueueBind(queue: queueName,
                                  exchange: exchangeName,
                                  routingKey: routingKey);

                var basicGetResult = channel.BasicGet(queueName, true);

                return $"method=SendReceiveTopic,exchangeName={exchangeName},queueName={queueName},topicName={topicName},message={message}";
            }
        }

        [HttpGet]
        public String RabbitMQ_SendReceiveTempQueue(String message)
        {
            var resultMessage = String.Empty;
            var queueName = String.Empty;

            using (var connection = Factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                queueName = channel.QueueDeclare().QueueName;

                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "",
                    routingKey: queueName,
                    basicProperties: null,
                    body: body);

                var basicGetResult = channel.BasicGet(queueName, true);
                resultMessage = Encoding.UTF8.GetString(basicGetResult.Body);

                return $"method=SendReceiveTempQueue,queueName={queueName}message={resultMessage}";
            }


        }

        [HttpGet]
        public String RabbitMQ_QueuePurge(string queueName)
        {
            using (var connection = Factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: queueName,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var body = Encoding.UTF8.GetBytes("I will be purged");

                channel.BasicPublish(exchange: "",
                    routingKey: queueName,
                    basicProperties: null,
                    body: body);

                var countMessages = channel.QueuePurge(queueName);

                return $"Purged {countMessages} message from queue: {queueName}";
            }
        }
    }
}
