// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Core.DistributedTracing;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Transactions
{
    public interface ITransactionService
    {
        /// <summary>
        /// Returns the current internal transaction, if any.
        /// </summary>
        /// <returns></returns>
        IInternalTransaction GetCurrentInternalTransaction();

        /// <summary>
        /// Returns the existing internal transaction, if any, or creates a new internal transaction and returns it.
        /// </summary>
        /// <param name="initialTransactionName">The initial name to use if a transaction a created.</param>
        /// <param name="onCreate">An action to perform if an internal transaction is created.</param>
        /// <param name="doNotTrackAsUnitOfWork">Whether or not the transaction must be root.</param>
        /// <returns></returns>
        IInternalTransaction GetOrCreateInternalTransaction(ITransactionName initialTransactionName, Action onCreate = null, bool doNotTrackAsUnitOfWork = true);

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

        float CreatePriority();
    }

    public class TransactionService : ConfigurationBasedService, ITransactionService
    {
        private const string TransactionContextKey = "NewRelic.Transaction";
        private readonly IEnumerable<IContextStorage<IInternalTransaction>> _sortedPrimaryContexts;
        private readonly IContextStorage<IInternalTransaction> _asyncContext;
        private readonly ISimpleTimerFactory _timerFactory;
        private readonly ICallStackManagerFactory _callStackManagerFactory;
        private readonly IDatabaseService _databaseService;
        private readonly ITracePriorityManager _tracePriorityManager;
        private readonly IDatabaseStatementParser _databaseStatementParser;
        private readonly IErrorService _errorService;
        private readonly IDistributedTracePayloadHandler _distributedTracePayloadHandler;
        private readonly IAttributeDefinitionService _attribDefSvc;
        private readonly IAdaptiveSampler _adaptiveSampler;

        public TransactionService(IEnumerable<IContextStorageFactory> factories, ISimpleTimerFactory timerFactory, ICallStackManagerFactory callStackManagerFactory, IDatabaseService databaseService, ITracePriorityManager tracePriorityManager, IDatabaseStatementParser databaseStatementParser,
            IErrorService errorService, IDistributedTracePayloadHandler distributedTracePayloadHandler, IAttributeDefinitionService attribDefSvc, IAdaptiveSampler adaptiveSampler)
        {
            _sortedPrimaryContexts = GetPrimaryTransactionContexts(factories);
            _asyncContext = GetAsyncTransactionContext(factories);
            _timerFactory = timerFactory;
            _callStackManagerFactory = callStackManagerFactory;
            _databaseService = databaseService;
            _tracePriorityManager = tracePriorityManager;
            _databaseStatementParser = databaseStatementParser;
            _errorService = errorService;
            _distributedTracePayloadHandler = distributedTracePayloadHandler;
            _attribDefSvc = attribDefSvc;
            _adaptiveSampler = adaptiveSampler;
        }

        public bool IsAttachedToAsyncStorage => TryGetInternalTransaction(_asyncContext) != null;

        public float CreatePriority()
        {
            return _tracePriorityManager.Create();
        }

        #region Private Helpers

        private static IEnumerable<IContextStorage<IInternalTransaction>> GetPrimaryTransactionContexts(IEnumerable<IContextStorageFactory> factories)
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

        private static IContextStorage<IInternalTransaction> GetAsyncTransactionContext(IEnumerable<IContextStorageFactory> factories)
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
                Log.Error(exception, $"ITransactionContext threw an exception when calling GetData with {TransactionContextKey}");
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

        private IInternalTransaction CreateInternalTransaction(ITransactionName initialTransactionName, Action onCreate)
        {
            RemoveOutstandingInternalTransactions(true, true);

            var transactionContext = GetFirstActivePrimaryContext();

            if (transactionContext == null)
            {
                Log.Error("Unable to locate a valid TransactionContext.");
                return null;
            }
            var priority = _tracePriorityManager.Create();
            var transaction = new Transaction(_configuration, initialTransactionName, _timerFactory.StartNewTimer(),
                DateTime.UtcNow, _callStackManagerFactory.CreateCallStackManager(), _databaseService, priority,
                _databaseStatementParser, _distributedTracePayloadHandler, _errorService, _attribDefSvc.AttributeDefs);
            _adaptiveSampler.StartTransaction();
            try
            {
                transactionContext.SetData(transaction);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "The chosen TransactionContext threw an exception when setting the data");
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
                    if (Log.IsFinestEnabled) transaction.LogFinest($"Retrieved from {context.ToString()}");
                    return transaction;
                }
            }

            transaction = TryGetInternalTransaction(_asyncContext);
            if (transaction != null)
            {
                if (Log.IsFinestEnabled) transaction.LogFinest($"Retrieved from {_asyncContext.ToString()}");
            }
            return transaction;
        }

        public bool SetTransactionOnAsyncContext(IInternalTransaction transaction)
        {
            if (_asyncContext == null)
            {
                return false;
            }

            if (_asyncContext.GetData() == null)
            {
                _asyncContext.SetData(transaction);
                if (Log.IsFinestEnabled) transaction.LogFinest($"Attached to {_asyncContext}");
            }

            return true;
        }

        public IInternalTransaction GetOrCreateInternalTransaction(ITransactionName initialTransactionName, Action onCreate = null, bool doNotTrackAsUnitOfWork = true)
        {
            var transaction = GetCurrentInternalTransaction();
            if (transaction == null)
            {
                return CreateInternalTransaction(initialTransactionName, onCreate);
            }

            var currentNestedTransactionAttempts = transaction.NoticeNestedTransactionAttempt();

            // If the transaction does not need to be root, then it really is a unit of work inside the current transaction, so increment the work counter to make sure all work is finished before the current transaction ends
            if (!doNotTrackAsUnitOfWork)
            {
                transaction.NoticeUnitOfWorkBegins();
            }

            // We have a limit of 100 because 100 attempts to nest a transaction indicates that something has gone wrong (e.g. a transaction is never ending and is being reused over and over)
            if (currentNestedTransactionAttempts > 100)
            {
                bool wasAsync = IsAttachedToAsyncStorage;

                Log.Warn("Releasing the transaction because there were too many nested transaction attempts.");
                RemoveOutstandingInternalTransactions(true, true);
                var newTransaction = CreateInternalTransaction(initialTransactionName, onCreate);

                if (wasAsync)
                {
                    newTransaction.AttachToAsync();
                    // If we're currently in an async context, it's VERY important that we not leave this transaction
                    // in ThreadLocal storage, or other workers may grab the wrong "current" transaction
                    RemoveOutstandingInternalTransactions(false, true);
                }
                return newTransaction;
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
