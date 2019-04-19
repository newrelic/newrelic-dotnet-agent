$ErrorActionPreference = "SilentlyContinue"
. .\agent-build-scripts\windows\common\powershell\nuGet.ps1
RestoreNuGetPackages NewRelicAzureCloudCI\NewRelicAzureCloudCI.sln "https://www.nuget.org/api/v2"