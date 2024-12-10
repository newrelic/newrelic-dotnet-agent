// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public class AmazonServiceClientWrapper : IWrapper
    {
        // cache the account id per instance of AmazonServiceClient.Config
        public static ConcurrentDictionary<object, string> AwsAccountIdByClientConfigCache = new();

        private static readonly ConcurrentHashSet<object> AmazonServiceClientInstanceCache = new();

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse(instrumentedMethodInfo.RequestedWrapperName == nameof(AmazonServiceClientWrapper));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            object client = instrumentedMethodCall.MethodCall.InvocationTarget;

            if (AmazonServiceClientInstanceCache.Contains(client)) // don't do anything if we've already seen this client instance
                return Delegates.NoOp;

            AmazonServiceClientInstanceCache.Add(client);

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
                AwsAccountIdByClientConfigCache.TryAdd(clientConfig, awsAccountId);
            });
        }
    }
}
