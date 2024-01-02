# New Relic .NET agent integration tests

Tests the integration of the New Relic .NET agent with various .NET applications.

This test suite can be run on both [Windows](#testing-on-windows-with-visual-studio) and [Linux](#testing-on-linux-with-dotnet-test).

These tests execute against valid New Relic accounts and test a variety of features.

## Testing on Windows with Visual Studio

Visual Studio 2022 or greater preferred.

Visual Studio 2019 may work with .NET 6 additionally installed.

### Additional install requirements

#### ASP.NET and web development workload

Install the "ASP.NET and web development" workload in the "Web & Cloud" category on the workload tab.

#### .NET Core / .NET SDKs and targeting packs

Depending on which version of Visual Studio you are using, you may have to install some or all of the following:

* .NET 6
* .NET 7
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
* IIS-ASPNET45
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
Enable-WindowsOptionalFeature -Online -FeatureName  IIS-ASPNET45
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

* Some tests require special New Relic license keys for High Security Mode (HSM) or Configurable Security Policies (CSP). Follow the steps below to set these license keys:

  1. Create a `secrets.json` file using the template below or copy the [example](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json).  **Do *not* place the `secrets.json` file within your local repo folder.**
  2. Replace the license key placeholders in the `secrets.json` template with actual license keys.
      * The `REPLACE_WITH_HIGH_SECURITY_LICENSE_KEY` and `REPLACE_WITH_SECURITY_POLICIES_CONFIGURABLE_LICENSE_KEY` are placeholders for license keys from a [HSM](https://docs.newrelic.com/docs/agents/manage-apm-agents/configuration/high-security-mode)-enabled account and a CSP-enabled account, respectively.
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
      },
      "CSP" : {
        "LicenseKey": "REPLACE_WITH_SECURITY_POLICIES_CONFIGURABLE_LICENSE_KEY",
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

#### HSM / CSP tests

High security mode (HSM) tests or Configurable Security Policies (CSP) tests require matching settings on the account they run against. These typically have "HSM" or "CSP" (or the full names) in the test or fixture naming.

See the test secrets section above on configuring an appropriate account.

#### Selenium tests

We currently have one test that executes a JavaScript ajax request via Selenium. This requires Internet Explorer to be installed and does not yet work with Edge.

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

The W3C validation tests requiring installing Python 3.x and a dependency. See the "Python and dependencies" section above.

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

You will need to install the .NET SDKs for .NET Core 3.1, .NET 5, .NET 6, and .NET 7.

```
sudo apt-get update -q -y && sudo apt-get install -q -y curl
sudo mkdir -p /usr/share/dotnet

sudo curl -sSL https://dotnetcli.azureedge.net/dotnet/Sdk/3.1.414/dotnet-sdk-3.1.414-linux-x64.tar.gz | sudo tar -xzC /usr/share/dotnet
sudo curl -sSL https://dotnetcli.azureedge.net/dotnet/Sdk/5.0.401/dotnet-sdk-5.0.401-linux-x64.tar.gz | sudo tar -xzC /usr/share/dotnet
sudo curl -sSL https://dotnetcli.azureedge.net/dotnet/Sdk/6.0.100/dotnet-sdk-6.0.100-linux-x64.tar.gz | sudo tar -xzC /usr/share/dotnet
sudo curl -sSL https://dotnetcli.azureedge.net/dotnet/Sdk/7.0.100/dotnet-sdk-7.0.100-linux-x64.tar.gz | sudo tar -xzC /usr/share/dotnet

sudo ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
dotnet --list-sdks
```

### Set up test secrets

Refer to the section above in the Windows setup instructions regarding configuring test secrets.  Everything is the same, except that the command for adding the secrets looks like:

`cat {SECRET_FILE_PATH}/secrets.json | dotnet user-secrets set --project {DOTNET_AGENT_REPO_PATH}/tests/Agent/IntegrationTests/Shared`

### Run tests

As previously mentioned, the agent solution needs to be built on Windows.  If you are using an Ubuntu VM in WSL, you can use this workflow to run the agent integration tests on Linux:

1. Build the FullAgent.sln in Visual Studio.
2. Copy the agent repo to the Ubuntu VM.  The VM's filesystem can be accessed from the Windows host using this path: `\\wsl$\Ubuntu-20.04` (replace `Ubuntu-20.04` with the name of your VM if it's different).
3. In the VM, from the shell:

```
cd {DOTNET_AGENT_REPO_PATH}/tests/Agent/IntegrationTests/IntegrationTests
sudo dotnet test -f netcoreapp3.1 -c Release --filter RuntimeFramework=NetCore
```

For more details on how to use dotnet test, see https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test.
