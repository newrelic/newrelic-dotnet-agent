using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class MessageBrokerTransactionName : ITransactionName
    {
        [NotNull]
        public readonly string DestinationType;
        [NotNull]
        public readonly string BrokerVendorName;
        [CanBeNull]
        public readonly string Destination;

        public MessageBrokerTransactionName([NotNull] String destinationType, [NotNull] String brokerVendorName, [CanBeNull] String destination)
        {
            DestinationType = destinationType;
            BrokerVendorName = brokerVendorName;
            Destination = destination;
        }

        public Boolean IsWeb { get { return true; } }
    }
}
