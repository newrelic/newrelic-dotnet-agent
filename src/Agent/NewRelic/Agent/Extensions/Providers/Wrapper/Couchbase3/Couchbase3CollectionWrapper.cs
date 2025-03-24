// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Linq;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.Couchbase3;

public class Couchbase3CollectionWrapper: IWrapper
{
    private Func<object, string> _getMethodInfo;
    public Func<object, string> GetMethodInfo => _getMethodInfo ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("Couchbase.NetClient", "Couchbase.KeyValue.CouchbaseCollection", "Name");

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(methodInfo.RequestedWrapperName.Equals(nameof(Couchbase3CollectionWrapper)));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var isAsync = instrumentedMethodCall.InstrumentedMethodInfo.Method.MethodName != "GetAllReplicasAsync"; // this is the only non-async method in ICouchbaseCollection
        if (isAsync) 
            transaction.AttachToAsync(); // all methods are async

        var operation = instrumentedMethodCall.MethodCall.Method.MethodName;

        var model = GetMethodInfo.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

        var segment = transaction.StartDatastoreSegment(
            instrumentedMethodCall.MethodCall,
            new ParsedSqlStatement(DatastoreVendor.Couchbase, model, operation));

        return isAsync ? Delegates.GetAsyncDelegateFor<Task>(agent, segment) : Delegates.GetDelegateFor(segment);
    }

}
