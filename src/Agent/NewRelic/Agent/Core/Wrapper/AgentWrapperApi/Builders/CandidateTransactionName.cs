using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Collections;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    public interface ICandidateTransactionName
    {
        bool TrySet([NotNull] ITransactionName transactionName, Int32 priority);

        /// <summary>
        /// Freeze the transaction name so it can't be changed again.
        /// </summary>
        void Freeze();

        // REVIEW this was marked non-null but the code seems to indicate that null transaction names are accepted through constructors
        [NotNull]
        ITransactionName CurrentTransactionName { get; }
    }

    public class CandidateTransactionName : ICandidateTransactionName
    {
        /// <summary>
        /// The current transaction name.
        /// This variable is volatile because it is accessed without a lock in the
        /// CurrentTransaction accessor.
        /// </summary>
        [NotNull]
        private volatile ITransactionName _currentTransactionName;

        [NotNull]
        private Int32 _highestPriority;

        [NotNull]
        private bool _isFrozen = false;

        public CandidateTransactionName([NotNull] ITransactionName initialTransactionName)
        {
            _currentTransactionName = initialTransactionName;
            _highestPriority = 0;
        }

        public bool TrySet(ITransactionName transactionName, Int32 priority)
        {
            // We could define this lock to be more coarse grained if we added extra variables
            // to track the before/after stuff for logging, but finest is rarely enabled, and if it is
            // things are already slow, so just do the logging under the lock.
            lock (this)
            {
                if (Log.IsFinestEnabled)
                {
                    if (_isFrozen)
                        Log.Finest($"Ignoring transaction name {FormatTransactionName(transactionName, priority)} because existing transaction name is frozen.");
                    else if (_currentTransactionName == null)
                        Log.Finest($"Setting transaction name to {FormatTransactionName(transactionName, priority)} from [nothing]");
                    else if (priority > _highestPriority)
                        Log.Finest($"Setting transaction name to {FormatTransactionName(transactionName, priority)} from {FormatTransactionName(_currentTransactionName, _highestPriority)}");
                    else
                        Log.Finest($"Ignoring transaction name {FormatTransactionName(transactionName, priority)} in favor of existing name {FormatTransactionName(_currentTransactionName, _highestPriority)}");
                }

                if (ChangeName(priority))
                {
                    _highestPriority = priority;
                    _currentTransactionName = transactionName;

                    return true;
                }
            }

            return false;
        }

        private bool ChangeName(Int32 newPriority)
        {
            return !_isFrozen && (newPriority > _highestPriority || _currentTransactionName == null);
        }

        public void Freeze()
        {
            // the _isFrozen variable is accessed under the same lock in the Add method.
            lock (this)
            {
                // REVIEW should this reject the freeze request if the transaction name is null?
                _isFrozen = true;
            }

            if (Log.IsFinestEnabled)
            {
                Log.Finest($"Freezing transaction name to {FormatTransactionName(_currentTransactionName, _highestPriority)}");
            }
        }

        public ITransactionName CurrentTransactionName => _currentTransactionName;

        [CanBeNull]
        private static String FormatTransactionName([NotNull] ITransactionName transactionName, Int32 priority)
        {
            return $"{transactionName.GetType().Name}{JsonConvert.SerializeObject(transactionName)} (priority {priority})";
        }
    }
}
