# Development

## Requirements
* [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/)
  * Workloads
    * .NET desktop development
    * Desktop development with C++
  * Individual components
    * C++ ATL for v142 build tools (x86 & x64)
* Optional installs:
  * [Docker Desktop for Windows](https://hub.docker.com/editions/community/docker-ce-desktop-windows/) for building the native Linux binaries.
  * [WiX Toolset 3.11](https://wixtoolset.org/releases/) and the WiX Toolset Visual Studio 2019 Extension for building the Windows MSI installer.

## Building

The New Relic .NET agent (managed) is contained in `FullAgent.sln`.

### FullAgent.sln

To get started quickly, this is the only solution you need to build. Building this solution creates a number of home directories for each of the target platforms the .NET agent supports:

| Framework | OS | x64 / x86 | Default output location |
| --------- | -- | --------- | ----------------------- |
| .NET Framework | Windows | x64 | src/Agent/newrelichome_x64 |
| .NET Framework | Windows | x86 | src/Agent/newrelichome_x86 |
| .NET Core | Windows | x64 | src/Agent/newrelichome_x64_coreclr |
| .NET Core | Windows | x86 | src/Agent/newrelichome_x86_coreclr |
| .NET Core | Linux | x64 | src/Agent/newrelichome_x64_coreclr_linux |

These home directories can be used to run and test the agent in your development environment.

You need to configure the following environment variables for the agent to attach to a process.

#### Environment variables for .NET Framework
```bash
NEW_RELIC_LICENSE_KEY=<your New Relic license key>
NEWRELIC_HOME=path\to\home\directory
COR_ENABLE_PROFILING=1
COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}
COR_PROFILER_PATH=path\to\home\directory\NewRelic.Profiler.dll
```

#### Environment variables for .NET Core
```bash
NEW_RELIC_LICENSE_KEY=<your New Relic license key>
CORECLR_NEWRELIC_HOME=path\to\home\directory
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
CORECLR_PROFILER_PATH=path\to\home\directory\NewRelic.Profiler.dll
```

### Profiler

The New Relic .NET Profiler is the unmanaged part of the Agent. The profiler implements interfaces defined by the unmanaged [.NET Profiling API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/) that enable the agent to attach to and monitor a .NET process. The net35/main branch does not include source for the Profiler and the Profiler solution does not need to be built if you will only be working with the agent's managed C# code. Pre-built versions of the profiler for both Windows (x86 and x64) and Linux (x64) are checked into the repository (src/Agent/ProfilerBuildsForDevMachines) and are used for creating the home directories built by the FullAgent.sln. 

## Testing

* Unit tests use the NUnit framework and are contained in the solutions. Run them using the Visual Studio Test Explorer.
* There is a suite of [integration tests](./integration-tests.md). Refer to the separate documentation for setting up an environment to run the integration tests. Some integration tests require set up of additional infrastructure (e.g., databases) and are therefore not easily run.

## Packaging

* MsiInstaller.sln
* Artifact builder
* Site extension
