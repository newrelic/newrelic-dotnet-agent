﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Extensions.Providers;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace NewRelic.Agent.Core.Transactions.UnitTest
{
	[TestFixture]
	public class TransactionBuilderServiceTests
	{
		[NotNull]
		private TransactionService _transactionService;
		[NotNull]
		private IContextStorage<ITransaction> _lowPriorityTransactionContext;
		[NotNull]
		private IContextStorage<ITransaction> _highPriorityTransactionContext;
		[NotNull]
		private readonly WebTransactionName _initialTransactionName = new WebTransactionName("initialCategory", "initialName");

		[SetUp]
		public void SetUp()
		{
			_lowPriorityTransactionContext = Mock.Create<IContextStorage<ITransaction>>();
			Mock.Arrange(() => _lowPriorityTransactionContext.Priority).Returns(1);
			DictionaryTransactionContext(_lowPriorityTransactionContext);

			_highPriorityTransactionContext = Mock.Create<IContextStorage<ITransaction>>();
			Mock.Arrange(() => _highPriorityTransactionContext.Priority).Returns(2);
			DictionaryTransactionContext(_highPriorityTransactionContext);

			var factory1 = CreateFactoryForTransactionContext(_highPriorityTransactionContext);
			var factory2 = CreateFactoryForTransactionContext(_lowPriorityTransactionContext);

			_transactionService = new TransactionService(new[] { factory1, factory2 }, Mock.Create<ITimerFactory>(), Mock.Create<ICallStackManagerFactory>(), Mock.Create<IDatabaseService>());
		}

		[TearDown]
		public void TearDown()
		{
			_transactionService.Dispose();
		}

		[Test]
		public void GetCurrentTransactionBuilder_ReturnsNull_IfNoCurrentTransactionBuilder()
		{
			// ACT
			var transaction = _transactionService.GetCurrentInternalTransaction();

			// ASSERT
			Assert.IsNull(transaction);
		}

		[Test]
		public void GetCurrentTransactionBuilder_ReturnsCurrentTransactionBuilder_IfCurrentTransactionBuilderExists()
		{
			// ARRANGE
			var oldTransaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ACT
			var newTransaction = _transactionService.GetCurrentInternalTransaction();

			// ASSERT
			Assert.IsNotNull(newTransaction);
			Assert.AreSame(oldTransaction, newTransaction);
		}

		[Test]
		public void GetOrCreateTransactionBuilder_ReturnsNewTransactionBuilder_IfNoCurrentTransactionBuilder()
		{
			// ACT
			var newTransaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ASSERT
			Assert.IsNotNull(newTransaction);

			var transactionName = newTransaction.ConvertToImmutableTransaction().TransactionName as WebTransactionName;
			Assert.AreEqual(_initialTransactionName.Name, transactionName.Name);
		}

		[Test]
		public void GetOrCreateTransactionBuilder_ReturnsCurrentTransactionBuilder_IfCurrentTransactionBuilderExists()
		{
			// ARRANGE
			var oldTransaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ACT
			var newTransaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ASSERT
			Assert.IsNotNull(newTransaction);
			Assert.AreSame(oldTransaction, newTransaction);
		}

		[Test]
		public void GetOrCreateTransactionBuilder_RunsProvidedAction_IfNoCurrentTransactionBuilder()
		{
			// ARRANGE
			var wasRun = false;

			// ACT
			_transactionService.GetOrCreateInternalTransaction(_initialTransactionName, () => wasRun = true);

			// ASSERT
			Assert.True(wasRun);
		}

		[Test]
		public void GetOrCreateTransactionBuilder_DoesNotRunProvidedAction_IfCurrentTransactionBuilderExists()
		{
			// ARRANGE
			var wasRun = false;
			_transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ACT
			_transactionService.GetOrCreateInternalTransaction(_initialTransactionName, () => wasRun = true);

			// ASSERT
			Assert.False(wasRun);
		}

		[Test]
		public void GetOrCreateTransactionBuilder_IncrementsUnitOfWorkCount_IfCurrentTransactionBuilderExistsAndMustBeRootTransactionIsFalse()
		{
			// ARRANGE
			var transaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			Assert.AreEqual(1, transaction.UnitOfWorkCount);

			// ACT
			_transactionService.GetOrCreateInternalTransaction(_initialTransactionName, mustBeRootTransaction: false);

			// ASSERT
			Assert.AreEqual(2, transaction.UnitOfWorkCount);
		}

		[Test]
		public void GetOrCreateTransactionBuilder_DoesNotIncrementsUnitOfWorkCount_IfCurrentTransactionBuilderExistsAndMustBeRootTransactionIsTrue()
		{
			// ARRANGE
			var transaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			Assert.AreEqual(1, transaction.UnitOfWorkCount);

			// ACT
			_transactionService.GetOrCreateInternalTransaction(_initialTransactionName, mustBeRootTransaction: true);

			// ASSERT
			Assert.AreEqual(1, transaction.UnitOfWorkCount);
		}

		[Test]
		public void GetOrCreateTransactionBuilder_ReplacesExistingTransaction_IfCurrentTransactionBuilderExistsAndRecreateCountIsOver100()
		{
			// ARRANGE
			var oldTransaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);
			Enumerable.Range(0, 101).ForEach(_ => oldTransaction.NoticeNestedTransactionAttempt());

			Assert.AreEqual(101, oldTransaction.NestedTransactionAttempts);

			// ACT
			var newTransaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);
			var newTransaction2 = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ASSERT
			Assert.AreNotSame(oldTransaction, newTransaction);
			Assert.AreSame(newTransaction, newTransaction2);
		}

		[Test]
		public void when_multiple_transactionContexts_are_available_with_higher_provided_first_then_highest_priority_is_used()
		{
			// ARRANGE
			DictionaryTransactionContext(_highPriorityTransactionContext);
			ThrowingTransactionContext(_lowPriorityTransactionContext);

			// ACT
			var transaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ASSERT
			Assert.IsNotNull(transaction);
		}

		[Test]
		public void when_multiple_transactionContexts_are_available_with_lower_provided_first_then_highest_priority_is_used()
		{
			// ARRANGE
			DictionaryTransactionContext(_highPriorityTransactionContext);
			ThrowingTransactionContext(_lowPriorityTransactionContext);

			// ACT
			var transaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ASSERT
			Assert.IsNotNull(transaction);
		}

		[Test]
		public void when_TransactionContextFactory_throws_then_current_transaction_builder_is_null()
		{
			// ARRANGE
			ThrowingTransactionContext(_highPriorityTransactionContext);

			// ACT
			var transaction = _transactionService.GetOrCreateInternalTransaction(_initialTransactionName);

			// ASSERT
			Assert.IsNull(transaction);
		}

		private static void ThrowingTransactionContext(IContextStorage<ITransaction> transactionContext)
		{
			Mock.Arrange(() => transactionContext.CanProvide).Returns(true);
			Mock.Arrange(() => transactionContext.SetData((ITransaction)Arg.AnyObject)).Throws<Exception>();
			Mock.Arrange(() => transactionContext.GetData()).Throws<Exception>();
		}

		private static void DictionaryTransactionContext(IContextStorage<ITransaction> transactionContext)
		{
			const string key = "TEST";
			var dictionary = new Dictionary<String, Object>();
			Mock.Arrange(() => transactionContext.CanProvide).Returns(true);
			Mock.Arrange(() => transactionContext.SetData((ITransaction)Arg.AnyObject)).DoInstead((Object value) =>
			{
				dictionary[key] = value;
			});
			Mock.Arrange(() => transactionContext.GetData()).Returns(() =>
			{
				if (!dictionary.ContainsKey(key))
					return null;

				Object value;
				dictionary.TryGetValue(key, out value);
				return value as ITransaction;

			});
		}

		[NotNull]
		private static IContextStorageFactory CreateFactoryForTransactionContext(IContextStorage<ITransaction> transactionContext)
		{
			var transactionContextFactory = Mock.Create<IContextStorageFactory>();
			Mock.Arrange(() => transactionContextFactory.CreateContext<ITransaction>(Arg.AnyString)).Returns(transactionContext);
			return transactionContextFactory;
		}
	}
}
