// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
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
        public static string AwsAccountId { get; private set; }

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse(instrumentedMethodInfo.RequestedWrapperName == nameof(AmazonServiceClientWrapper));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (AwsAccountId != null)
                return Delegates.NoOp;

            try
            {
                // get the AWSCredentials parameter
                dynamic awsCredentials = instrumentedMethodCall.MethodCall.MethodArguments[0];

                dynamic immutableCredentials = awsCredentials.GetCredentials();
                string accessKey = immutableCredentials.AccessKey;

                // convert the access key to an account id
                AwsAccountId = AwsAccountIdDecoder.GetAccountId(accessKey);
            }
            catch (Exception e)
            {
                agent.Logger.Info($"Unable to parse AWS Account ID from AccessKey. Using AccountId from configuration instead. Exception: {e.Message}");
                AwsAccountId = agent.Configuration.AwsAccountId;
            }

            return Delegates.NoOp;
        }
    }
}
