// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class AzureFunctionApplicationFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = "AzureFunctionApplication";
    private const string ExecutableName = "AzureFunctionApplication.exe";

    private const string InProcApplicationDirectoryName = "AzureFunctionInProcApplication";
    private const string InProcExecutableName = "AzureFunctionInProcApplication.dll";

    private const string TestTraceId = "12345678901234567890123456789012";
    private const string TestTraceParent = "1234567890123456";
    private const string TestTracingVendors = "rojo,congo";
    private const string TestOtherVendorEntries = "rojo=1,congo=2";
    private const string AccountId = "1";
    private const string Version = "0";
    private const int ParentType = 0;
    private const string AppId = "5043";
    private const string SpanId = "27ddd2d8890283b4";
    private const string TransactionId = "5569065a5b1313bd";
    private const string Sampled = "1";
    private const string Priority = "1.23456";
    private const string Timestamp = "1518469636025";

    protected AzureFunctionApplicationFixture(string functionNames, string targetFramework, bool enableAzureFunctionMode, bool isCoreApp = true, bool inProc = false)
        : base(new AzureFuncTool(inProc ? InProcApplicationDirectoryName : ApplicationDirectoryName, inProc ? InProcExecutableName : ExecutableName, targetFramework, ApplicationType.Bounded, true, isCoreApp, true, enableAzureFunctionMode, inProc))
    {
        CommandLineArguments = $"start --no-build --functions {functionNames} --language-worker ";

        CommandLineArguments += inProc ? "dotnet --dotnet " : "dotnet-isolated --dotnet-isolated ";

#if DEBUG
        // set a long timeout if you're going to debug into the function
        CommandLineArguments += "--timeout 600 --verbose ";
#endif

        AzureFunctionModeEnabled = enableAzureFunctionMode;
    }


    public void Get(string endpoint)
    {
        var address = $"http://{DestinationServerName}:{Port}/{endpoint}";
        var headers = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string> ("traceparent", $"00-{TestTraceId}-{TestTraceParent}-00"),
            new KeyValuePair<string, string> ("tracestate", $"{AccountId}@nr={Version}-{ParentType}-{AccountId}-{AppId}-{SpanId}-{TransactionId}-{Sampled}-" + Priority + $"-{Timestamp},{TestOtherVendorEntries}")
        };

        GetStringAndIgnoreResult(address, headers);
    }

    public void Post(string endpoint, string payload)
    {
        var address = $"http://{DestinationServerName}:{Port}/{endpoint}";
        var inputPayload = $$"""{"input":"{{payload}}"}""";
        var headers = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string> ("traceparent", $"00-{TestTraceId}-{TestTraceParent}-00"),
            new KeyValuePair<string, string> ("tracestate", $"{AccountId}@nr={Version}-{ParentType}-{AccountId}-{AppId}-{SpanId}-{TransactionId}-{Sampled}-" + Priority + $"-{Timestamp},{TestOtherVendorEntries}")
        };

        PostJson(address, inputPayload, headers);
    }

    public void PostToAzureFuncTool(string triggerName, string payload)
    {
        var address = $"http://{DestinationServerName}:{Port}/admin/functions/{triggerName}";

        var inputPayload = $$"""{"input":"{{payload}}"}""";
        PostJson(address, inputPayload);
    }

    public bool AzureFunctionModeEnabled { get; }

    public string FunctionHostOutput => ((AzureFuncTool)RemoteApplication).StandardOutput;
}

#region Isolated model fixtures

public class AzureFunctionApplicationFixtureHttpTriggerCoreOldest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureHttpTriggerCoreOldest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net8.0", true)
    {
    }
}

// TODO: will need to update this for net10.0
public class AzureFunctionApplicationFixtureHttpTriggerCoreLatest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureHttpTriggerCoreLatest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net10.0", true)
    {
    }
}

public class AzureFunctionApplicationFixtureHttpTriggerFWLatest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureHttpTriggerFWLatest() : base("httpTriggerFunctionUsingSimpleInvocation", "net481", true, false)
    {
    }
}

public class AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net10.0", false)
    {
    }
}

public class AzureFunctionApplicationFixtureQueueTriggerCoreOldest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureQueueTriggerCoreOldest() : base("queueTriggerFunction", "net8.0", true)
    {
    }
}

public class AzureFunctionApplicationFixtureQueueTriggerCoreLatest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureQueueTriggerCoreLatest() : base("queueTriggerFunction", "net10.0", true)
    {
    }
}

/// <summary>
/// Base for the Service Bus failure-path fixtures. Each run gets its own queue. The timeout function
/// never completes its message, so on a shared queue the service hands that message to the next test
/// that listens, which then records an extra transaction.
/// </summary>
public abstract class AzureFunctionServiceBusFailureFixture : AzureFunctionApplicationFixture
{
    private readonly string _queueName;

    private ServiceBusQueueScope _queueScope;

    protected AzureFunctionServiceBusFailureFixture(string functionNames)
        : base(functionNames, "net10.0", true)
    {
        _queueName = $"azure-func-test-failure-queue-{Guid.NewGuid()}";

        SetAdditionalEnvironmentVariable("ServiceBus", AzureServiceBusConfiguration.ConnectionString);
        SetAdditionalEnvironmentVariable(AzureServiceBusConfiguration.FuncTestFailureQueueNameSetting, _queueName);
    }

    public override void Initialize()
    {
        // xUnit constructs the test class once per test method, and each construction calls Initialize on
        // this shared fixture. The base call ignores every call after the first, so create the queue once.
        // Create it before the host starts: a listener that binds to a missing entity retries.
        _queueScope ??= ServiceBusQueueScope.Create(_queueName);

        base.Initialize();
    }

    public override void Dispose()
    {
        // Stop the app first. A listener that shuts down against a deleted queue logs errors.
        base.Dispose();

        _queueScope?.Dispose();
    }
}

public class AzureFunctionApplicationFixtureServiceBusTriggerThrowsCoreLatest : AzureFunctionServiceBusFailureFixture
{
    public AzureFunctionApplicationFixtureServiceBusTriggerThrowsCoreLatest()
        : base("ServiceBusTriggerFunction_Throws HttpTrigger_SendServiceBusMessage")
    {
    }
}

public class AzureFunctionApplicationFixtureServiceBusTriggerTimeoutCoreLatest : AzureFunctionServiceBusFailureFixture
{
    public static readonly TimeSpan FunctionTimeout = TimeSpan.FromSeconds(30);

    public AzureFunctionApplicationFixtureServiceBusTriggerTimeoutCoreLatest()
        : base("ServiceBusTriggerFunction_Timeout HttpTrigger_SendServiceBusMessage")
    {
        // The Functions host reads AzureFunctionsJobHost__<path> app settings as host.json overrides,
        // so this sets functionTimeout for this fixture only and leaves host.json alone.
        SetAdditionalEnvironmentVariable("AzureFunctionsJobHost__functionTimeout", FunctionTimeout.ToString(@"hh\:mm\:ss"));
    }
}
#endregion

#region InProc model fixtures
public class AzureFunctionApplicationFixtureHttpTriggerInProcCoreOldest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureHttpTriggerInProcCoreOldest() : base("HttpTriggerFunction", "net8.0", true, inProc: true)
    {
    }
}

public class AzureFunctionApplicationFixtureServiceBusTriggerInProcCoreOldest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureServiceBusTriggerInProcCoreOldest() : base("ServiceBusTriggerFunction HttpTrigger_SendServiceBusMessage", "net8.0", true, inProc: true)
    {
    }
}
#endregion
