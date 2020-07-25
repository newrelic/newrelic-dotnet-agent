using System;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class MessageBrokerTransactionName : ITransactionName
    {
        public readonly string DestinationType;
        public readonly string BrokerVendorName;
        public readonly string Destination;

        public MessageBrokerTransactionName(String destinationType, String brokerVendorName, String destination)
        {
            DestinationType = destinationType;
            BrokerVendorName = brokerVendorName;
            Destination = destination;
        }

        public Boolean IsWeb { get { return true; } }
    }
}
