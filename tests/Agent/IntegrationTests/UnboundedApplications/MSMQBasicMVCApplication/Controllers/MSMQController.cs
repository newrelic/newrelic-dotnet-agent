using System;
using System.Messaging;
using System.Web.Mvc;

namespace MSMQBasicMVCApplication.Controllers
{
    public class MSMQController : Controller
    {
		private const string QueueNameFormatter = @"FormatName:DIRECT=OS:{0}\private$\nrtestqueue";
		private const string QueueNameFormatterTransactional = @"FormatName:DIRECT=OS:{0}\private$\nrTestQueueTransactional";

		[HttpGet]
		public String Msmq_Send(bool ignoreThisTransaction = false)
		{
			if(ignoreThisTransaction)
				NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
			var queue = new MessageQueue(String.Format(QueueNameFormatter, Environment.MachineName));
			var message = new Message { Body = "Message Queues Testing" };
			queue.Send(message);
			queue.Close();

			return "Finished Sending a message via MSMQ";
		}

		[HttpGet]
		public String Msmq_Receive()
		{
			var messageReceived = String.Empty;

			var queue = new MessageQueue(String.Format(QueueNameFormatter, Environment.MachineName));
			queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });

			var message = queue.Receive();
			queue.Close();

			messageReceived = message.Body.ToString();

			return messageReceived;
		}

		[HttpGet]
		public String Msmq_Peek()
		{
			var messageReceived = String.Empty;
			var queue = new MessageQueue(String.Format(QueueNameFormatter, Environment.MachineName));
			queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });

			var message = queue.Peek();

			queue.Close();
			messageReceived = message.Body.ToString();

			return messageReceived;
		}

		[HttpGet]
		public String Msmq_Purge()
		{
			var queue = new MessageQueue(String.Format(QueueNameFormatter, Environment.MachineName));
			queue.Purge();
			queue.Close();

			return "Purged MSMQ queue";
		}
	}
}