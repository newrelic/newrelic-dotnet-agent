// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Microsoft.Extensions.Logging;
using NewRelic.Agent.Extensions.Logging;

namespace MicrosoftExtensionsLogging
{
	public class LoggerFactoryWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        private const string WrapperName = "LoggerFactoryWrapper";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var providers = (ILoggerProvider[])instrumentedMethodCall.MethodCall.MethodArguments[0];

            if (providers != null)
            {
                for (int i = 0; i < providers.Length; i++)
                {
                    var provider = providers[i].ToString();
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
                }
            }

            return Delegates.NoOp;
        }
    }
}
