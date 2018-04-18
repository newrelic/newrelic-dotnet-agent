$ErrorActionPreference = "Stop"

# Annotate the build
$agentVersion = [Reflection.AssemblyName]::GetAssemblyName("$env:WORKSPACE\CopiedArtifacts\Agent\_build\AnyCPU-Release\NewRelic.Api.Agent\net45\NewRelic.Api.Agent.dll").Version.ToString()
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$agentVersion" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

# Copy the api .dll
Remove-Item -Recurse -Force lib
New-Item lib -Type Directory
New-Item lib\net45 -Type Directory
New-Item lib\netstandard2.0 -Type Directory
Copy-Item CopiedArtifacts\Agent\_build\AnyCPU-Release\NewRelic.Api.Agent\net35\NewRelic.Api.Agent.dll lib\net35
Copy-Item CopiedArtifacts\Agent\_build\AnyCPU-Release\NewRelic.Api.Agent\netstandard2.0\NewRelic.Api.Agent.dll lib\netstandard2.0

# Update the version number in the .nuspec file, update the release notes
$packageName = "NewRelic.Agent.Api"
$nuspecPath = "$packageName.nuspec"
$nupkgPath = "$packageName.nupkg"
$packageContentTestsPath = "nuget-test\tests\package_content.Tests.ps1"
$packageLibTestsPath = "nuget-test\tests\package_lib.Tests.ps1"

# Remove the old .nupkg file
Remove-Item $nupkgPath

[Xml]$nuspec = Get-Content $nuspecPath
$nuspec.package.metadata.version = $agentVersion
$nuspec.Save($nuspecPath)

# Set the version number in the 'package_content.Tests.ps1' file
$regex = "[0-9]+.[0-9]+.[0-9]+.[0-9]`""
(Get-Content $packageContentTestsPath) | ForEach-Object { $_ -replace $regex, "$agentVersion`"" } | Set-Content $packageContentTestsPath

# Set the version number in the 'package_lib.Tests.ps1' file
$regex = "version = `"[0-9]+.[0-9]+.[0-9]+.[0-9]`""
(Get-Content $packageLibTestsPath) | ForEach-Object { $_ -replace $regex, "version = `"$agentVersion`"" } | Set-Content $packageLibTestsPath

# Build the NuGet package
. C:\NuGet.exe Pack $nuspecPath

# Push the package to the internal repository
. .\agent-build-scripts\windows\common\powershell\pushToInternalNuGetServer.ps1

# Rename the generated .nupkg file, removing the version
Rename-Item "$packageName.$($agentVersion).nupkg" "$nupkgPath"