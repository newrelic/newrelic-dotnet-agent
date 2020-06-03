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
                () => Assert.AreEqual("initialCategory", builtName.Category),
                () => Assert.AreEqual("initialName", builtName.Name)
                );
        }

        [Test]
        public void Build_UsesHighestPriorityName()
        {
            _candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), TransactionNamePriority.Route);

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (

                () => Assert.AreEqual("newCategory", builtName.Category),
                () => Assert.AreEqual("newName", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresSamePriorityNames()
        {
            Assert.IsFalse(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), 0));

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (
                () => Assert.AreEqual("initialCategory", builtName.Category),
                () => Assert.AreEqual("initialName", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresLowerPriorityNames()
        {
            Assert.IsTrue(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory3", "newName3"), TransactionNamePriority.Handler));
            Assert.IsFalse(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory2", "newName2"), TransactionNamePriority.StatusCode));

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (

                () => Assert.AreEqual("newCategory3", builtName.Category),
                () => Assert.AreEqual("newName3", builtName.Name)
                );
        }

        [Test]
        public void Build_IgnoresNamesAddedAfterFreezing()
        {
            _candidateTransactionName.Freeze(TransactionNameFreezeReason.CrossApplicationTracing);
            Assert.IsFalse(_candidateTransactionName.TrySet(TransactionName.ForWebTransaction("newCategory", "newName"), TransactionNamePriority.FrameworkHigh));

            var builtName = _candidateTransactionName.CurrentTransactionName;

            NrAssert.Multiple
                (
                () => Assert.AreEqual("initialCategory", builtName.Category),
                () => Assert.AreEqual("initialName", builtName.Name)
                );
        }
    }
}
