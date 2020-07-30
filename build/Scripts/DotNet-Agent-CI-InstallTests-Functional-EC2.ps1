# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Combined Install Test  

# Elevate
Write-Host "Elevating to Administrator"
$myWindowsID=[System.Security.Principal.WindowsIdentity]::GetCurrent()
$myWindowsPrincipal=new-object System.Security.Principal.WindowsPrincipal($myWindowsID)
$adminRole=[System.Security.Principal.WindowsBuiltInRole]::Administrator
if ($myWindowsPrincipal.IsInRole($adminRole))
{
    $Host.UI.RawUI.WindowTitle = $myInvocation.MyCommand.Definition + "(Elevated)"
    $Host.UI.RawUI.BackgroundColor = "DarkRed"
    clear-host
}
else
{
    $newProcess = new-object System.Diagnostics.ProcessStartInfo "PowerShell";
    $newProcess.Arguments = $myInvocation.MyCommand.Definition;
    $newProcess.Verb = "runas";
    [System.Diagnostics.Process]::Start($newProcess);
    exit
}
 
# Run your code that needs to be elevated here
Write-Host "Elevated"
$ErrorActionPreference = "Stop"

Function RunFunctionalTests
{
    Write-Host ""
    Write-Host "---------------------------"
    Write-Host "Functional Tests"
    Write-Host "---------------------------"
    Write-Host ""

    iisreset /start
    Write-Host "SERVER is $env:SERVER"

    $install = Get-ChildItem $env:WORKSPACE\Agent\_build\x64-Release\Installer\NewRelicAgent_x64_*.msi -Name
    $version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

    $appConfigPath = "$env:WORKSPACE\FunctionalTests\bin\Release\FunctionalTests.dll.config"
    [Xml]$appConfig = Get-Content $appConfigPath
    $appConfig.SelectSingleNode("/configuration/appSettings/add[@key='Environment']").Value = "Local"
    $appConfig.SelectSingleNode("/configuration/appSettings/add[@key='RemoteServers']").Value = "localhost"
    $appConfig.SelectSingleNode("/configuration/appSettings/add[@key='AgentVersion']").Value = $version
    $appConfig.SelectSingleNode("/configuration/appSettings/add[@key='TestApplicationsPath']").Value = "C:\TestApplications"
    $appConfig.Save($appConfigPath)

    # Execute the functional tests
    Write-Host "Executing the install-specific functional tests"

    Write-Host "nunit3-console.exe `"$env:WORKSPACE\FunctionalTests\bin\Release\FunctionalTests.dll`" --where `"cat==Install`" --workers=1 `"--result=TestResult_NUnit2.xml;format=nunit2`" `"--result=TestResult_NUnit3.xml;format=nunit3`""
    & "C:\Program Files (x86)\NUnit.org\nunit-console\nunit3-console.exe" "$env:WORKSPACE\FunctionalTests\bin\Release\FunctionalTests.dll" --where "cat==Install" --workers=1 "--result=TestResult_NUnit2.xml;format=nunit2" "--result=TestResult_NUnit3.xml;format=nunit3"

    $exitCode = $LastExitCode
    
    Write-Host "Testing exited with code $LastExitCode"
    if($exitCode -ge "1")
    {
        Write-Host "Tests failed, exiting."
        exit $exitCode
    }

    Remove-Item -Path "c:\$install" -Force
}

Function CreateAppInRPM
{
    Write-Host ""
    Write-Host "---------------------------"
    Write-Host "Ensure App Exists in RPM"
    Write-Host "---------------------------"
    Write-Host ""

    $serviceConfigPath = "C:\code\dotnet_web_sandbox\DotNet-Functional-WindowsService\DotNet-Functional-WindowsService\bin\Release\DotNet-Functional-WindowsService.exe.config"
    if(Test-Path -Path "$serviceConfigPath")
    {
        Write-Host "Setting up test services with correct name"
        $myhostname = $(hostname).Trim().ToUpper()
        [Xml]$serviceConfig = Get-Content $serviceConfigPath
        $serviceConfig.SelectSingleNode("/configuration/appSettings/add[@key='NewRelic.AppName']").Value = "DotNet-Functional-WindowsService_$myhostname"
        $serviceConfig.Save($serviceConfigPath)
    }
    else
    {
        Write-Host "TestApp config file is missing, exiting."
        exit 1
    }

    $agentConfigPath = "C:\ProgramData\New Relic\.NET Agent\newrelic.config"
    if(Test-Path -Path "$agentConfigPath")
    {
        Write-Host "Setting up the agent to report to staging"
        [Xml]$agentConfig = Get-Content $agentConfigPath
        $hostAtt = $agentConfig.CreateAttribute("host")
        $hostAtt.Value = "staging-collector.newrelic.com"
        $agentConfig.configuration.service.Attributes.Append($hostAtt)
        $keyAtt = $agentConfig.CreateAttribute("licenseKey")
        $keyAtt.Value = "b25fd3ca20fe323a9a7c4a092e48d62dc64cc61d"
        $agentConfig.configuration.service.Attributes.Append($keyAtt)
        $agentConfig.Save($agentConfigPath)
    }
    else
    {
        Write-Host "newrelic.config file is missing, exiting."
        exit 1
    }


    Write-Host "Cycling test service to create appid"
    Start-Service -Name DotNet-Functional-WindowsService
    Start-Sleep -Seconds 65
    Stop-Service -Name DotNet-Functional-WindowsService


    if($LastExitCode -ge "1")
    {
        Write-Host "An error occured, exiting with $LastExitCode"
        exit $LastExitCode
    }

    Write-Host "Prep completed with $LastExitCode"
}

Stop-Service -Name DotNet-Functional-WindowsService
$exitCode = 0

CreateAppInRPM
RunFunctionalTests

exit $exitCode
