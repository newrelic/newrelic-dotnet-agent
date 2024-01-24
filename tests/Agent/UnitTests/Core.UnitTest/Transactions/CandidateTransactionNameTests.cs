// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transactions
{
    [TestFixture]
    public class CandidateTransactionNameTests
    {
        private TransactionName _initialTransactionName;

        private CandidateTransactionName _candidateTransactionName;

        [SetUp]
        public void SetUp()
        {
            _initialTransactionName = TransactionName.ForWebTransaction("initialCategory", "initialName");
            _candidateTransactionName = new CandidateTransactionName(Mock.Create<ITransaction>(), _initialTransactionName);
        }

        [Test]
        public void Build_UsesInitialName_IfNoOtherNamesAdded()
        {
            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (
                () => Assert.That(builtName.Category, Is.EqualTo("initialCategory")),
                () => Assert.That(builtName.Name, Is.EqualTo("initialName"))
                );
        }

        [Test]
        public void Build_UsesHighestPriorityName()
        {
            _candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), TransactionNamePriority.Route);

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (

                () => Assert.That(builtName.Category, Is.EqualTo("newCategory")),
                () => Assert.That(builtName.Name, Is.EqualTo("newName"))
                );
        }

        [Test]
        public void Build_IgnoresSamePriorityNames()
        {
            Assert.That(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), 0), Is.False);

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (
                () => Assert.That(builtName.Category, Is.EqualTo("initialCategory")),
                () => Assert.That(builtName.Name, Is.EqualTo("initialName"))
                );
        }

        [Test]
        public void Build_IgnoresLowerPriorityNames()
        {
            Assert.Multiple(() =>
            {
                Assert.That(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory3", "newName3"), TransactionNamePriority.Handler), Is.True);
                Assert.That(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory2", "newName2"), TransactionNamePriority.StatusCode), Is.False);
            });

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (

                () => Assert.That(builtName.Category, Is.EqualTo("newCategory3")),
                () => Assert.That(builtName.Name, Is.EqualTo("newName3"))
                );
        }

        [Test]
        public void Build_IgnoresNamesAddedAfterFreezing()
        {
            _candidateTransactionName.Freeze(TransactionNameFreezeReason.CrossApplicationTracing);
            Assert.That(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), TransactionNamePriority.FrameworkHigh), Is.False);

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (
                () => Assert.That(builtName.Category, Is.EqualTo("initialCategory")),
                () => Assert.That(builtName.Name, Is.EqualTo("initialName"))
                );
        }
    }
}
