// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
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
                () => ClassicAssert.AreEqual("initialCategory", builtName.Category),
                () => ClassicAssert.AreEqual("initialName", builtName.Name)
                );
        }

        [Test]
        public void Build_UsesHighestPriorityName()
        {
            _candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), TransactionNamePriority.Route);

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (

                () => ClassicAssert.AreEqual("newCategory", builtName.Category),
                () => ClassicAssert.AreEqual("newName", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresSamePriorityNames()
        {
            ClassicAssert.IsFalse(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), 0));

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (
                () => ClassicAssert.AreEqual("initialCategory", builtName.Category),
                () => ClassicAssert.AreEqual("initialName", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresLowerPriorityNames()
        {
            ClassicAssert.IsTrue(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory3", "newName3"), TransactionNamePriority.Handler));
            ClassicAssert.IsFalse(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory2", "newName2"), TransactionNamePriority.StatusCode));

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (

                () => ClassicAssert.AreEqual("newCategory3", builtName.Category),
                () => ClassicAssert.AreEqual("newName3", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresNamesAddedAfterFreezing()
        {
            _candidateTransactionName.Freeze(TransactionNameFreezeReason.CrossApplicationTracing);
            ClassicAssert.IsFalse(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), TransactionNamePriority.FrameworkHigh));

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (
                () => ClassicAssert.AreEqual("initialCategory", builtName.Category),
                () => ClassicAssert.AreEqual("initialName", builtName.Name)
                );
        }
    }
}
