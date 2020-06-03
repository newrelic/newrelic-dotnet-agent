[![Community Project header](https://github.com/newrelic/open-source-office/raw/master/examples/categories/images/Community_Project.png)](https://github.com/newrelic/open-source-office/blob/master/examples/categories/index.md#community-project)

# New Relic .NET Agent Integration Tests

Tests the integration of the New Relic .NET Agent with various .NET applications.  

## Installation

### Install required .NET SDKs and runtimes
Tests cover the integration of applications that target different versions of .NET Framework and .NET Core. The following versions of .NET Framework and Core are required:
1. .NET Framework 4.5.1
2. .NET Framework 4.5.2
3. .NET Framework 4.6.1
4. .NET Core 2.0
5. .NET Core 3.0
6. .NET Core 3.1

### Install required Windows features
The following Windows features are also required:
1. IIS Hostable Web Core
2. MSMQ Server

### Set up test secrets
A New Relic license key is required to run the tests. Some tests require special New Relic license keys for high security mode (HSM) and configurable security policies (CSP). Follow the steps below to set these license keys.

1. Create a `secrets.json` json file using the template below.  **Do *not* place the `secrets.json` file within the your local repo folder.**
2. Replace the license key placeholders in the `secrets.json` template with actual license keys. The `REPLACE_WITH_HIGH_SECURITY_LICENSE_KEY` and `REPLACE_WITH_SECURITY_POLICIES_CONFIGURABLE_LICENSE_KEY` are placeholders for license keys from a [High Security Mode](https://docs.newrelic.com/docs/agents/manage-apm-agents/configuration/high-security-mode) enabled account and a [Configurable Security Policies](https://docs.newrelic.com/docs/agents/manage-apm-agents/configuration/enable-configurable-security-policies) enabled account respectively. To find your license keys, visit [this page](https://docs.newrelic.com/docs/accounts/install-new-relic/account-setup/license-key).
3. Open a Windows command prompt and run this command `type {SECRET_FILE_PATH}\secrets.json | dotnet user-secrets set --project {DOTNET_AGENT_REPO_PATH}\IntegrationTests\Shared`. Replace `{SECRET_FILE_PATH}` and `{DOTNET_AGENT_REPO_PATH}` with the location of `secrets.json` file and the location of the repo repectively. A Successful message should appear if all the secrets are successfully installed. 

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
2. Running Visual Studio as an Administrator, open the `IntegrationTests.sln` solution and build the solution. After a successful build, the tests will show up in the Visual Studio test explorer window.
3. Run all tests or selected tests. 
