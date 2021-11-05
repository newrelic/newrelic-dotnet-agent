# New Relic .NET agent integration tests

Tests the integration of the New Relic .NET agent with various .NET applications.

This test suite can be run on both Windows and Linux, however:

* Only tests with the `[NetCoreTest]` attribute (which sets an XUnit trait named `RuntimeFramework` to `NetCore`) can run on Linux.
* Testing on Linux is done from the command line with `dotnet test`.
* The rest of this document describes how to set up and run the integration tests to be run from Visual Studio on Windows.

## Installation
Requires Visual Studio 2022 Version 17.0 Preview 7.0 or greater.

### Additional items to install
* ASP.NET and web development workload for Visual Studio 2022.
* The following .NET Core/.NET SDKs:
  * .NET Core 2.1 (out of support, but we currently still have tests which target this runtime)
  * .NET Core 2.2 (ditto)
  * .NET Core 3.1
  * .NET 5
  * .NET 6
* Required Windows features
  * Windows features can be enabled as follows:
    ```
    Enable-WindowsOptionalFeature -Online -FeatureName <feature name>
    ```
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
  * IIS-NetFxExtensibility45
  * IIS-Performance
  * IIS-RequestFiltering
  * IIS-Security
  * IIS-StaticContent
  * IIS-WebServer
  * IIS-WebServerManagementTools
  * IIS-WebServerRole
  * MSMQ-Container
  * MSMQ-Multicast
  * MSMQ-Server
  * MSMQ-Triggers
  * MSRDC-Infrastructure
  * WCF-Services45
  * WCF-TCP-PortSharing45
  
  * Full Powershell Script for convenience:
    ```
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
    Enable-WindowsOptionalFeature -Online -FeatureName  IIS-NetFxExtensibility45
    Enable-WindowsOptionalFeature -Online -FeatureName  IIS-Performance
    Enable-WindowsOptionalFeature -Online -FeatureName  IIS-RequestFiltering
    Enable-WindowsOptionalFeature -Online -FeatureName  IIS-Security
    Enable-WindowsOptionalFeature -Online -FeatureName  IIS-StaticContent
    Enable-WindowsOptionalFeature -Online -FeatureName  IIS-WebServer
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
  
* IBM DB2 Data Server Client
  * We use IBM Data Server Driver from the IBM Data Server Client installer in our unbounded integration tests for DB2. Unfortunately, simply referencing the appropriate dll in our tests is insufficient. A full install of Data Server Client is required on any machine running our unbounded integration test suite.
  * The data server client installer can be downloaded from [here](https://www.ibm.com/support/pages/ibm-data-server-client-packages-version-111-mod-4-fix-pack-4).

### Set up test secrets
* You must have a valid New Relic license key to run the tests. 
* The license key and other settings are accessed by the tests in a `secrets.json` file. [Here](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json) is an example.
* The [example](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json) includes placeholders for values unique to a user's environment.
* The [example](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json) includes values needed for all Integration Tests and Unbounded Integration Tests.
  * Not all values in the `secrets.json` are required if a user is running a subset of tests, and can be omitted for irrelevant tests.

* Some tests require special New Relic license keys for High Security Mode (HSM) or Configurable Security Policies (CSP). Follow the steps below to set these license keys:

  1. Create a `secrets.json` file using the template below or copy the [example](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/tests/Agent/IntegrationTests/UnboundedServices/example-secrets.json).  **Do *not* place the `secrets.json` file within your local repo folder.**
  2. Replace the license key placeholders in the `secrets.json` template with actual license keys. 
      * The `REPLACE_WITH_HIGH_SECURITY_LICENSE_KEY` and `REPLACE_WITH_SECURITY_POLICIES_CONFIGURABLE_LICENSE_KEY` are placeholders for license keys from a [HSM](https://docs.newrelic.com/docs/agents/manage-apm-agents/configuration/high-security-mode)-enabled account and a [CSP](https://docs.newrelic.com/docs/agents/manage-apm-agents/configuration/enable-configurable-security-policies)-enabled account, respectively. 
      * To find your license keys, visit [this page](https://docs.newrelic.com/docs/accounts/accounts-billing/account-setup/new-relic-license-key/).

* Once placeholder values have been replaced with actual values:

  * Open a Windows command prompt and run this command: 
    * ```type {SECRET_FILE_PATH}\secrets.json | dotnet user-secrets set --project {DOTNET_AGENT_REPO_PATH}\tests\Agent\IntegrationTests\Shared```
      * Replacing `{SECRET_FILE_PATH}` with the location of the edited `secrets.json` file
      * Replacing `{DOTNET_AGENT_REPO_PATH}`with the location of the local repo
    * A "successful" message indicates if all the secrets are successfully installed. 

### `secrets.json` file template:
```
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

## Run tests

1. Build the `FullAgent.sln`.
2. Running Visual Studio as an Administrator, open the `IntegrationTests.sln` solution and build the solution. After a successful build, the tests are listed in the Visual Studio test explorer window.
3. The recommended "Group By" order for the tests in the test explorer is `Project`, `Traits`, `Namespace`, `Class`.
4. The main `IntegrationTests` test project is multi-targeted to both a .NET Framework and a .NET Core version to support both Windows/.NET Framework and Linux testing.  If you are running tests from Visual Studio on Windows, it is only necessary to run the .NET Framework version of the tests.  (Note: this is the runtime of the **test code**, not the test target applications.  The variant of the New Relic .NET agent (Framework/Core) being tested depends on the latter.)
5. Run all tests or selected tests. 
