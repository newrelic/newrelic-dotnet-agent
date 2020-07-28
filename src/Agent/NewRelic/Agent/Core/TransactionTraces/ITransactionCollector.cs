using System.Collections.Generic;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.TransactionTraces
{
    public interface ITransactionCollector
    {
        /// <summary>
        /// Informs a transaction collector of a new transaction trace allowing it to collect it.
        /// Note that this method may be called by multiple threads so the trace storage method should
        /// be thread safe.
        /// 
        /// Transaction collectors must be able to decide whether or not they want to keep a transaction 
        /// without calling the WireModel method.
        /// </summary>
        /// <param name="transactionTraceWireModelComponents"></param>
        void Collect(TransactionTraceWireModelComponents transactionTraceWireModelComponents);

        /// <summary>
        /// Returns an immutable enumerable of samples and clears the sample storage.
        /// </summary>
        IEnumerable<TransactionTraceWireModelComponents> GetAndClearCollectedSamples();
    }
}
