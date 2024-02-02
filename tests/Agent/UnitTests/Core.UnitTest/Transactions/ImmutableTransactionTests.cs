// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System;

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

            Assert.That(transaction.IsWebTransaction(), Is.True);
        }

        [Test]
        public void ShouldNotBeAWebTransaction()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsOtherTransaction("category", "name")
                .Build();

            Assert.That(transaction.IsWebTransaction(), Is.False);
        }

        [Test]
        public void ResponseTimeOrDurationShouldContainResponseTimeForWebTransactions()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsWebTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithResponseTime(TimeSpan.FromSeconds(1))
                .Build();

            Assert.That(transaction.ResponseTimeOrDuration, Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void ResponseTimeOrDurationShouldBeADurationForWebTransactionsWithoutAResponseTime()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsWebTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithNoResponseTime()
                .Build();

            Assert.That(transaction.ResponseTimeOrDuration, Is.EqualTo(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void ResponseTimeOrDurationIsAlwaysDurationForOtherTransactions()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsOtherTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithResponseTime(TimeSpan.FromSeconds(1))
                .Build();

            Assert.That(transaction.ResponseTimeOrDuration, Is.EqualTo(TimeSpan.FromSeconds(5)));
        }
    }
}
