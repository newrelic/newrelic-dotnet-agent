using System;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using RabbitMQ.Client;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
	public class RabbitMqReceiverFixture : RemoteApplicationFixture
	{
		private readonly ConnectionFactory _factory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };
		private const String Message = "Hello, Spaceman.";
		private const String ApplicationDirectoryName = "RabbitMqReceiverHost";
		private const String ExecutableName = "RabbitMqReceiverHost.exe";
		private const String TargetFramework = "net452";

		public String QueueName { get; }

		public RabbitMqReceiverFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Unbounded))
		{
			QueueName = $"integrationTestQueue-{Guid.NewGuid()}";
		}

		public void CreateQueueAndSendMessage()
		{
			using (var connection = _factory.CreateConnection())
			{ 
				using (var channel = connection.CreateModel())
				{
					channel.QueueDeclare(queue: QueueName,
						durable: false,
						exclusive: false,
						autoDelete: false,
						arguments: null);

					var body = Encoding.UTF8.GetBytes(Message);

					channel.BasicPublish(exchange: "",
						routingKey: QueueName,
						basicProperties: null,
						body: body);
				}
			}
		}

		private void DeleteQueue()
		{
			using (var connection = _factory.CreateConnection())
			using (var channel = connection.CreateModel())
			{
				channel.QueueDeleteNoWait(QueueName, false, false);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			DeleteQueue();
		}
	}
}
