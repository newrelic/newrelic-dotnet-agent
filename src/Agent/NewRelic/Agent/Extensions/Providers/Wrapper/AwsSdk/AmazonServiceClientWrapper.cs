// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public class AmazonServiceClientWrapper : IWrapper
    {
        // TODO: Can an application use a separate account ID per service? If so, need to cache by service type
        public static string AwsAccountId { get; private set; }

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse(instrumentedMethodInfo.RequestedWrapperName == nameof(AmazonServiceClientWrapper));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (AwsAccountId != null) // only look up the account id once. See comment above regarding caching by service type
                return Delegates.NoOp;

            // get the AWSCredentials parameter
            dynamic awsCredentials = instrumentedMethodCall.MethodCall.MethodArguments[0];

            dynamic immutableCredentials = awsCredentials.GetCredentials();
            string accessKey = immutableCredentials.AccessKey;

            // convert the access key to an account id; if an exception is thrown, fall back to getting the account id from config
            try
            {
                AwsAccountId = AwsAccountDecoder.GetAccountId(accessKey);
            }
            catch (Exception e)
            {
                agent.Logger.Info($"Unable to parse AWS Account ID from AccessKey. Using AccountId from configuration instead. Exception: {e.Message}");
                AwsAccountId = agent.Configuration.AwsAccountId;
            }

            return Delegates.NoOp;
        }
    }

    internal static class AwsAccountDecoder
    {
        // magic number
        private const long Mask = 140737488355200L;

        public static string GetAccountId(string awsAccessKeyId)
        {
            if (string.IsNullOrEmpty(awsAccessKeyId))
            {
                throw new ArgumentNullException(nameof(awsAccessKeyId), "AWS Access Key ID cannot be null or empty.");
            }

            if (awsAccessKeyId.Length < 14)
            {
                throw new ArgumentOutOfRangeException(nameof(awsAccessKeyId), "AWS Access Key ID must be at least 14 characters long.");
            }

            string accessKeyWithoutPrefix = awsAccessKeyId.Substring(4).ToLowerInvariant();
            long encodedAccount = Base32Decode(accessKeyWithoutPrefix);

            return ((encodedAccount & Mask) >> 7).ToString();
        }

        /// <summary>
        /// Character range is a-z, 2-7. 'a' being 0 and '7', 31.
        /// Characters outside of this range will be considered 0.
        /// </summary>
        /// <param name="src">The string to be decoded. Must be at least 10 characters.</param>
        /// <returns>A long containing first 6 bytes of the base 32 decoded data.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If src has less than 10 characters.</exception>
        private static long Base32Decode(string src)
        {
            if (src.Length < 10)
            {
                throw new ArgumentOutOfRangeException(nameof(src), "The input string must be at least 10 characters long.");
            }

            long baseValue = 0;
            for (int i = 0; i < 10; i++)
            {
                baseValue <<= 5;
                char c = src[i];
                switch (c)
                {
                    case >= 'a' and <= 'z':
                        baseValue += c - 'a';
                        break;
                    case >= '2' and <= '7':
                        baseValue += c - '2' + 26;
                        break;
                }
            }
            return baseValue >> 2;
        }
    }

}
