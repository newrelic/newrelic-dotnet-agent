// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.Couchbase3;

public class Couchbase3QueryWrapper : IWrapper
{
    private static Func<object, string> _getMethodInfo;
    private static Func<object, string> GetMethodInfo => _getMethodInfo ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("Couchbase.NetClient", "Couchbase.KeyValue.Scope", "Name");

    private static Func<object, object> _getBucket;
    private static Func<object, object> GetBucket => _getBucket ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("Couchbase.NetClient", "Couchbase.KeyValue.Scope", "Bucket");

    private static Func<object, string> _getName;
    private static Func<object, string> GetName => _getName ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("Couchbase.NetClient", "Couchbase.Core.BucketBase", "Name");

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(methodInfo.RequestedWrapperName.Equals(nameof(Couchbase3QueryWrapper)));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
        }

        var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
        string model = GetBucketName(instrumentedMethodCall.MethodCall.InvocationTarget); 

        // TODO: for SearchAsync / SearchQueryAsync, this is just the name of the index. Should commandText just be null in that case?
        string commandText = instrumentedMethodCall.MethodCall.MethodArguments[0] as string; 

        var segment = transaction.StartDatastoreSegment(
            instrumentedMethodCall.MethodCall,
            new ParsedSqlStatement(DatastoreVendor.Couchbase, model, operation),
            null,
            commandText);

        return Delegates.GetDelegateFor(segment);
    }

    private string GetBucketName(object owner)
    {
        var bucket = GetBucket(owner);
        return GetName(bucket);
    }

}
