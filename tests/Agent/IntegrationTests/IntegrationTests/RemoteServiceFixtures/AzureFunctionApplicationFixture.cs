// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

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
    public AzureFunctionApplicationFixtureHttpTriggerCoreLatest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net9.0", true)
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
    public AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net9.0", false)
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
    public AzureFunctionApplicationFixtureQueueTriggerCoreLatest() : base("queueTriggerFunction", "net9.0", true)
    {
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
