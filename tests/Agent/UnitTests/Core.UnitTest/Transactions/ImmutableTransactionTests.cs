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

            Assert.True(transaction.IsWebTransaction());
        }

        [Test]
        public void ShouldNotBeAWebTransaction()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsOtherTransaction("category", "name")
                .Build();

            Assert.False(transaction.IsWebTransaction());
        }

        [Test]
        public void ResponseTimeOrDurationShouldContainResponseTimeForWebTransactions()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsWebTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithResponseTime(TimeSpan.FromSeconds(1))
                .Build();

            Assert.AreEqual(TimeSpan.FromSeconds(1), transaction.ResponseTimeOrDuration);
        }

        [Test]
        public void ResponseTimeOrDurationShouldBeADurationForWebTransactionsWithoutAResponseTime()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsWebTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithNoResponseTime()
                .Build();

            Assert.AreEqual(TimeSpan.FromSeconds(5), transaction.ResponseTimeOrDuration);
        }

        [Test]
        public void ResponseTimeOrDurationIsAlwaysDurationForOtherTransactions()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsOtherTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(5))
                .WithResponseTime(TimeSpan.FromSeconds(1))
                .Build();

            Assert.AreEqual(TimeSpan.FromSeconds(5), transaction.ResponseTimeOrDuration);
        }
    }
}
