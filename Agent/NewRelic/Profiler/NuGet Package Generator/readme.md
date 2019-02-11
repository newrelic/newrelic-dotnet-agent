# NuGet Package Generator
This project dynamically generates a nuspec file and then packs.

It will package x86 and x64 versions `NewRelic.Profiler.dll`.

It will also package `libNewRelicProfiler.so`, if it exists for Linux distributions. This is x64 only.


## Running locally
The build/servers use an old version of nuget that results in a different location and folder structure.

To get around this, a couple command-line options have been added. The nuget path can be overriden and the new folder structure can be chosen.

#### Example
Ran from NewRelic.Profiler root

```
. ".\NuGet Package Generator\bin\Release\net451\NuGet Package Generator.exe" --solution="." --output="NuGet Package Generator\bin\Release\net451" --nugetPackageDir="C:\Users\<YOUR USER>\.nuget" --useModernNugetFolders
```
