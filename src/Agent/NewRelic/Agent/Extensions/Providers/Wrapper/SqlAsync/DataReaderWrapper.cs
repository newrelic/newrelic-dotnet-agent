// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.WrapperUtilities;

namespace NewRelic.Providers.Wrapper.SqlAsync
{
    public class DataReaderWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyNames: new[]
                {
                    "System.Data",
                    "System.Data.SqlClient"
                },
                typeNames: new[]
                {
                    "System.Data.SqlClient.SqlDataReader",
                },
                methodNames: new[]
                {
                    "NextResultAsync",
                    "ReadAsync"
                });

            if (canWrap)
            {
                return WrapperUtils.LegacyAspPipelineIsPresent()
                    ? new CanWrapResponse(false, WrapperUtils.LegacyAspPipelineNotSupportedMessage("System.Data", "System.Data.SqlClient.SqlDataReader", method.MethodName))
                    : new CanWrapResponse(true);

            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "DatabaseResult/Iterate");
            segment.MakeCombinable();

            return WrapperUtils.GetAsyncDelegateFor(agentWrapperApi, segment);
        }
    }
}
