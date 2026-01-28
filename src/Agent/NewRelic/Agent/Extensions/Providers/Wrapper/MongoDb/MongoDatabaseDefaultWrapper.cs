// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.MongoDb;

public class MongoDatabaseDefaultWrapper : IWrapper
{
    private const string WrapperName = "MongoDatabaseDefaultWrapper";
    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
        var model = GetCollectionName(instrumentedMethodCall.MethodCall);
        var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation));

        return Delegates.GetDelegateFor(segment);
    }

    private string GetCollectionName(MethodCall methodCall)
    {
        return methodCall.MethodArguments.ExtractNotNullAs<string>(0);
    }
}