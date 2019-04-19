$ErrorActionPreference = "Stop"
. .\agent-build-scripts\windows\common\powershell\nuGet.ps1
RestoreNuGetPackages DotNet-Functional-NuGet-AgentAPI\DotNet-Functional-NuGet-AgentAPI.sln "https://www.nuget.org/api/v2"
