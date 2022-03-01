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
                    if (provider == LogProviders.Log4NetProviderName)
                    {
                        LogProviders.RegisteredLogProvider[(int)LogProvider.Log4Net] = true;
                    }
                    else if (provider == LogProviders.SerilogProviderName)
                    {
                        LogProviders.RegisteredLogProvider[(int)LogProvider.Serilog] = true;
                    }
                }
            }

            return Delegates.NoOp;
        }
    }
}
