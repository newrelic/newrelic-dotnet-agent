// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.OpenTracing.AmazonLambda.Util;

namespace NewRelic.OpenTracing.AmazonLambda.Events
{
    internal class ErrorEvent : Event
    {
        private const string TYPE = "TransactionError";

        private DateTimeOffset _timestamp;
        private TimeSpan _transactionDuration;
        private readonly string _errorClass;
        private readonly string _errorMessage;

        private IDictionary<string, object> _userAttributes;
        private IDictionary<string, object> _agentAttributes;
        private IDictionary<string, object> _intrinsics;
        private readonly IDictionary<string, object> _distributedTraceIntrinsics;

        private readonly string _transactionName;
        private readonly string _transactionGuid;

        public IDictionary<string, object> Tags { get; }

        public ErrorEvent(DateTimeOffset timestamp, TimeSpan transactionDuration, string errorClass, string errorMessage, string transactionName, string transactionGuid,
                IDictionary<string, object> tags, IDictionary<string, object> distributedTraceIntrinsics)
        {
            _timestamp = timestamp;
            _transactionDuration = transactionDuration;
            _errorClass = errorClass;
            _errorMessage = errorMessage;
            _transactionName = transactionName;
            _transactionGuid = transactionGuid;
            _distributedTraceIntrinsics = distributedTraceIntrinsics;
            Tags = tags;
        }

        // Per 12-4-2019 spec, error events have very specific instrinsic attributes
        public override IDictionary<string, object> Intrinsics
        {
            get
            {
                return _intrinsics ?? (_intrinsics = BuildIntrinsics());
            }
        }

        protected virtual IDictionary<string, object> BuildIntrinsics()
        {
            var intrinsics = new Dictionary<string, object>
            {
                { "type", TYPE },
                { "error.class", _errorClass },
                { "error.message", _errorMessage },
                { "timestamp", _timestamp.ToUnixTimeMilliseconds() },
                { "duration", _transactionDuration.TotalSeconds },
                { "transactionName", _transactionName },
                { "nr.transactionGuid", _transactionGuid },
                { "guid", _transactionGuid }
            };

            foreach (var keyValuePair in _distributedTraceIntrinsics)
            {
                if (!intrinsics.ContainsKey(keyValuePair.Key))
                {
                    intrinsics[keyValuePair.Key] = keyValuePair.Value;
                }
            }

            return intrinsics;
        }

        // Per 12-4-2019 spec, error events do have user attributes, but nothing specific so we save only non agent/instrinsic ones here
        public override IDictionary<string, object> UserAttributes
        {
            get
            {
                return _userAttributes ?? (_userAttributes = Tags.BuildUserAttributes());
            }
        }

        // Per 12-4-2019 spec, error events have very specific agent attributes
        public override IDictionary<string, object> AgentAttributes
        {
            get
            {
                return _agentAttributes ?? (_agentAttributes = Tags.BuildAgentAttributes());
            }
        }

    }
}
