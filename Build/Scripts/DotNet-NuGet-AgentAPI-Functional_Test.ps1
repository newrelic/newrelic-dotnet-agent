$ErrorActionPreference = "Stop"

# Annotate the build with the NuGet package version
[Xml]$xml = Get-Content .\DotNet-Functional-NuGet-AgentAPI\DotNet-Functional-NuGet-AgentAPI\packages.config
$version = $xml.packages.SelectSingleNode("//package[@id='NewRelic.Agent.Api']").version
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$version" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

# Copy out the test application
$webConfigPath = "$env:WORKSPACE\DotNet-Functional-NuGet-AgentAPI\DotNet-Functional-NuGet-AgentAPI\Web.config"
[Xml] $doc = Get-Content -Path $webConfigPath
($doc.configuration.appSettings.add | where { $_.key -eq 'NewRelic.AppName' }).value = 'DotNet-Functional-NuGet-AgentAPI_win-agent-func-api'
$doc.Save($webConfigPath)


$server = "win-agent-func-api.pdx.vm.datanerd.us"
Write-Host "Copy-Item DotNet-Functional-NuGet-AgentAPI `"\\$server\c$\TestApplications`" -Recurse -Force"
Copy-Item DotNet-Functional-NuGet-AgentAPI "\\$server\c$\TestApplications" -Recurse -Force

$install = Get-ChildItem $env:WORKSPACE\Agent\_build\x64-Release\Installer\NewRelicAgent_x64_*.msi -Name
$version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

$appConfigPath = "$env:WORKSPACE\FunctionalTests\bin\Release\net45\FunctionalTests.dll.config"
[Xml]$appConfig = Get-Content $appConfigPath
$appConfig.SelectSingleNode("/configuration/appSettings/add[@key='Environment']").Value = "Remote"
$appConfig.SelectSingleNode("/configuration/appSettings/add[@key='RemoteServers']").Value = $server
$appConfig.SelectSingleNode("/configuration/appSettings/add[@key='AgentVersion']").Value = $version
$appConfig.SelectSingleNode("/configuration/appSettings/add[@key='TestApplicationsPath']").Value = "C:\TestApplications"
$appConfig.Save($appConfigPath)

# Uninstall the current version of the .NET Agent if present
. .\agent-build-scripts\windows\agent\powershell\agentInstallActions.ps1
uninstallAgentOnRemoteServer $server $install

# Remove the existing msi files
Write-Host "Removing previous msi files from the test VM"

# Purge any previous installers if present, keeping the most recent 3
$installers = Get-Item -Path "\\$server\c$\NewRelicAgent_x64_*"
if ($installers.Count -gt 3)
{
    Remove-Item ($installers | Sort-Object Name -Descending | Select-Object -Last ($($installers.Count) - 3))
}

# Clear the application event log on the remote server
Clear-EventLog -LogName Application -ComputerName $server

# Copy the new version of the installer out to the test VM
Write-Host "Copying new version of the .NET Agent out to the test VM"
Copy-Item $env:WORKSPACE\Agent\_build\x64-Release\Installer\$install \\$server\c$

Remove-Item -Force -Recurse "\\$server\c`$\ProgramData\New Relic\.NET Agent" -ErrorAction Continue

# Install the new version of the .NET Agent
installAgentOnRemoteServer $server $install

# Update the 'newrelic.config' file
$configPath = "\\$server\c`$\ProgramData\New Relic\.NET Agent\newrelic.config"
[Xml]$config = Get-Content $configPath
$ns = New-Object Xml.XmlNamespaceManager $config.NameTable
$ns.AddNamespace("x", "urn:newrelic-config")

$config.configuration.service.SetAttribute("licenseKey", "b25fd3ca20fe323a9a7c4a092e48d62dc64cc61d")
$config.configuration.service.SetAttribute("host", "staging-collector.newrelic.com")
$config.configuration.log.SetAttribute("level", "debug")
$config.configuration.log.SetAttribute("auditLog", "true")
$config.Save($configPath)

# Execute the functional tests
Write-Host "Executing the functional regression tests"

# & "$env:WORKSPACE\FunctionalTests\packages\NUnit.Console.3.0.0\tools\nunit3-console.exe" "$env:WORKSPACE\FunctionalTests\bin\Release\FunctionalTests.dll" --test=FunctionalTests.NuGet_AgentAPI --workers=1 "--result=TestResult_NUnit2.xml;format=nunit2" "--result=TestResult_NUnit3.xml;format=nunit3"

& "C:\Program Files (x86)\NUnit.org\nunit-console\nunit3-console.exe" "$env:WORKSPACE\FunctionalTests\bin\Release\net45\FunctionalTests.dll" --test=FunctionalTests.NuGet_AgentAPI --workers=1 "--result=TestResult_NUnit2.xml;format=nunit2" "--result=TestResult_NUnit3.xml;format=nunit3"