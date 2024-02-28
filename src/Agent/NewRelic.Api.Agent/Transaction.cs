// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;

namespace NewRelic.Api.Agent
{
    internal class Transaction : ITransaction
    {
        private readonly dynamic _wrappedTransaction;
        private static ITransaction _noOpTransaction = new NoOpTransaction();

        internal Transaction(dynamic? wrappedTransaction = null)
        {
            _wrappedTransaction = wrappedTransaction ?? _noOpTransaction;
        }

        private static bool _isAcceptDistributedTraceHeadersAvailable = true;

        public void AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType)
        {
            if (!_isAcceptDistributedTraceHeadersAvailable) return;

            try
            {
                _wrappedTransaction.AcceptDistributedTraceHeaders(carrier, getter, (int)transportType);
            }
            catch (RuntimeBinderException)
            {
                _isAcceptDistributedTraceHeadersAvailable = false;
            }
        }

        private static bool _isInsertDistributedTraceHeadersAvailable = true;

        public void InsertDistributedTraceHeaders<T>(T carrier, Action<T, string, string> setter)
        {
            if (!_isInsertDistributedTraceHeadersAvailable) return;

            try
            {
                _wrappedTransaction.InsertDistributedTraceHeaders(carrier, setter);
            }
            catch (RuntimeBinderException)
            {
                _isInsertDistributedTraceHeadersAvailable = false;
            }
        }

        private static bool _isAddCustomAttributeAvailable = true;
        public ITransaction AddCustomAttribute(string key, object value)
        {
            if (!_isAddCustomAttributeAvailable)
            {
                return _noOpTransaction.AddCustomAttribute(key, value);
            }

            try
            {
                _wrappedTransaction.AddCustomAttribute(key, value);
                return this;
            }
            catch (RuntimeBinderException)
            {
                _isAddCustomAttributeAvailable = false;
            }

            return this;
        }

        private static bool _isCurrentSpanAvailable = true;
        public ISpan CurrentSpan
        {
            get
            {
                if (!_isCurrentSpanAvailable) return _noOpTransaction.CurrentSpan;

                try
                {
                    var wrappedSpan = _wrappedTransaction.CurrentSpan;
                    if (wrappedSpan != null)
                    {
                        return new Span(wrappedSpan);
                    }
                }
                catch (RuntimeBinderException)
                {
                    _isCurrentSpanAvailable = false;
                }

                return _noOpTransaction.CurrentSpan;
            }
        }

        private static bool _isSetUserIdAvailable = true;
        /// <summary>
        /// Sets a User Id to be associated with this transaction.
        /// </summary>
        /// <param name="userid">The User Id for this transaction.</param>
        public void SetUserId(string userid)
        {
            if (!_isSetUserIdAvailable)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(userid))
                {
                    _wrappedTransaction.SetUserId(userid);
                }
            }
            catch (RuntimeBinderException)
            {
                _isSetUserIdAvailable = false;
            }
        }
    }
}
