// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class AzureFunctionApplicationFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = @"AzureFunctionApplication";
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

    protected AzureFunctionApplicationFixture(string functionNames, string targetFramework, bool enableAzureFunctionMode)
        : base(new AzureFuncTool(ApplicationDirectoryName, targetFramework, ApplicationType.Bounded, true, true, true, enableAzureFunctionMode))
    {
        CommandLineArguments = $"start --no-build --language-worker dotnet-isolated --dotnet-isolated --functions {functionNames} ";

#if DEBUG
        // set a long timeout if you're going to debug into the function
        CommandLineArguments += "--timeout 600 ";
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

    public void PostToAzureFuncTool(string triggerName, string payload)
    {
        var address = $"http://{DestinationServerName}:{Port}/admin/functions/{triggerName}";

        var inputPayload = $$"""{"input":"{{payload}}"}""";
        PostJson(address, inputPayload);
    }

    public bool AzureFunctionModeEnabled { get; }
}

public class AzureFunctionApplicationFixtureHttpTriggerCoreOldest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureHttpTriggerCoreOldest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net8.0", true)
    {
    }
}
public class AzureFunctionApplicationFixtureHttpTriggerCoreLatest : AzureFunctionApplicationFixture
{
    public AzureFunctionApplicationFixtureHttpTriggerCoreLatest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net9.0", true)
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