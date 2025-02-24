// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using ApplicationLifecycle;
using AzureFunctionInProcApplication;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(AzureFunctionInProcApplication.MyStartup))]
namespace AzureFunctionInProcApplication;

public class MyStartup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        // Add services to the container.
        builder.AddPidFileCreator();
    }
}

public static class PidFileCreatorExtensions
{
    public static IFunctionsHostBuilder AddPidFileCreator(this IFunctionsHostBuilder builder)
    {
        AppLifecycleManager.Log("Adding PIDFileCreator service");
        builder.Services.AddSingleton<PIDFileCreator>();

        return builder;
    }
}

public class PIDFileCreator
{
    private readonly string _port;

    public PIDFileCreator()
    {
        // --port arg to this app is different from the --port arg sent in the `func` invocation
        // so we have to pull it from a custom environment variable
        _port = Environment.GetEnvironmentVariable("AZURE_FUNCTION_APP_EVENT_HANDLE_PORT");
        if (string.IsNullOrEmpty(_port))
            throw new Exception("AZURE_FUNCTION_APP_EVENT_HANDLE_PORT environment variable not set");

        AppLifecycleManager.CreatePidFile();
    }

    public void WaitForTestCompletion()
    {
        AppLifecycleManager.WaitForTestCompletion(_port);
    }
}
