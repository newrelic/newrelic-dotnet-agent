// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transactions
{
    [TestFixture]
    internal class TransactionLogEventTests
    {
        private Dictionary<string, object> _contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

        [Test]
        public void CanAddAndHarvestLogFromTransaction()
        {
            var logEvent = new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData);
            var transaction = TestTransactions.CreateDefaultTransaction();

            transaction.AddLogEvent(logEvent);
            var harvestedLogs = transaction.HarvestLogEvents();

            ClassicAssert.NotNull(harvestedLogs);
            ClassicAssert.AreEqual(1, harvestedLogs.Count);
            ClassicAssert.AreSame(logEvent, harvestedLogs.First());

        }

        [Test]
        public void AddLogEventReturnsFalse_AfterLogEventsHarvested()
        {
            var logEvent = new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData);
            var transaction = TestTransactions.CreateDefaultTransaction();

            transaction.HarvestLogEvents();

            var result = transaction.AddLogEvent(logEvent);

            ClassicAssert.False(result);
        }
    }
}
