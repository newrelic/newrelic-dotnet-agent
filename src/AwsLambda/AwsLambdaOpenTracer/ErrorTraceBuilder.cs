// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.OpenTracing.AmazonLambda.Traces;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class ErrorTraceBuilder
    {
        private DateTimeOffset _timestamp;
        private string _transactionName;
        private string _message;
        private string _errorType;
        private string _stackTrace;
        private IDictionary<string, object> _intrinsics = new Dictionary<string, object>();
        private IDictionary<string, object> _userAttributes = new Dictionary<string, object>();
        private string _transactionGuid;

        public ErrorTraceBuilder()
        {
        }

        public ErrorTraceBuilder SetTimestamp(DateTimeOffset timestamp)
        {
            _timestamp = timestamp;
            return this;
        }

        public ErrorTraceBuilder SetTransactionName(string transactionName)
        {
            _transactionName = transactionName;
            return this;
        }

        public ErrorTraceBuilder SetErrorMessage(string message)
        {
            _message = message;
            return this;
        }

        public ErrorTraceBuilder SetErrorType(string errorType)
        {
            _errorType = errorType;
            return this;
        }

        public ErrorTraceBuilder SetStackTrace(string stackTrace)
        {
            _stackTrace = stackTrace;
            return this;
        }

        public ErrorTraceBuilder SetDistributedTraceIntrinsics(IDictionary<string, object> distributedTraceIntrinsics)
        {
            foreach (var keyValuePair in distributedTraceIntrinsics)
            {
                if (!_intrinsics.ContainsKey(keyValuePair.Key))
                {
                    _intrinsics.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
            return this;
        }

        public ErrorTraceBuilder SetUserAttributes(IDictionary<string, object> userAttributes)
        {
            _userAttributes = userAttributes;
            return this;
        }

        public ErrorTraceBuilder SetTransactionGuid(string transactionGuid)
        {
            _transactionGuid = transactionGuid;

            if (!_intrinsics.ContainsKey("nr.transactionGuid"))
            {
                _intrinsics.Add("nr.transactionGuid", _transactionGuid);
            }

            if (!_intrinsics.ContainsKey("guid"))
            {
                _intrinsics.Add("guid", _transactionGuid);
            }
            else
            {
                _intrinsics["guid"] = _transactionGuid;
            }

            return this;
        }

        public ErrorTrace CreateErrorTrace()
        {
            return new ErrorTrace(_timestamp, _transactionName, _message, _errorType, _stackTrace, _intrinsics, _userAttributes, _transactionGuid);
        }
    }
}
