/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.Couchbase
{
    public class CouchbaseDefaultWrapperAsync : IWrapper
    {
        private Func<object, string> _getMethodInfo;
        public Func<object, string> GetMethodInfo => _getMethodInfo ?? (_getMethodInfo = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "Name"));

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny("Couchbase.NetClient", "Couchbase.CouchbaseBucket", new[]
                {
                    "AppendAsync",
                    "DecrementAsync",
                    "ExistsAsync",
                    "GetAndLockAsync",
                    "GetAsync",
                    "GetAndTouchAsync",
                    "GetFromReplicaAsync",
                    "GetWithLockAsync",
                    "IncrementAsync",
                    "InsertAsync",
                    "InvokeAsync",
                    "ObserveAsync",
                    "PrependAsync",
                    "RemoveAsync",
                    "ReplaceAsync",
                    "TouchAsync",
                    "UnlockAsync",
                    "UpsertAsync"
                });

            if (canWrap)
            {
                return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("Couchbase.NetClient", "Couchbase.CouchbaseBucket", methodInfo.Method.MethodName);
            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;

            if (operation.StartsWith("Get"))
            {
                operation = "Get";
            }

            var parm = instrumentedMethodCall.MethodCall.MethodArguments[0];
            if (parm is IList || parm is IDictionary)
            {
                operation += "Multiple";
            }

            var model = GetMethodInfo.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

            var segment = transaction.StartDatastoreSegment(
                instrumentedMethodCall.MethodCall,
                new ParsedSqlStatement(DatastoreVendor.Couchbase, model, operation));

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment);
        }

    }
}
