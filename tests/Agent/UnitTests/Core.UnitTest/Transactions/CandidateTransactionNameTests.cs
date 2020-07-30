/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Transactions
{
    [TestFixture]
    public class CandidateTransactionNameTests
    {
        private WebTransactionName _initialTransactionName;
        private CandidateTransactionName _candidateTransactionName;

        [SetUp]
        public void SetUp()
        {
            _initialTransactionName = new WebTransactionName("initialCategory", "initialName");
            _candidateTransactionName = new CandidateTransactionName(_initialTransactionName);
        }

        [Test]
        public void Build_UsesInitialName_IfNoOtherNamesAdded()
        {
            var builtName = _candidateTransactionName.CurrentTransactionName as WebTransactionName;

            NrAssert.Multiple
                (
                () => Assert.AreEqual("initialCategory", builtName.Category),
                () => Assert.AreEqual("initialName", builtName.Name)
                );
        }

        [Test]
        public void Build_UsesHighestPriorityName()
        {
            _candidateTransactionName.TrySet(new WebTransactionName("newCategory", "newName"), 4);

            var builtName = _candidateTransactionName.CurrentTransactionName as WebTransactionName;

            NrAssert.Multiple
                (

                () => Assert.AreEqual("newCategory", builtName.Category),
                () => Assert.AreEqual("newName", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresSamePriorityNames()
        {
            Assert.IsFalse(_candidateTransactionName.TrySet(new WebTransactionName("newCategory", "newName"), 0));

            var builtName = _candidateTransactionName.CurrentTransactionName as WebTransactionName;

            NrAssert.Multiple
                (
                () => Assert.AreEqual("initialCategory", builtName.Category),
                () => Assert.AreEqual("initialName", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresLowerPriorityNames()
        {
            Assert.IsTrue(_candidateTransactionName.TrySet(new WebTransactionName("newCategory3", "newName3"), 3));
            Assert.IsFalse(_candidateTransactionName.TrySet(new WebTransactionName("newCategory2", "newName2"), 2));

            var builtName = _candidateTransactionName.CurrentTransactionName as WebTransactionName;

            NrAssert.Multiple
                (

                () => Assert.AreEqual("newCategory3", builtName.Category),
                () => Assert.AreEqual("newName3", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresNamesAddedAfterFreezing()
        {
            _candidateTransactionName.Freeze();
            Assert.IsFalse(_candidateTransactionName.TrySet(new WebTransactionName("newCategory", "newName"), 6));

            var builtName = _candidateTransactionName.CurrentTransactionName as WebTransactionName;

            NrAssert.Multiple
                (
                () => Assert.AreEqual("initialCategory", builtName.Category),
                () => Assert.AreEqual("initialName", builtName.Name)
                );
        }
    }
}
