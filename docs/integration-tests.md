# New Relic .NET agent integration tests

Tests the integration of the New Relic .NET agent with various .NET applications. This test suite will only run on Windows.

## Installation

### Additional items to install
* ASP.NET and web development workload for Visual Studio 2019.
* Required Windows features
  * Windows features can be enabled as follows:
    ```
    Enable-WindowsOptionalFeature -Online -FeatureName <featuren name>
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
      * To find your license keys, visit [this page](https://docs.newrelic.com/docs/accounts/install-new-relic/account-setup/license-key).

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
3. Run all tests or selected tests. 
