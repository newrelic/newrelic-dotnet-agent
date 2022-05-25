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
            if (LogProviders.Log4NetProviderNames.Contains(provider))
            {
                LogProviders.RegisteredLogProvider[(int)LogProvider.Log4Net] = true;
                agent.Logger.Log(Level.Info, "Detected log4net provider in use with Microsoft.Extensions.Logging, disabling log4net instrumentation.");
            }
            else if (LogProviders.SerilogProviderNames.Contains(provider))
            {
                LogProviders.RegisteredLogProvider[(int)LogProvider.Serilog] = true;
                agent.Logger.Log(Level.Info, "Detected Serilog provider in use with Microsoft.Extensions.Logging, disabling Serilog instrumentation.");
            }
            else if (LogProviders.NLogProviderNames.Contains(provider))
            {
                LogProviders.RegisteredLogProvider[(int)LogProvider.NLog] = true;
                agent.Logger.Log(Level.Info, "Detected NLog provider in use with Microsoft.Extensions.Logging, disabling NLog instrumentation.");
            }

            return Delegates.NoOp;
        }
    }
}
