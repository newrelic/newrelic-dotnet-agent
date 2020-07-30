# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Combined Install Test  

#Server report
Write-Host ""
Write-Host "---------------------------"
Write-Host "RUNNING ON SERVER: $env:SERVER"
Write-Host "---------------------------"
Write-Host ""

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

Function CheckForInstallFailure([string] $frameworkVersion)
{
    Write-Host ""
    Write-Host "---------------------------"
    Write-Host "Install Failure Check"
    Write-Host "---------------------------"
    Write-Host ""

    # Copy the install "agent" folder
    $install = Get-ChildItem Agent\_build\x64-Release\Installer\NewRelicAgent_x64_*.msi -Name
    Copy-Item -Force "Agent\_build\x64-Release\Installer\$install" "C:\"

    # Gets version of install
    $version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

    Write-Host "Attempting to install version $version of the .NET Agent"
    $product = (Get-WmiObject -List | Where-Object -FilterScript {$_.Name -eq "Win32_Product"})
    $result = $product.Install("C:\$install")

    if ($result.ReturnValue)
    {
        Write-Host "There was an error attempting to install the .NET Agent - Code: $($result.ReturnValue)  We'll rerun msiexec to get details by examining the log file"
        Invoke-Command -ScriptBlock {msiexec.exe /i C:\$($args[0]) /lv* C:\installLog.txt} -ArgumentList $install

        Start-Sleep -Seconds 30

        if(-Not (Test-Path -Path "C:\installLog.txt"))
        {
            Write-Host "No install.log, exiting."
            exit 1
        }

        $installLog = Get-Content -Path C:\installLog.txt
        Write-Host ""
        Write-Host "-------installLog.txt------"
        Write-Host ""
        $installLog
        Write-Host ""
        Write-Host "-------installLog.txt------"
        Write-Host ""

        if (Select-String -InputObject $installLog -SimpleMatch "New Relic requires that .NET Framework 2.0/3.0 be upgraded to 3.5 prior to installation." -Quiet)
        {
            Write-Host "FrameworkVersion $frameworkVersion - Passed (found magic string)"
        }
        else
        {
            Write-Host "FrameworkVersion $frameworkVersion - Failed"
            $exitCode = $result.ReturnValue
        }
    }

    Write-Host "FrameworkVersion $frameworkVersion Exit Code: $exitCode"
}

Function CheckForInstallSuccess([string] $frameworkVersion)
{
    Write-Host ""
    Write-Host "---------------------------"
    Write-Host " Install Success Check"
    Write-Host "---------------------------"
    Write-Host ""

    # Copy the install out to the server
    $install = Get-ChildItem Agent\_build\x64-Release\Installer\NewRelicAgent_x64_*.msi -Name
    Copy-Item -Force "Agent\_build\x64-Release\Installer\$install" "C:\"

    # Install the .NET Agent
    $version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

    Write-Host "Attempting to install version $version of the .NET Agent"
    $product = (Get-WmiObject -List | Where-Object -FilterScript {$_.Name -eq "Win32_Product"})
    $result = $product.Install("C:\$install", "INSTALLLEVEL=50")

    if ($result.ReturnValue) 
    {
        Write-Host "There was an error attempting to install the .NET Agent - Code: $($result.ReturnValue)"
        Write-Host "FrameworkVersion $frameworkVersion - Failed"
        $exitCode = $result.ReturnValue
    }
    else
    {
        if(Test-Path -Path "C:\ProgramData\New Relic\.NET Agent\newrelic.config")
        {
            Write-Host "FrameworkVersion $frameworkVersion - Passed"
        }
        else
        {
            Write-Host "newrelic.config file is missing, exiting."
            Write-Host "FrameworkVersion $frameworkVersion Exit Code: 1"
            exit 1
        }
    }

    Write-Host "FrameworkVersion $frameworkVersion Exit Code:$exitCode"
    $exitCode
}

$exitCode = 0
$realExitCode = 0

if($env:SERVER -like "dn-inst-300*")
{
    $realExitCode = CheckForInstallFailure "3.0"
    Write-Host "Real exit code $realExitCode "
}
elseif($env:SERVER -like "dn-inst-350*")
{
    $realExitCode = CheckForInstallSuccess "3.5"
    Write-Host "Real exit code $realExitCode "
}
elseif($env:SERVER -like "dn-inst-400v2*")
{
    $realExitCode = CheckForInstallSuccess "4.0"
    Write-Host "Real exit code $realExitCode "
}
elseif($env:SERVER -like "dn-inst-452*")
{
    $realExitCode = CheckForInstallSuccess "4.5.2"
    Write-Host "Real exit code $realExitCode "
}

Write-Host "Final exit code $realExitCode"
exit $realExitCode
