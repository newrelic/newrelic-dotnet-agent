$ErrorActionPreference = "Stop"

# Annotate the build
$agentVersion = [Reflection.AssemblyName]::GetAssemblyName("$env:WORKSPACE\CopiedArtifacts\Agent\_build\AnyCPU-Release\NewRelic.Api.Agent\net45\NewRelic.Api.Agent.dll").Version.ToString()

$annotation = "$env:Repository - $agentVersion"
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$annotation" -Method POST -Headers @{'Authorization'=$authorization}

# Purge the contents of 'content\newrelic'
Remove-Item -Path content\newrelic\* -Force -Recurse
#New-Item -Path content\newrelic -Type directory

# Copy the artifacts
if ($env:Repository.Contains("x64"))
{
    Write-Host "64-bit"
    Copy-Item "CopiedArtifacts\Agent\New Relic Home x64\*" -Destination content\newrelic -Force -Recurse
}
else
{
    Write-Host "32-bit"
    Copy-Item "CopiedArtifacts\Agent\New Relic Home x86\*" -Destination content\newrelic -Force -Recurse
}
Copy-Item CopiedArtifacts\Agent\_build\AnyCPU-Release\NewRelic.Api.Agent\net45\NewRelic.Api.Agent.dll lib -Force

# Remove PDBs before committing
Remove-Item content\newrelic\* -recurse -include *.pdb

# Update the 'newrelic.config' file
[Xml]$config = Get-Content .\content\newrelic\newrelic.config
$ns = New-Object Xml.XmlNamespaceManager $config.NameTable
$ns.AddNamespace("x", "urn:newrelic-config")

# Remove the 'application' element
$node = $config.SelectSingleNode("//x:configuration/x:application", $ns)
$node.ParentNode.RemoveChild($node)

# Re-create the 'application' element
$nodeLog = $config.SelectSingleNode("//x:configuration/x:log", $ns)
$app = $config.CreateElement("application", "urn:newrelic-config")
$config.configuration.InsertBefore($app, $nodeLog)

# Set the 'directory' attribute
$config.configuration.log.SetAttribute("directory", "c:\Home\LogFiles\NewRelic")
$config.Save("$env:WORKSPACE\content\newrelic\newrelic.config")

# Update the version number in the .nuspec file, update the release notes
if ($env:Repository.Contains("x64"))
{
     $nuspecPath = "NewRelic.Azure.WebSites.x64.nuspec"
}
else
{
     $nuspecPath = "NewRelic.Azure.WebSites.nuspec"
}

# Remove the old .nupkg file
Remove-Item NewRelic.Azure.WebSites*.nupkg

# Update the .nuspec file
[Xml]$nuspec = Get-Content $nuspecPath
$nuspec.package.metadata.version = $agentVersion
$nuspec.Save($nuspecPath)

# Set the version number in the 'package_content.Tests.ps1' file
$regex = "agentVersion = `"[0-9]+.[0-9]+.[0-9]+.[0-9]`""
(Get-Content nuget.test\tests\package_content.Tests.ps1) | ForEach-Object { $_ -replace $regex, "agentVersion = `"$agentVersion`"" } | Set-Content nuget.test\tests\package_content.Tests.ps1

# Build the NuGet package
. C:\NuGet.exe Pack $nuspecPath

# Push the package to the internal repository
. .\agent-build-scripts\windows\common\powershell\pushToInternalNuGetServer.ps1

# Rename the generated .nupkg file, removing the version
if ($env:Repository.Contains("x64"))
{
     Rename-Item "NewRelic.Azure.WebSites.x64.$($agentVersion).nupkg" NewRelic.Azure.WebSites.x64.nupkg
}
else
{
     Rename-Item "NewRelic.Azure.WebSites.$($agentVersion).nupkg" NewRelic.Azure.WebSites.nupkg
}