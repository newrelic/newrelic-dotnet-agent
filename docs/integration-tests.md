# New Relic .NET agent integration tests

Tests the integration of the New Relic .NET agent with various .NET applications.

This test suite can be run on both [Windows](#testing-on-windows-with-visual-studio) and [Linux](#testing-on-linux-with-dotnet-test).

These tests execute against valid New Relic accounts and test a variety of features.

There is also a separate `ContainerIntegrationTests.sln` solution that runs instrumented Linux test applications inside Docker containers, requiring Docker Desktop. See [ContainerIntegrationTests/README.md](../tests/Agent/IntegrationTests/ContainerIntegrationTests/README.md) for how to add a new container test.

## Testing on Windows with Visual Studio

Visual Studio 2022 or greater required.

All generally available and supported .NET runtimes should be installed (e.g. .NET 8 and .NET 9 as of March 2025)

### Additional install requirements

#### ASP.NET and web development workload

Install the "ASP.NET and web development" workload in the "Web & Cloud" category on the workload tab.

#### .NET Core / .NET SDKs and targeting packs

Depending on which version of Visual Studio you are using, you may have to install some or all of the following:

* .NET 8
* .NET 9
* .NET Framework 4.7.1 targeting pack
* .NET Framework 4.8.0 targeting pack
* .NET Framework 4.8.1 targeting pack

#### Windows features

Windows features can be enabled via PowerShell as follows:

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName <feature name>
```

Or manually via search -> "Turn windows features on or off".

Install first:
* IIS-WebServer (most features require this to be enabled first)
* IIS-NetFxExtensibility45 (required by IIS-ASPNET45)

Install second:

* IIS-ApplicationDevelopment
* IIS-CommonHttpFeatures
* IIS-DefaultDocument
* IIS-DirectoryBrowsing
* IIS-HealthAndDiagnostics
* IIS-HostableWebCore
* IIS-HttpCompressionStatic
* IIS-HttpErrors
* IIS-HttpLogging
* IIS-ISAPIExtensions
* IIS-ISAPIFilter
* IIS-ManagementConsole
* IIS-Performance
* IIS-RequestFiltering
* IIS-Security
* IIS-StaticContent
* IIS-WebServerManagementTools
* IIS-WebServerRole
* IIS-ASPNET45
* MSMQ-Container
* MSMQ-Multicast
* MSMQ-Server
* MSMQ-Triggers
* MSRDC-Infrastructure
* WCF-Services45
* WCF-TCP-PortSharing45

Full Powershell Script for convenience:

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-WebServer
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-NetFxExtensibility45
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-ApplicationDevelopment
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-CommonHttpFeatures
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-DefaultDocument
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-DirectoryBrowsing
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-HealthAndDiagnostics
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-HostableWebCore
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-HttpCompressionStatic
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-HttpErrors
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-HttpLogging
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-ISAPIExtensions
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-ISAPIFilter
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-ManagementConsole
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-Performance
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-RequestFiltering
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-Security
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-StaticContent
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-WebServerManagementTools
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName  MSMQ-Container
Enable-WindowsOptionalFeature -Online -FeatureName  MSMQ-Multicast
Enable-WindowsOptionalFeature -Online -FeatureName  MSMQ-Server
Enable-WindowsOptionalFeature -Online -FeatureName  MSMQ-Triggers
Enable-WindowsOptionalFeature -Online -FeatureName  MSRDC-Infrastructure
Enable-WindowsOptionalFeature -Online -FeatureName  WCF-Services45
Enable-WindowsOptionalFeature -Online -FeatureName  WCF-TCP-PortSharing45
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-ASPNET45
```

#### Trusting the .NET SDK Development SSL Certificate

Some integration tests use a "mock collector" to simulate agent commands being sent from the real New Relic backend.  This service requires the use of https, and is configured to use the .NET SDK localhost development SSL certificate, which needs to be trusted on the system for the agent to connect to the mock collector successfully.

On Windows, run the following command:
`dotnet dev-certs https --trust`

and click "Yes" when prompted to install the certificate.  See https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs for more details.

For Linux, you'll need to perform distro-specific steps to trust the development certificate.

#### Python and dependencies

The W3CValidation test runs the test suite from https://github.com/w3c/trace-context/tree/main/test against the agent. Please see that repository for current requirements.

This currently consists of needing to install:
* Python 3.x
* aiohttp (`pip install aiohttp`)

**NOTE: Python will need to be in your `PATH` env var to be picked up by the testing.**

### Set up test secrets

The integration tests require round-trip communication with valid New Relic accounts.

* You must have a valid New Relic license key to run the tests.
* The license key and other settings are accessed by the tests in a `secrets.json` file. [Here](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json) is an example.
* The [example](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json) includes placeholders for values unique to a user's environment.
* The [example](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json) includes values needed for all Integration Tests and Unbounded Integration Tests.
  * Not all values in the `secrets.json` are required if a user is running a subset of tests, and can be omitted for irrelevant tests.

* Some tests require a special New Relic license key for High Security Mode (HSM). Follow the steps below to set this license key:

  1. Create a `secrets.json` file using the template below or copy the [example](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json).  **Do *not* place the `secrets.json` file within your local repo folder.**
  2. Replace the license key placeholders in the `secrets.json` template with actual license keys.
      * The `REPLACE_WITH_HIGH_SECURITY_LICENSE_KEY` is a placeholder for a license key from a [HSM](https://docs.newrelic.com/docs/agents/manage-apm-agents/configuration/high-security-mode)-enabled account.
      * To find your license keys, visit [this page](https://docs.newrelic.com/docs/accounts/accounts-billing/account-setup/new-relic-license-key/).

* Once placeholder values have been replaced with actual values:

  * Open a Windows command prompt and run this command:
    * ```type {SECRET_FILE_PATH}\secrets.json | dotnet user-secrets set --project {DOTNET_AGENT_REPO_PATH}\tests\Agent\IntegrationTests\Shared```
      * Replacing `{SECRET_FILE_PATH}` with the location of the edited `secrets.json` file
      * Replacing `{DOTNET_AGENT_REPO_PATH}`with the location of the local repo
    * A "successful" message indicates if all the secrets are successfully installed.

### `secrets.json` file template

```json
{
  "IntegrationTestConfiguration": {
    "DefaultSetting": {
      "LicenseKey": "REPLACE_WITH_LICENSE_KEY",
      "Collector": "collector.newrelic.com"
    },
    "TestSettingOverrides": {
      "HSM": {
        "LicenseKey": "REPLACE_WITH_HIGH_SECURITY_LICENSE_KEY"
      }
    }
  }
}
```

### Test application requirements

Some tests require specific settings on the applications themselves. Below is a summary of requirements, some of which have been mentioned above.

#### Browser / RUM tests

Tests that verify the browser agent (RUM) require the browser agent is configured for the application on the account matching the configured license key.

All of these currently use the application name: "IntegrationTestAppName".

* NewRelic.Agent.IntegrationTests.AgentFeatures.CallStackFallbackMvc.Test
* NewRelic.Agent.IntegrationTests.AgentFeatures.GetBrowserTimingHeader.Test
* NewRelic.Agent.IntegrationTests.AgentFeatures.GetBrowserTimingHeaderAutoOn.Test
* NewRelic.Agent.IntegrationTests.BasicInstrumentation.BasicMvcApplication.Test
* NewRelic.Agent.IntegrationTests.BasicInstrumentation.BasicMvcApplicationWithAsyncDisabled.Test
* NewRelic.Agent.IntegrationTests.BasicInstrumentation.MvcRum.Test

#### HSM tests

High security mode (HSM) tests require matching settings on the account they run against. These typically have "HSM" (or the full name) in the test or fixture naming.

See the test secrets section above on configuring an appropriate account.

#### LLM / Bedrock tests

The Bedrock tests in the `LLM` namespace call the real AWS Bedrock service. They
no longer use a static access key.

Before running them:

1. Sign in to AWS however you normally do, and confirm it worked:

   ```
   aws sts get-caller-identity
   ```

2. Make sure `AwsRegion` is present under `DefaultSetting` in your
   `secrets.json`, set to `us-west-2`.

That is all. `AwsTestCredentials` runs at test startup, asks the AWS CLI for your
current credentials, and passes them to the test application as environment
variables. You do not need to set `AWS_PROFILE` or `AWS_REGION` yourself, and you
do not need to export credentials by hand.

Two things are worth knowing if this ever fails:

* The AWS SDK for .NET cannot read an AWS SSO session on its own. SSO credential
  resolution lives in the `AWSSDK.SSO` and `AWSSDK.SSOOIDC` packages, which the
  test applications deliberately do not reference. That is why the credentials
  come via the AWS CLI rather than from the SDK's own profile handling. CI works
  the same way: `aws-actions/configure-aws-credentials` exports credentials into
  the job environment, and `AwsTestCredentials` sees they are already present and
  does nothing.
* `AWS_REGION` has no effect on these tests. The Bedrock client is constructed
  with an explicit region taken from `AwsRegion` in your `secrets.json`, so that
  setting is the one that matters.

Three test classes exercise models Bedrock still offers in us-west-2:

* `BedrockInvokeTests` -- `amazon.titan-embed-text-v1`
* `BedrockConverseTests` -- `us.amazon.nova-micro-v1:0`
* `BedrockConverseContentBlockTests` -- `us.anthropic.claude-sonnet-4-5-20250929-v1:0`

`LLMDisabledTests` and `LLMErrorTests` still name `meta.llama2-13b-chat-v1` and
`meta.llama2-70b-chat-v1`, which Bedrock has retired along with the rest of the
Llama 2 and Jurassic-2 families. They pass anyway, because neither asserts a
successful completion: `LLMDisabledTests` runs with AI monitoring disabled and
checks that no LLM events are produced, and `LLMErrorTests` checks that a failed
call produces an error event. A retired model and an IAM-denied model produce the
same error shape, so both satisfy it.

`LLMApiTests` and `LLMAccountDisabledTests` have their `[Fact]` attributes
commented out for the same deprecation, so they do not run at all. Migrating
those to current models would need a change to the agent's own model-ID handling
in `BedrockLlmModelTypeExtensions`, not just the tests, and is tracked
separately.

`LLM` is excluded from CI via the `INTEGRATION_EXCLUDE_NAMESPACES` repository
variable, for reasons unrelated to Bedrock.

The CI role grants only `bedrock:InvokeModel`. The Converse API authorizes
against that action, but `ConverseStream` would need
`bedrock:InvokeModelWithResponseStream`, which is deliberately not granted
because nothing in the repo calls it. A new streaming Bedrock call will get a
403 until the policy is updated.

#### Selenium tests

We currently have one test that executes a JavaScript ajax request via Selenium. This requires Chrome to be installed.

* NewRelic.Agent.IntegrationTests.BasicInstrumentation.BasicAspWebService.Test

#### Metric normalization tests

We currently have one test that exercises our metrics normalization rules. This test relies on one set of rules that can be configured by anyone and one set of rules only configurable by New Relic employees. As such, you may not be able to configure this to pass at this time.

* NewRelic.Agent.IntegrationTests.AgentFeatures.Rules.Test

The test application is named: "RulesWebApi".

Segment terms must be configured for the account and test application. Terms can be updated by NR employees here: `https://[staging].newrelic.com/accounts/{accountId}/applications/{applicationId}/segment_terms`.

| Metric prefix         | Terms                |
| --------------------- | ---------------------|
| WebTransaction/WebAPI | Values/Sleep/UrlRule |

Please note that the terms should be entered with a space delimiter: `Values Sleep UrlRule`.

A URL rule must also be set. This can be set by anyone via the Metric normalization page: `https://[staging-]one.newrelic.com/nr1-core/metric-normalization-rules/view-rules/{entityGuid}`.

| Order | Match                             | Replacement                      | Actions | Target      | Terminate? | Active |
| ----- | --------------------------------- | -------------------------------- | ------- | ----------- | ---------- | ------ |
| 0     | WebTransaction/WebAPI/.\*/UrlRule | WebTransaction/WebAPI/\*/UrlRule | Replace | RulesWebApi | true       | true   |

For more on metric normalization rules see: https://docs.newrelic.com/docs/new-relic-solutions/new-relic-one/ui-data/metric-normalization-rules/.

#### W3C validation tests

The W3C validation tests requiring installing Python 3.x and a dependency (`aiohttp`). See the "Python and dependencies" section above.

#### Distributed Tracing tests

Most of the distributed tracing tests require specific accounts for trusted account checks. This is not currently configurable.

These typically have "DistributedTracing" or "DT" in the name and are spread throughout several test fixtures.

#### Infinite Tracing tests

Infinite tracing tests require the `TraceObserverUrl` be configured for a trace observer configured for the account. This is a special feature that non New Relic employees likely do not want to configure just for testing.

See the "Set up test secrets" section for how to set and for template examples.

### Run tests

1. Build the `FullAgent.sln`.
2. Running Visual Studio as an Administrator, open the `IntegrationTests.sln` solution and build the solution. After a successful build, the tests are listed in the Visual Studio test explorer window.
3. The recommended "Group By" order for the tests in the test explorer is `Project`, `Traits`, `Namespace`, `Class`.
4. The main `IntegrationTests` test project is multi-targeted to both a .NET Framework and a .NET Core version to support both Windows/.NET Framework and Linux testing.  If you are running tests from Visual Studio on Windows, it is only necessary to run the .NET Framework version of the tests.  (Note: this is the runtime of the **test code**, not the test target applications.  The variant of the New Relic .NET agent (Framework/Core) being tested depends on the latter.)
5. Run all tests or selected tests.

## Testing on Linux with dotnet test

First, a few caveats:

* Only tests with the `[NetCoreTest]` attribute (which sets an XUnit trait named `RuntimeFramework` to `NetCore`) can run on Linux.
* The agent solution still needs to be built on Windows in Visual Studio, or from the command line using the [build.ps1](../build/build.ps1) script (which uses Visual Studio tooling).

We recommend using [WSL](https://docs.microsoft.com/en-us/windows/wsl/about) to install an Ubuntu 20.04+ VM on your Windows 10+ development system.

### Linux system setup

You will need to install the .NET SDKs for currently available and supported .NET runtimes (e.g. .NET 8 and .NET 9 as of March 2025).

See Microsoft's documentation for how to install the required SDKs for your particular Linux distro and hardware platform: https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual

### Set up test secrets

Refer to the section above in the Windows setup instructions regarding configuring test secrets.  Everything is the same, except that the command for adding the secrets looks like:

`cat {SECRET_FILE_PATH}/secrets.json | dotnet user-secrets set --project {DOTNET_AGENT_REPO_PATH}/tests/Agent/IntegrationTests/Shared`

### Run tests

As previously mentioned, the agent solution needs to be built on Windows.  If you are using an Ubuntu VM in WSL, you can use this workflow to run the agent integration tests on Linux:

1. Build the FullAgent.sln in Visual Studio.
2. Copy the agent repo to the Ubuntu VM.  The VM's filesystem can be accessed from the Windows host using this path: `\\wsl$\Ubuntu-24.04` (replace `Ubuntu-24.04` with the name of your VM if it's different).
3. In the VM, from the shell:

```
cd {DOTNET_AGENT_REPO_PATH}/tests/Agent/IntegrationTests/IntegrationTests
sudo dotnet test -f net10.0 -c Release --filter RuntimeFramework=NetCore
```

For more details on how to use dotnet test, see https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test.
