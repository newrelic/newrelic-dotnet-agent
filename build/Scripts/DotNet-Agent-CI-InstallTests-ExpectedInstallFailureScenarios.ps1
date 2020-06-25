############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

# Tests that agent MSI install fails on systems that do not have .NET Framework 4.5 or above.

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
    Write-Host "Install Failure Check on $env:SERVER"
    Write-Host "---------------------------"
    Write-Host ""

    iisreset /stop

    $install = Get-ChildItem build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_x64_*.msi -Name

    # Gets version of install
    $version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

    Write-Host "Attempting to install version $version of the .NET Agent"
    $product = (Get-WmiObject -List | Where-Object -FilterScript {$_.Name -eq "Win32_Product"})
    $result = $product.Install("build\BuildArtifacts\MsiInstaller-x64\$install")

    if ($result.ReturnValue)
    {
        Write-Host "There was an error attempting to install the .NET Agent - Code: $($result.ReturnValue)  We'll rerun msiexec to get details by examining the log file"
        Invoke-Command -ScriptBlock {msiexec.exe /i C:\$($args[0]) /lv* C:\installLog.txt} -ArgumentList $install

        Start-Sleep -Seconds 30

        if(-Not (Test-Path -Path "C:\installLog.txt"))
        {
            Write-Host "No install.log, exiting."
            ReportError 1
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

        if (Select-String -InputObject $installLog -SimpleMatch "New Relic requires that .NET Framework 4.5 be present prior to installation." -Quiet)
        {
            Write-Host "FrameworkVersion $frameworkVersion - Passed (found magic string)"
        }
        else
        {
            Write-Host "FrameworkVersion $frameworkVersion - Failed"
            $exitCode = $result.ReturnValue
            ReportError $exitCode
        }
    }

    iisreset /start
    Write-Host "FrameworkVersion $frameworkVersion Exit Code: $exitCode"
    ReportError $exitCode
}

Function ReportError([int] $errorCode) 
{
    if($realExitCode -ne 0 -and $errorCode -gt 0) 
    {
        $realExitCode = $errorCode
    }
}

$exitCode = 0
$realExitCode = 0

if($env:SERVER -like "dn-inst*-35*")
{
    CheckForInstallFailure "3.5"
    Write-Host "Real exit code $realExitCode "
}
elseif($env:SERVER -like "dn-inst*-40*")
{
    CheckForInstallFailure "4.0"
    Write-Host "Real exit code $realExitCode "
}
else
{
	Write-Host "FAILURE: Unable to identify which version of the .Net Framework to test."
	ReportError(1);
}

Write-Host "Final exit code $realExitCode"
exit $realExitCode
