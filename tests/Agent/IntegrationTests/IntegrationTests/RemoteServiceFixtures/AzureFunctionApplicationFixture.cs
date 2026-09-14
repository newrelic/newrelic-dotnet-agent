// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Net;
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

    private readonly bool _inProc;

    // func is instrumented as well as the worker, and it keeps logging after the
    // worker goes quiet. Choosing the most recently written agent log therefore
    // lands on func's log, which never holds the worker's transactions. A test
    // that names its own log file through SetLogFileName still wins.
    protected override string AgentLogFileName
    {
        get
        {
            var configuredName = base.AgentLogFileName;
            if (!string.IsNullOrEmpty(configuredName))
            {
                return configuredName;
            }

            return _inProc ? "newrelic_agent_func.log" : "newrelic_agent_AzureFunctionApplication.log";
        }
    }

    protected AzureFunctionApplicationFixture(string functionNames, string targetFramework, bool enableAzureFunctionMode, bool isCoreApp = true, bool inProc = false)
        : base(new AzureFuncTool(inProc ? InProcApplicationDirectoryName : ApplicationDirectoryName, inProc ? InProcExecutableName : ExecutableName, targetFramework, ApplicationType.Bounded, true, isCoreApp, true, enableAzureFunctionMode, inProc))
    {
        _inProc = inProc;

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

        GetStringAndIgnoreResult(address, BuildDistributedTracingHeaders());
    }

    public void Post(string endpoint, string payload)
    {
        var address = $"http://{DestinationServerName}:{Port}/{endpoint}";
        var inputPayload = $$"""{"input":"{{payload}}"}""";

        PostJson(address, inputPayload, BuildDistributedTracingHeaders());
    }

    public void PostToAzureFuncTool(string triggerName, string payload)
    {
        var address = $"http://{DestinationServerName}:{Port}/admin/functions/{triggerName}";

        var inputPayload = $$"""{"input":"{{payload}}"}""";
        PostJson(address, inputPayload);
    }

    public void GetAndAssertStatusCode(string endpoint, HttpStatusCode expectedStatusCode)
    {
        var address = $"http://{DestinationServerName}:{Port}/{endpoint}";

        GetAndAssertStatusCode(address, expectedStatusCode, BuildDistributedTracingHeaders());
    }

    public bool AzureFunctionModeEnabled { get; }

    private List<KeyValuePair<string, string>> BuildDistributedTracingHeaders()
    {
        return new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string> ("traceparent", $"00-{TestTraceId}-{TestTraceParent}-00"),
            new KeyValuePair<string, string> ("tracestate", $"{AccountId}@nr={Version}-{ParentType}-{AccountId}-{AppId}-{SpanId}-{TransactionId}-{Sampled}-" + Priority + $"-{Timestamp},{TestOtherVendorEntries}")
        };
    }
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

public class AzureFunctionApplicationFixtureHttpTriggerThrowsCoreLatest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureHttpTriggerThrowsCoreLatest()
        : base("httpTriggerFunctionThatThrows httpTriggerFunctionUsingSimpleInvocation", "net10.0", true)
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
