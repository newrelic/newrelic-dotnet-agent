// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Transactions
{
    [TestFixture]
    internal class TransactionLogEventTests
    {
        [Test]
        public void CanAddAndHarvestLogFromTransaction()
        {
            var logEvent = new LogEventWireModel(1, "message1", "info", "spanid", "traceid");
            var transaction = TestTransactions.CreateDefaultTransaction();

            transaction.AddLogEvent(logEvent);
            var harvestedLogs = transaction.HarvestLogEvents();

            Assert.NotNull(harvestedLogs);
            Assert.AreEqual(1, harvestedLogs.Count);
            Assert.AreSame(logEvent, harvestedLogs.First());

        }

        [Test]
        public void AddLogEventReturnsFalse_AfterLogEventsHarvested()
        {
            var logEvent = new LogEventWireModel(1, "message1", "info", "spanid", "traceid");
            var transaction = TestTransactions.CreateDefaultTransaction();

            transaction.HarvestLogEvents();

            var result = transaction.AddLogEvent(logEvent);

            Assert.False(result);
        }
    }
}
