// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class MessageBrokerTransactionName : ITransactionName
    {
        public readonly string DestinationType;
        public readonly string BrokerVendorName;
        public readonly string Destination;

        public MessageBrokerTransactionName(string destinationType, string brokerVendorName, string destination)
        {
            DestinationType = destinationType;
            BrokerVendorName = brokerVendorName;
            Destination = destination;
        }

        public bool IsWeb { get { return true; } }
    }
}
