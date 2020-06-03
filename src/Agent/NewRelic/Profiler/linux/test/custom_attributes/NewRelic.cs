using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NewRelic.Api.Agent
{

    /// <summary>
    /// Instructs the New Relic agent to time the associated method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TraceAttribute : Attribute
    {
    }

    /// <summary>
    /// Instructs the New Relic agent to create a transaction
    /// and time the associated method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TransactionAttribute : TraceAttribute
    {
        /// <summary>
        /// If true, the transaction will be reported as a web transaction, otherwise it
        /// will be reported as an "other" transaction.  The default is false.
        /// </summary>
        public bool Web { get; set; }
    }
}
