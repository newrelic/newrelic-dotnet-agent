// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
#if NETFRAMEWORK
using System.Net;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport.Client;

/// <summary>
///     Pseudo "factory" implementation to get an instance of a NRWebRequestClient. Registered in DI
///     at startup if running in a .NET Framework build. Returns a new instance on every request, as singleton
///     management is not required.
/// </summary>
public class WebRequestHttpClientFactory : IHttpClientFactory
{
    private const string UseWinHttpHandlerAppSettingKey = "UseWinHttpHandlerForCollector";
    private const string UseWinHttpHandlerEnvironmentVariable = "NEW_RELIC_USE_WINHTTP_HANDLER_FOR_COLLECTOR";

    public WebRequestHttpClientFactory()
    {
    }

    public IHttpClient GetOrCreateClient(IWebProxy proxy, IConfiguration configuration)
    {
        if (ShouldUseWinHttpHandler(configuration))
        {
            Log.Info("UseWinHttpHandlerForCollector is enabled. Using NRHttpClient (WinHttpHandler) for collector requests.");
            return new NRHttpClient(proxy, configuration);
        }

        Log.Info("Using NRWebRequestClient for collector requests.");
        return new NRWebRequestClient(proxy, configuration);
    }

    private static bool ShouldUseWinHttpHandler(IConfiguration configuration)
    {
        if (configuration.GetAppSettings().TryGetValue(UseWinHttpHandlerAppSettingKey, out var appSettingValue)
            && bool.TryParse(appSettingValue, out var appSettingEnabled) && appSettingEnabled)
        {
            return true;
        }

        var environmentVariableValue = System.Environment.GetEnvironmentVariable(UseWinHttpHandlerEnvironmentVariable);
        return bool.TryParse(environmentVariableValue, out var environmentVariableEnabled) && environmentVariableEnabled;
    }
}
#endif
