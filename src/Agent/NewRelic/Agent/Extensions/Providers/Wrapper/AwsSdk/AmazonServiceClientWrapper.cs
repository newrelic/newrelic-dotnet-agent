// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Caching;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk;

public class AmazonServiceClientWrapper : IWrapper
{
    private const int LRUCapacity = 100;
    // cache the account id per instance of AmazonServiceClient.Config
    public static LRUCache<WeakReferenceKey<object>, string> AwsAccountIdByClientConfigCache = new(LRUCapacity);

    // cache instances of AmazonServiceClient
    private static readonly LRUHashSet<WeakReferenceKey<object>> AmazonServiceClientInstanceCache = new(LRUCapacity);

    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        return new CanWrapResponse(instrumentedMethodInfo.RequestedWrapperName == nameof(AmazonServiceClientWrapper));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        object client = instrumentedMethodCall.MethodCall.InvocationTarget;

        var weakReferenceKey = new WeakReferenceKey<object>(client);
        if (AmazonServiceClientInstanceCache.Contains(weakReferenceKey)) // don't do anything if we've already seen this client instance
            return Delegates.NoOp;

        AmazonServiceClientInstanceCache.Add(weakReferenceKey);

        string awsAccountId;
        try
        {
            // get the AWSCredentials parameter
            dynamic awsCredentials = instrumentedMethodCall.MethodCall.MethodArguments[0];

            dynamic immutableCredentials = awsCredentials.GetCredentials();
            string accessKey = immutableCredentials.AccessKey;

            // convert the access key to an account id
            awsAccountId = AwsAccountIdDecoder.GetAccountId(accessKey);
        }
        catch (Exception e)
        {
            agent.Logger.Info($"Unable to parse AWS Account ID from AccessKey. Using AccountId from configuration instead. Exception: {e.Message}");
            awsAccountId = agent.Configuration.AwsAccountId;
        }

        return Delegates.GetDelegateFor(onComplete: () =>
        {
            // get the _config field from the client
            object clientConfig = ((dynamic)client).Config;

            // cache the account id using clientConfig as the key
            AwsAccountIdByClientConfigCache.Put(new WeakReferenceKey<object>(clientConfig), awsAccountId);
        });
    }
}
