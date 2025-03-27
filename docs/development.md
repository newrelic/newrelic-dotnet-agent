# Development

## Requirements
* [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/)
  * Workloads
    * .NET desktop development
    * Desktop development with C++
  * Individual components
    * C++ ATL for v142 build tools (x86 & x64)
* Optional installs:
  * [Docker Desktop for Windows](https://docs.docker.com/desktop/setup/install/windows-install/) for building the native Linux binaries, as well as running containerized integration tests.
  * [HeatWave](https://www.firegiant.com/heatwave/) is a Visual Studio extension that enables building the [agent MSI installer solution](../src/Agent/MsiInstaller/MsiInstaller.sln).

## Building

The New Relic .NET agent is primarily comprised of two solutions: `FullAgent.sln` and `Profiler.sln`.

### FullAgent.sln

To get started quickly, this is the only solution you need to build. Building this solution creates a number of home directories for each of the target platforms the .NET agent supports:

| Framework | OS | x64 / x86 | Default output location |
| --------- | -- | --------- | ----------------------- |
| .NET Framework | Windows | x64 | src/Agent/newrelichome_x64 |
| .NET Framework | Windows | x86 | src/Agent/newrelichome_x86 |
| .NET Core | Windows | x64 | src/Agent/newrelichome_x64_coreclr |
| .NET Core | Windows | x86 | src/Agent/newrelichome_x86_coreclr |
| .NET Core | Linux | x64 | src/Agent/newrelichome_x64_coreclr_linux |
| .NET Core | Linux | arm64 | src/Agent/newrelichome_arm64_coreclr_linux |

These home directories can be used to run and test the agent in your development environment.

You need to configure the following environment variables for the agent to attach to a process.

#### Environment variables for .NET Framework
```bash
NEWRELIC_LICENSE_KEY=<your New Relic license key>
NEWRELIC_HOME=path\to\home\directory
COR_ENABLE_PROFILING=1
COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}
COR_PROFILER_PATH=path\to\home\directory\NewRelic.Profiler.dll
```

#### Environment variables for .NET Core
```bash
NEWRELIC_LICENSE_KEY=<your New Relic license key>
CORECLR_NEWRELIC_HOME=path\to\home\directory
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
CORECLR_PROFILER_PATH=path\to\home\directory\NewRelic.Profiler.dll
```

### Profiler.sln

The Profiler.sln builds the native profiler component of the .NET agent. The profiler implements interfaces defined by the unmanaged [.NET Profiling API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/) that enable the agent to attach to and monitor a .NET process.  See [the profiler README](../src/Agent/NewRelic/Profiler/README.md) for more details.

As mentioned above, this solution does not need to be built if you will only be working with the agent's managed C# code. The profiler is [available as a NuGet package](https://www.nuget.org/packages/NewRelic.Agent.Internal.Profiler) and is referenced by the full agent solution from NuGet [here](https://github.com/newrelic/newrelic-dotnet-agent/blob/1f446c282811a0f2ccd71a088b35397a29d961a0/src/Agent/NewRelic/Home/Home.csproj#L16).  

You can use a Powershell [script](../src/Agent/NewRelic/Profiler/build/build.ps1) to build the profiler.

The script uses the following syntax:
```
./build.ps1 [-Platform <windows|linux|x86|x64>] [-Configuration <Debug|Release>]
```
Examples:

#### Building in debug mode for Windows x64
```
build.ps1 -Platform x64 -Configuration Debug
```

#### Building for linux (requires Docker Desktop and builds the profiler in a Linux container)
```
build.ps1 -Platform linux
```

#### Building in release mode for all target platforms (requires Docker Desktop for building the Linux binary)
```
build.ps1
```

#### Local profiler testing

In order to integrate local profiler changes with local builds of the FullAgent solution:

1. First, build the FullAgent solution from Visual Studio (creating the various agent home dirs in `src/Agent`).
2. Build the profiler for all platforms and architectures: `build.ps1` (which places the profiler artifacts in `src/Agent/_profilerBuild`)
3. Copy the relevant profiler .dll (Windows) or .so (Linux) to the appropriate agent home folder on your system, overwriting the version pulled from NuGet:
```
# Example shown for the Windows 64-bit profiler, testing with the Windows CoreCLR (.NET Core/.NET) version of the agent
copy C:\workspace\newrelic-dotnet-agent\src\Agent\_profilerBuild\x64-Release\NewRelic.Profiler.dll C:\workspace\newrelic-dotnet-agent\src\Agent\newrelichome_x64_coreclr\

# Example shown for the Linux profiler and Linux
copy C:\workspace\newrelic-dotnet-agent\src\Agent\_profilerBuild\linux-x64-release\libNewRelicProfiler.so C:\workspace\newrelic-dotnet-agent\src\Agent\newrelichome_x64_coreclr_linux\
```

## Testing

* Unit tests use the NUnit framework and are contained in the solutions. Run them using the Visual Studio Test Explorer.
* There is a suite of [integration tests](integration-tests.md). Refer to the separate documentation for setting up an environment to run the integration tests. Some integration tests require you to set up additional infrastructure (e.g., databases) and are therefore not easily run.

## Packaging

* MsiInstaller.sln
* Artifact builder
* Site extension
