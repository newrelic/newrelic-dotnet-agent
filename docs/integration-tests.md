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

### Set up test secrets
You must have a New Relic license key to run the tests. Some tests require special New Relic license keys for high security mode (HSM) and configurable security policies (CSP). Follow the steps below to set these license keys.

1. Create a `secrets.json` file using the template below.  **Do *not* place the `secrets.json` file within your local repo folder.**
2. Replace the license key placeholders in the `secrets.json` template with actual license keys. The `REPLACE_WITH_HIGH_SECURITY_LICENSE_KEY` and `REPLACE_WITH_SECURITY_POLICIES_CONFIGURABLE_LICENSE_KEY` are placeholders for license keys from a [High Security Mode](https://docs.newrelic.com/docs/agents/manage-apm-agents/configuration/high-security-mode) enabled account and a [Configurable Security Policies](https://docs.newrelic.com/docs/agents/manage-apm-agents/configuration/enable-configurable-security-policies) enabled account respectively. To find your license keys, visit [this page](https://docs.newrelic.com/docs/accounts/install-new-relic/account-setup/license-key).
3. Open a Windows command prompt and run this command: `type {SECRET_FILE_PATH}\secrets.json | dotnet user-secrets set --project {DOTNET_AGENT_REPO_PATH}\IntegrationTests\Shared`. Replace `{SECRET_FILE_PATH}` and `{DOTNET_AGENT_REPO_PATH}` with the location of the `secrets.json` file and the location of the repo repectively. You'll get a "successful" message if all the secrets are successfully installed. 

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
