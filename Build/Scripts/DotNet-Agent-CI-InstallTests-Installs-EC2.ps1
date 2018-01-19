# Tests that agent installs at all and only on supported systems.

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

        # LEGACY: Swap the IFs below to enable 3.5 testing!
        # if (Select-String -InputObject $installLog -SimpleMatch "New Relic requires that .NET Framework 2.0/3.0 be upgraded to 3.5 prior to installation." -Quiet)
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

Function CheckForInstallSuccess([string] $frameworkVersion)
{
    Write-Host ""
    Write-Host "---------------------------"
    Write-Host " Install Success Check on $env:SERVER"
    Write-Host "---------------------------"
    Write-Host ""

    iisreset /stop

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
        ReportError $exitCode
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
            ReportError 1
            exit 1
        }
    }

    iisreset /start
    Write-Host "FrameworkVersion $frameworkVersion Exit Code:$exitCode"
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

# LEGACY: Uncomment me to enable legacy agent testing!
# if($env:SERVER -like "dn-inst-300*")
# {
#     CheckForInstallFailure "3.0"
#     Write-Host "Real exit code $realExitCode "
# }
if($env:SERVER -like "dn-inst-350*")
{
    # LEGACY: Swap the two checks out to enable legacy agent testing!
    # CheckForInstallSuccess "3.5"
    CheckForInstallFailure "3.5"
    Write-Host "Real exit code $realExitCode "
}
elseif($env:SERVER -like "dn-inst-400v2*")
{
    # LEGACY: Swap the two checks out to enable legacy agent testing!
    # CheckForInstallSuccess "4.0"
    CheckForInstallFailure "4.0"
    Write-Host "Real exit code $realExitCode "
}
elseif($env:SERVER -like "dn-inst-452*")
{
    CheckForInstallSuccess "4.5.2"
    Write-Host "Real exit code $realExitCode "
}
# Likely Future test servers
# LEGACY: Comment out below hereto enable legacy agent testing!
# elseif($env:SERVER -like "dn-inst-462*")
# {
#     $realExitCode = CheckForInstallSuccess "4.6.2"
#     Write-Host "Real exit code $realExitCode "
# }
# elseif($env:SERVER -like "dn-inst-470*")
# {
#     $realExitCode = CheckForInstallSuccess "4.7.0"
#     Write-Host "Real exit code $realExitCode "
# }

# Don't comment this out!  Stop, go no further
Write-Host "Final exit code $realExitCode"
exit $realExitCode
