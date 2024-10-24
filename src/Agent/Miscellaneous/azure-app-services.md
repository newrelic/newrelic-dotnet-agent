# Azure App Services

## Choosing the Appropriate Windows Agent

### Not Self-Contained / No RuntimeIdentifier
Applications published to Windows App Services, without a `RuntimeIdentifier` for self-contained publish, will run on the x86 version of .NET Core 2.0. This is true regardless of the server architecture chosen.

These applications will need to use the x86 package of the agent.

### Self-Contained Applications
Applications published to Windows App Services, with an x64 `RuntimeIdentifier` for self-contained publish, will run as an x64 application.

These applications will need to use the x64 package of the agent.

An example of this configuration inside a `.csproj` file looks something like:

```
<PropertyGroup>
  <TargetFramework>netcoreapp2.0</TargetFramework>
  <RuntimeIdentifier>win81-x64</RuntimeIdentifier>
</PropertyGroup>
```

## Installation
* Create a `newrelic` directory in the root of your web site and extract the agent to it.
* Configure environment variables as follows:

```
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
CORECLR_PROFILER_PATH=D:\Home\site\wwwroot\newrelic\NewRelic.Profiler.dll
CORECLR_NEWRELIC_HOME=D:\Home\site\wwwroot\newrelic
```
Note: environment variables for Azure App Services are set in the `Application settings` configuration page for your app, under `App settings`.  You can verify the environment variables for your app by browsing to the `Environment` tab of your app's Kudu diagnostic console and then jumping to the `Environment Variables` section, e.g. `https://myappname.scm.azurewebsites.net/Env.cshtml#envVariables`

* Modify the location agent logs are written to in the `newrelic.config` file:

```
<log level="info" directory="D:\Home\LogFiles\NewRelic" />
```
## Troubleshooting
* Make sure that all files in the `newrelic` directory at the root of your app got published to Azure.
* Make sure the environment variables are set correctly.
