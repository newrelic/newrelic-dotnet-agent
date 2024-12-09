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
        /// <summary>
        /// The AWS account id.
        /// Parsed from the access key in the credentials of the client - or fall back to the configuration value if parsing fails.
        /// Assumes only a single account id is used in the application.
        /// </summary>
        //public static string AwsAccountId { get; private set; }

        // cache the account id per instance of AmazonServiceClient.Config
        public static ConcurrentDictionary<object, string> AwsAccountIdByClientConfigCache = new();

        private static ConcurrentHashSet<object> AmazonServiceClientInstanceCache = new();

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse(instrumentedMethodInfo.RequestedWrapperName == nameof(AmazonServiceClientWrapper));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            dynamic client = instrumentedMethodCall.MethodCall.InvocationTarget;

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
                object clientConfig = client.Config;

                // cache the account id using clientConfig as the key
                AwsAccountIdByClientConfigCache.TryAdd(clientConfig, awsAccountId);
            });
        }
    }
}
