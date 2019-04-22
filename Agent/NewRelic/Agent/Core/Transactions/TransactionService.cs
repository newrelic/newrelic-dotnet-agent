using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;

namespace NewRelic.Agent.Core.Transactions
{
	public interface ITransactionService
	{
		/// <summary>
		/// Returns the current internal transaction, if any.
		/// </summary>
		/// <returns></returns>
		[CanBeNull]
		IInternalTransaction GetCurrentInternalTransaction();

		/// <summary>
		/// Returns the existing internal transaction, if any, or creates a new internal transaction and returns it.
		/// </summary>
		/// <param name="initialTransactionName">The initial name to use if a transaction a created.</param>
		/// <param name="onCreate">An action to perform if an internal transaction is created.</param>
		/// <param name="mustBeRootTransaction">Whether or not the transaction must be root.</param>
		/// <returns></returns>
		[CanBeNull]
		IInternalTransaction GetOrCreateInternalTransaction([NotNull] ITransactionName initialTransactionName, Action onCreate = null, Boolean mustBeRootTransaction = true);

		/// <summary>
		/// Removes any outstanding internal transactions.
		/// </summary>
		/// <param name="removeAsync">If true, removes from async context storage</param>
		void RemoveOutstandingInternalTransactions(bool removeAsync, bool removePrimary);

		/// <summary>
		/// Sets the transaction on an async compatible context
		/// </summary>
		/// <param name="transaction">Transaction to store in the async context</param>
		/// <returns>Returns true if found an async context to store the transaction in</returns>
		bool SetTransactionOnAsyncContext(IInternalTransaction transaction);

		bool IsAttachedToAsyncStorage { get; }
	}

	public class TransactionService : ConfigurationBasedService, ITransactionService
	{
		private const String TransactionContextKey = "NewRelic.Transaction";
		[NotNull]
		private readonly IEnumerable<IContextStorage<IInternalTransaction>> _sortedPrimaryContexts;

		[CanBeNull]
		private readonly IContextStorage<IInternalTransaction> _asyncContext;

		[NotNull]
		private readonly ITimerFactory _timerFactory;
		[NotNull]
		private readonly ICallStackManagerFactory _callStackManagerFactory;

		[NotNull]
		private readonly IDatabaseService _databaseService;

		private readonly ITracePriorityManager _tracePriorityManager;

		private IDatabaseStatementParser _databaseStatementParser;

		public TransactionService(IEnumerable<IContextStorageFactory> factories, ITimerFactory timerFactory, ICallStackManagerFactory callStackManagerFactory, IDatabaseService databaseService, ITracePriorityManager tracePriorityManager, IDatabaseStatementParser databaseStatementParser)
		{
			_sortedPrimaryContexts = GetPrimaryTransactionContexts(factories);
			_asyncContext = GetAsyncTransactionContext(factories);
			_timerFactory = timerFactory;
			_callStackManagerFactory = callStackManagerFactory;
			_databaseService = databaseService;
			_tracePriorityManager = tracePriorityManager;
			_databaseStatementParser = databaseStatementParser;
		}

		public bool IsAttachedToAsyncStorage => TryGetInternalTransaction(_asyncContext) != null;


		#region Private Helpers

		[NotNull]
		private static IEnumerable<IContextStorage<IInternalTransaction>> GetPrimaryTransactionContexts([NotNull] IEnumerable<IContextStorageFactory> factories)
		{
			var list = factories
				.Where(factory => factory != null)
				.Where(factory => !factory.IsAsyncStorage)
				.Select(factory => factory.CreateContext<IInternalTransaction>("NewRelic.Transaction"))
				.Where(transactionContext => transactionContext != null)
				.ToList(); //ToList() is important to force evaluation only once

			list.Add(new ThreadLocalStorage<IInternalTransaction>("NewRelic.Transaction"));

			return list
				.OrderByDescending(transactionContext => transactionContext.Priority).ToList();
		}

		private static IContextStorage<IInternalTransaction> GetAsyncTransactionContext([NotNull] IEnumerable<IContextStorageFactory> factories)
		{
			return factories
				.Where(factory => factory != null)
				.Where(factory => factory.IsAsyncStorage)
				.Select(factory => factory.CreateContext<IInternalTransaction>("NewRelic.Transaction"))
				.Where(transactionContext => transactionContext != null)
				.Where(transactionContext => transactionContext.CanProvide)
				.OrderByDescending(transactionContext => transactionContext.Priority)
				.FirstOrDefault(); 
		}

		[CanBeNull]
		private IInternalTransaction TryGetInternalTransaction(IContextStorage<IInternalTransaction> transactionContext)
		{
			try
			{
				if (transactionContext == null)
				{ 
					return null;
				}

				return transactionContext.GetData();
			}
			catch (Exception exception)
			{
				Log.Error($"ITransactionContext threw an exception when calling GetData with {TransactionContextKey}: {exception}");
				return null;
			}
		}

		private IContextStorage<IInternalTransaction> GetFirstActivePrimaryContext()
		{
			foreach (var context in _sortedPrimaryContexts)
			{
				if (context.CanProvide)
				{
					return context;
				}
			}
			return null;
		}

		private IInternalTransaction CreateInternalTransaction([NotNull] ITransactionName initialTransactionName, Action onCreate)
		{
			RemoveOutstandingInternalTransactions(true, true);

			var transactionContext = GetFirstActivePrimaryContext();

			if (transactionContext == null)
			{
				Log.Error("Unable to locate a valid TransactionContext.");
				return null;
			}
			var priority = _tracePriorityManager.Create();
			var transaction = new Transaction(_configuration, initialTransactionName, _timerFactory.StartNewTimer(), DateTime.UtcNow, _callStackManagerFactory.CreateCallStackManager(), _databaseService.SqlObfuscator, priority, _databaseStatementParser);

			try
			{
				transactionContext.SetData(transaction);
			}
			catch (Exception exception)
			{
				Log.Error($"The chosen TransactionContext threw an exception when setting the data: {exception}");
				return null;
			}

			if (Log.IsFinestEnabled) transaction.LogFinest($"Created transaction on {transactionContext}");

			if (onCreate != null)
			{ 
				onCreate();
			}

			return transaction;
		}

		private void TryClearContexts(IEnumerable<IContextStorage<IInternalTransaction>> contexts)
		{
			foreach (var context in contexts)
			{
				try
				{
					context.Clear();
				}
				catch
				{

				}
			}
		}

		#endregion

		#region Public API

		public IInternalTransaction GetCurrentInternalTransaction()
		{
			IInternalTransaction transaction;
			foreach (var context in _sortedPrimaryContexts)
			{
				transaction = TryGetInternalTransaction(context);
				if (transaction != null)
				{
					if (Log.IsFinestEnabled) transaction.LogFinest($"Retrived from {context.ToString()}");
					return transaction;
				}
			}

			transaction = TryGetInternalTransaction(_asyncContext);
			if (transaction != null)
			{
				if (Log.IsFinestEnabled) transaction.LogFinest($"Retrived from {_asyncContext.ToString()}");
			}
			return transaction;
		}

		public bool SetTransactionOnAsyncContext(IInternalTransaction transaction)
		{
			if(_asyncContext == null)
			{
				return false;
			}

			if(_asyncContext.GetData() == null)
			{
				_asyncContext.SetData(transaction);
				if (Log.IsFinestEnabled) transaction.LogFinest($"Attached to {_asyncContext}");
			}

			return true;
		}
		
		public IInternalTransaction GetOrCreateInternalTransaction(ITransactionName initialTransactionName, Action onCreate = null, Boolean mustBeRootTransaction = true)
		{
			var transaction = GetCurrentInternalTransaction();
			if (transaction == null)
			{ 
				return CreateInternalTransaction(initialTransactionName, onCreate);
			}

			var currentNestedTransactionAttempts = transaction.NoticeNestedTransactionAttempt();

			// If the transaction does not need to be root, then it really is a unit of work inside the current transaction, so increment the work counter to make sure all work is finished before the current transaction ends
			if (!mustBeRootTransaction)
			{ 
				transaction.NoticeUnitOfWorkBegins();
			}

			// We have a limit of 100 because 100 attempts to nest a transaction indicates that something has gone wrong (e.g. a transaction is never ending and is being reused over and over)
			if (currentNestedTransactionAttempts > 100)
			{
				Log.WarnFormat("Releasing the transaction because there were too many nested transaction attempts.");
				RemoveOutstandingInternalTransactions(true, true);
				return CreateInternalTransaction(initialTransactionName, onCreate);
			}

			return transaction;
		}

		public void RemoveOutstandingInternalTransactions(bool removeAsync, bool removePrimary)
		{
			if (removePrimary)
			{
				TryClearContexts(_sortedPrimaryContexts);
			}

			if (removeAsync)
			{
				_asyncContext?.Clear();
			}
		}

		#endregion

		#region Event Handlers

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			// It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
			// If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).
		}

		#endregion
	}
}
