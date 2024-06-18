// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Microsoft.Extensions.Logging;
using NewRelic.Agent.Extensions.Logging;

namespace MicrosoftExtensionsLogging
{
	public class AddProviderRegistrationWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        private const string WrapperName = "AddProviderRegistrationWrapper";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var provider = instrumentedMethodCall.MethodCall.MethodArguments[0].ToString();
            if (LogProviders.KnownMELProviders.Contains(provider))
            {
                LogProviders.KnownMELProviderEnabled = true;
                agent.Logger.Log(Level.Info, $"Known log provider {provider} in use. Disabling Microsoft.Extensions.Logging instrumentation.");
            }
            else
            {
                agent.Logger.Log(Level.Info, $"Log provider {provider} will use Microsoft.Extensions.Logging instrumentation.");
            }

            return Delegates.NoOp;
        }
    }
}
