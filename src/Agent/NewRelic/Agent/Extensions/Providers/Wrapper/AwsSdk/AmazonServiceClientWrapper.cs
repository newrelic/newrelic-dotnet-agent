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
    private bool _disableServiceClientWrapper;

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
        if (_disableServiceClientWrapper) // something bad happened on a previous call, so we're disabling this wrapper
            return Delegates.NoOp;

        object client = instrumentedMethodCall.MethodCall.InvocationTarget;

        var weakReferenceKey = new WeakReferenceKey<object>(client);

        // don't do anything if we've already seen this client instance -- the account ID is cached already
        if (AmazonServiceClientInstanceCache.Contains(weakReferenceKey)) 
            return Delegates.NoOp;

        // add this client instance to cache so we don't process it again
        AmazonServiceClientInstanceCache.Add(weakReferenceKey);

        // retrieve and cache the account id
        string awsAccountId = null;
        try
        {
            // get the AWSCredentials parameter
            if (instrumentedMethodCall.MethodCall.MethodArguments.Length > 0)
            {
                dynamic awsCredentials = instrumentedMethodCall.MethodCall.MethodArguments[0];
                if (awsCredentials != null)
                {
                    dynamic immutableCredentials = awsCredentials.GetCredentials();
                    if (immutableCredentials != null)
                    {
                        string accessKey = immutableCredentials.AccessKey;

                        if (!string.IsNullOrEmpty(accessKey))
                        {
                            try
                            {
                                // convert the access key to an account id
                                awsAccountId = AwsAccountIdDecoder.GetAccountId(accessKey);
                            }
                            catch (Exception e)
                            {
                                agent.Logger.Debug(e, "Unexpected exception parsing AWS Account ID from AccessKey.");
                            }
                        }
                        else
                            agent.Logger.Debug("Unable to parse AWS Account ID from AWSCredentials because AccessKey was null.");
                    }
                    else
                        agent.Logger.Debug("Unable to parse AWS Account ID from AWSCredentials because GetCredentials() returned null.");
                }
                else
                    agent.Logger.Debug("Unable to parse AWS Account ID from AWSCredentials because AWSCredentials was null.");
            }
            else
                agent.Logger.Debug("Unable to parse AWS Account ID from AWSCredentials because there were no arguments in the method call.");

            // fall back to configuration if we didn't get an account id from the credentials
            if (string.IsNullOrEmpty(awsAccountId))
            {
                agent.Logger.Debug("Using AccountId from configuration.");
                awsAccountId = agent.Configuration.AwsAccountId;
            }
        }
        catch (Exception e)
        {
            agent.Logger.Debug(e, "Unexpected exception in AmazonServiceClientWrapper.BeforeWrappedMethod(). Using AccountId from configuration.");
            awsAccountId = agent.Configuration.AwsAccountId;
        }

        // disable the wrapper if we get this far and there's no account id
        if (string.IsNullOrEmpty(awsAccountId))
        {
            agent.Logger.Warn("Unable to parse AWS Account ID from AWSCredentials or configuration. Further AWS Account ID parsing will be disabled.");
            _disableServiceClientWrapper = true;

            return Delegates.NoOp;
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
