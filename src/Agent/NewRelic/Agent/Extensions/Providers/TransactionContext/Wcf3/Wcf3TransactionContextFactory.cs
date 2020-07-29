/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Runtime.CompilerServices;
using System.ServiceModel;

namespace NewRelic.Agent.Extensions.Providers.TransactionContext
{
    /// <summary>
    /// ITransactionContextFactory implementation for version 3 of WCF.
    /// </summary>
    public class Wcf3TransactionContextFactory : IContextStorageFactory
    {
        public bool IsAsyncStorage => false;

        bool IContextStorageFactory.IsValid
        {
            get
            {
                // if attempting to access OperationContext throws an exception then this factory is invalid
                try
                {
                    AccessOperationContext();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }
        }

        ContextStorageType IContextStorageFactory.Type => ContextStorageType.OperationContext;

        IContextStorage<T> IContextStorageFactory.CreateContext<T>(string key)
        {
            return new Wcf3TransactionContext<T>(key);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void AccessOperationContext()
        {
            // we want to force access to the operation context so we can get an type load exception if WCF is not available
            var temp = OperationContext.Current;
        }
    }
}
