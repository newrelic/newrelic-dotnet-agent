// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;
using System;

namespace NewRelic.OpenTracing.AmazonLambda.State
{
    internal class TransactionState
    {
        private bool _error = false;

        private string _transactionName;

        private string _transactionId;

        public string TransactionId
        {
            get
            {
                return _transactionId ?? (_transactionId = GuidGenerator.GenerateNewRelicGuid());
            }
        }

        public void SetTransactionName(string transactionType, string functionName)
        {
            _transactionName = transactionType + "/Function/" + functionName;
        }

        public string TransactionName => _transactionName;


        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(0);

        public void SetError()
        {
            _error = true;
        }

        public bool HasError()
        {
            return _error;
        }
    }
}
