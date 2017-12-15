$ErrorActionPreference = "Stop"
. .\agent-build-scripts\windows\common\powershell\nuGet.ps1
RestoreNuGetPackages DotNet-Functional-NuGet-AgentAPI\DotNet-Functional-NuGet-AgentAPI.sln "http://win-nuget-repository.pdx.vm.datanerd.us:81/NuGet/Default;https://www.nuget.org/api/v2"