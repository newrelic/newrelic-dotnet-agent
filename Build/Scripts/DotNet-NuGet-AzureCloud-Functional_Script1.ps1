$ErrorActionPreference = "SilentlyContinue"
. .\agent-build-scripts\windows\common\powershell\nuGet.ps1
RestoreNuGetPackages NewRelicAzureCloudCI\NewRelicAzureCloudCI.sln "http://win-nuget-repository.pdx.vm.datanerd.us:81/NuGet/Default;https://www.nuget.org/api/v2"