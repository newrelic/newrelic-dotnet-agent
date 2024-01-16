// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Transactions
{
    [TestFixture]
    public class ImmutableTransactionTests
    {
        [Test]
        public void ShouldBeAWebTransaction()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsWebTransaction("category", "name")
                .Build();

            Assert.That(transaction.IsWebTransaction());
        }

        [Test]
        public void ShouldNotBeAWebTransaction()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsOtherTransaction("category", "name")
                .Build();

            ClassicAssert.False(transaction.IsWebTransaction());
        }

        [Test]
        public void ResponseTimeOrDurationShouldContainResponseTimeForWebTransactions()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsWebTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithResponseTime(TimeSpan.FromSeconds(1))
                .Build();

            ClassicAssert.AreEqual(TimeSpan.FromSeconds(1), transaction.ResponseTimeOrDuration);
        }

        [Test]
        public void ResponseTimeOrDurationShouldBeADurationForWebTransactionsWithoutAResponseTime()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsWebTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithNoResponseTime()
                .Build();

            ClassicAssert.AreEqual(TimeSpan.FromSeconds(5), transaction.ResponseTimeOrDuration);
        }

        [Test]
        public void ResponseTimeOrDurationIsAlwaysDurationForOtherTransactions()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsOtherTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithResponseTime(TimeSpan.FromSeconds(1))
                .Build();

            ClassicAssert.AreEqual(TimeSpan.FromSeconds(5), transaction.ResponseTimeOrDuration);
        }
    }
}
