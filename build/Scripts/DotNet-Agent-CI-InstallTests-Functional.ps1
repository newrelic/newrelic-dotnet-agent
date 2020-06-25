############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

# Function Install Tests (4.5+ only)

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

    iisreset /stop
    Write-Host "SERVER is $env:SERVER"

    $install = Get-ChildItem build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_x64_*.msi -Name
    $version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

    $appConfigPath = "$env:WORKSPACE\Tests\Agent\MsiInstallerTests\bin\Release\MsiInstallerTests.dll.config"
    [Xml]$appConfig = Get-Content $appConfigPath
    $appConfig.SelectSingleNode("/configuration/appSettings/add[@key='Environment']").Value = "Local"
    $appConfig.SelectSingleNode("/configuration/appSettings/add[@key='RemoteServers']").Value = "localhost"
    $appConfig.SelectSingleNode("/configuration/appSettings/add[@key='AgentVersion']").Value = $version
    $appConfig.Save($appConfigPath)

    iisreset /start
    
    # Execute the functional tests
    Write-Host "Executing the install-specific functional tests"

    Write-Host "nunit3-console.exe `"$env:WORKSPACE\Tests\Agent\MsiInstallerTests\bin\Release\MsiInstallerTests.dll`" --where `"cat==Install`" --workers=1 `"--result=TestResult_NUnit2.xml;format=nunit2`" `"--result=TestResult_NUnit3.xml;format=nunit3`""
    & "$env:WORKSPACE\Build\Tools\NUnit-Console\nunit3-console.exe" "$env:WORKSPACE\Tests\Agent\MsiInstallerTests\bin\Release\MsiInstallerTests.dll" --where "cat==Install" --workers=1 "--result=TestResult_NUnit3.xml;format=nunit3"

    $exitCode = $LastExitCode
    
    Write-Host "Testing exited with code $LastExitCode"
    if($exitCode -ge "1")
    {
        Write-Host "Tests failed, exiting."
        exit $exitCode
    }
}

Function SetupWebConfig
{
    Write-Host ""
    Write-Host "---------------------------"
    Write-Host "Setup web.config with servername"
    Write-Host "---------------------------"
    Write-Host ""

    iisreset /stop
    $appConfigPath = "C:\inetpub\wwwroot\DotNet-Functional-InstallTestApp\Web.config"
    if(Test-Path -Path "$appConfigPath")
    {
        Write-Host "Setting up test services with correct name"
        $myhostname = $(hostname).Trim().ToUpper()
        [Xml]$appConfig = Get-Content $appConfigPath
        $appConfig.SelectSingleNode("/configuration/appSettings/add[@key='NewRelic.AppName']").Value = "DotNet-Functional-InstallTestApp_$myhostname"
        $appConfig.Save($appConfigPath)
    }
    else
    {
        Write-Host "TestApp config file is missing, exiting."
        exit 1
    }

    Write-Host "Prep completed with $LastExitCode"
}

$exitCode = 0

# Clean out the previous runs logs
Remove-Item -Force -Path "c:\installLog.txt" -ErrorAction SilentlyContinue
Remove-Item -Force -Path "c:\repairLog.txt" -ErrorAction SilentlyContinue
Remove-Item -Force -Path "c:\moved_installLog.txt" -ErrorAction SilentlyContinue
Remove-Item -Force -Path "c:\moved_uninstallLog.txt" -ErrorAction SilentlyContinue
Remove-Item -Force -Path "c:\moved_repairLog.txt" -ErrorAction SilentlyContinue

SetupWebConfig
RunFunctionalTests

exit $exitCode
