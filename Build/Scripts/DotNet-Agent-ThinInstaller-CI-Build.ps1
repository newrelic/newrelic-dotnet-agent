$ErrorActionPreference = "Stop"
. .\agent-build-scripts\windows\common\powershell\nuGet.ps1

$agentVersion = [IO.File]::ReadAllText(".\Installer\AgentVersion.txt").Trim()

$Source = ".\Installer"

###
# Add agentVerstion to the install.ps1 file
###
$Script = (Get-Content ('Installer/install.ps1')).replace('AGENT_VERSION_STRING', $agentVersion) 
Set-Content 'Installer/install.ps1' $Script

###
# Cleanup of files we don't currently need for installer.
###
if ( Test-path "$Source\.gitignore") { Remove-Item "$Source\.gitignore" }
if ( Test-Path "$Source\EnvironmentVariables.xml") { Remove-Item "$Source\EnvironmentVariables.xml" }
#if ( Test-Path "$Source\Nuget.config") { Remove-Item "$Source\Nuget.config" }
#if ( Test-Path "$Source\NuGet.targets") { Remove-Item "$Source\NuGet.targets" }

###
# Remove old versions of the installer
###
$installers = Get-ChildItem *.zip
foreach($installer in $installers) {
    Remove-item $installer
}

###
# Create installer zip file
###
$destination = ".\NewRelic.Agent.Installer.$agentVersion.zip"
Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::CreateFromDirectory($Source, $destination) 

###
# Annotate the build and the parent CI job
###
#$Commit = $env:GIT_COMMIT.Substring(0,10)
#
#$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
#Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$agentVersion - $env:GIT_BRANCH - $Commit" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
#if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
#    $null
#}
#
#if (!$env:sha1 -and $env:BUILD_CAUSE_UPSTREAMTRIGGER)
#{
#    Invoke-RestMethod -Uri "$($env:UPSTREAM_BUILD_URL)submitDescription?description=$agentVersion - $env:GIT_BRANCH - $Commit" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
#if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
#    $null
#}
#}
