using System;
using System.Messaging;
using System.Threading;
using System.Web.Mvc;
using NServiceBusReceiver;

namespace NServiceBusBasicMvcApplication.Controllers
{
    public class MessageQueueController : Controller
    {
        [HttpGet]
        public string NServiceBus_Send()
        {
            // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
            var message = new SampleNServiceBusMessage(new Random().Next(), "Foo bar");

            // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
            MvcApplication.Bus.Send("NServiceBusReceiver", message);

            return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        }

        [HttpGet]
        public string NServiceBus_SendValid()
        {
            // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
            var message = new SampleNServiceBusMessage2(new Random().Next(), "Valid");

            // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
            MvcApplication.Bus.Send("NServiceBusReceiverHost", message);

            return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        }

        [HttpGet]
        public string NServiceBus_SendInvalid()
        {
            // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
            var message = new SampleNServiceBusMessage2(new Random().Next(), "Invalid", false);

            // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
            MvcApplication.Bus.Send("NServiceBusReceiverHost", message);

            return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        }
    }
}
