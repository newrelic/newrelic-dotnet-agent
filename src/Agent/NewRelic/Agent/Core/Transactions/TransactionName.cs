// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Text;

namespace NewRelic.Agent.Core.Transactions
{
    public interface ITransactionName
    {
        bool IsWeb { get; }

        string Category { get; }

        string Name { get; }

        string UnprefixedName { get; }
    }


    public class TransactionName : ITransactionName
    {
        public bool IsWeb { get; }

        public string Category { get; }

        public string Name { get; }

        public string UnprefixedName
        {
            get
            {
                return Category + MetricNames.PathSeparator + Name;
            }
        }

        private TransactionName(bool isWeb, string category, string name)
        {
            IsWeb = isWeb;
            Category = category;
            Name = name;
        }

        public static TransactionName ForOtherTransaction(string category, string name)
        {
            var trxName = new TransactionName(false, category, name);
            return trxName;
        }

        public static TransactionName ForWebTransaction(WebTransactionType type, string name)
        {
            var categoryName = EnumNameCache<WebTransactionType>.GetName(type);
            return ForWebTransaction(categoryName, name);
        }
        public static TransactionName ForWebTransaction(string type, string name)
        {
            var trxName = new TransactionName(true, type, name);
            return trxName;
        }

        public static TransactionName ForUriTransaction(string normalizedUri)
        {
            var trxName = new TransactionName(true, MetricNames.Uri, normalizedUri);
            return trxName;
        }
        public static TransactionName ForBrokerTransaction(MessageBrokerDestinationType type, string vendor, string destination)
        {
            var trxName = new StringBuilder(vendor)
                .Append(MetricNames.PathSeparator)
                .Append(EnumNameCache<MessageBrokerDestinationType>.GetName(type))
                .Append(MetricNames.PathSeparator);

            if (string.IsNullOrWhiteSpace(destination))
            {
                trxName.Append(MetricNames.MessageBrokerTemp);
            }
            else
            {
                trxName.Append(MetricNames.MessageBrokerNamed)
                    .Append(MetricNames.PathSeparator)
                    .Append(destination);
            }

            return new TransactionName(false, MetricNames.Message, trxName.ToString());
        }

        public static TransactionName ForCustomTransaction(bool isWeb, string name, int maxLength)
        {
            // Note: In our public docs to tells users that they must prefix their metric names with "Custom/", but there's no mechanism that actually enforces this restriction, so there's no way to know whether it'll be there or not. For consistency, we'll just strip off "Custom/" if there's at all and then we know it's consistently not there.
            if (name.StartsWith("Custom/"))
            {
                name = name.Substring(7);
            }

            name = Clamper.ClampLength(name.Trim(), maxLength);
            if (name.Length <= 0)
            {
                throw new ArgumentException("A segment name cannot be an empty string.");
            }

            return new TransactionName(isWeb, MetricNames.Custom, name);
        }
    }
}
