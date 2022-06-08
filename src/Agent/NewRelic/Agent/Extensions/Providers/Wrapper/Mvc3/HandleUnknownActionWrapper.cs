// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Web;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Mvc3
{
    public class HandleUnknownActionWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo) => new CanWrapResponse(nameof(HandleUnknownActionWrapper).Equals(methodInfo.RequestedWrapperName));

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            return Delegates.GetDelegateFor(onFailure: exception =>
            {
                // Handle a missing Action after already being pushed through a valid Route and onto a Controller
                if (exception is HttpException he && he.GetHttpCode() == 404)
                {
                    transaction.SetWebTransactionName(WebTransactionType.StatusCode, "404",
                        TransactionNamePriority.FrameworkHigh);
                }
            });
        }
    }
}
