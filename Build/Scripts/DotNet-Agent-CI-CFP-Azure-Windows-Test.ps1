$ErrorActionPreference = "Stop"
$serverName = "$env:SERVER_IP"
$appName = "DotNet-Functional-4_6_1-MVC5"
$logFilePath = "C:\ProgramData\New Relic\.NET Agent\Logs"
$appLogFile = "$logFilePath\newrelic_agent__LM_W3SVC_1_ROOT_$appname.log"
$connectLinePattern = 'Invoking "connect" with :';
$testDirectory = "C:\NewRelicTests"
$installerIdentifier = "*64*"
$user = "$env:PSRemote_Username"
$pass = ConvertTo-SecureString -String "$env:PSRemote_Password" -AsPlainText -Force
$cred = New-Object -TypeName "System.Management.Automation.PSCredential" -ArgumentList $user, $pass
$session = New-PSSession -ComputerName $serverName -Credential $cred
$installerName = $null;

$scriptExitCode = 1

function RunRemoteCommand([System.Management.Automation.ScriptBlock] $command , $argumentList) {
    Invoke-Command $session -ScriptBlock $command -ArgumentList $argumentList
}

if (Test-Path $installerIdentifier) {
    $installerName = Get-ChildItem $installerIdentifier -Name;
}

function RemoveDirectory([string] $path) {
    Write-Host "Removing $path Directory"

    if (RunRemoteCommand { Test-Path -Path $args[0] } $path) {
        RunRemoteCommand { Remove-Item $args[0] -Recurse -Force} $path
    }
}

function CreateDirectory([string] $path)
{
    Write-Host "Creating $path Directory"
    RunRemoteCommand { New-Item $args[0] -ItemType "directory"} $path
}

function CopyInstaller() {
    Write-Host "Copying Installer"
    Copy-Item -Path *.msi -ToSession $session -Destination $testDirectory
}

function StopIIS()
{
    RunRemoteCommand { iisreset /stop }
}

function StartIIS()
{
    RunRemoteCommand { iisreset /start }
}

function ResetIIS()
{
    RunRemoteCommand { iisrest }
}

function InstallAgent() 
{
    RunremoteCommand {

        $testDirectory = $args[0]
        $installerName = $args[1]
        $install = Get-ChildItem $testDirectory/$installerName

        Write-Host $install
        Write-Host "Attempting to install version $version of the .NET Agent"
        $product = (Get-WmiObject -List | Where-Object -FilterScript {$_.Name -eq "Win32_Product"})
        return $product.Install("$install", "INSTALLLEVEL=50")
    } ($testDirectory, $installerName)
}
 
function RemoveAgent() {
    Write-Host -NoNewline "Removing Files... "
    RunRemoteCommand {

        # $myWindowsID = [System.Security.Principal.WindowsIdentity]::GetCurrent()
        # $myWindowsPrincipal = new-object System.Security.Principal.WindowsPrincipal($myWindowsID)
        # $adminRole = [System.Security.Principal.WindowsBuiltInRole]::Administrator
        # if ($myWindowsPrincipal.IsInRole($adminRole)) {
        #     $Host.UI.RawUI.WindowTitle = $myInvocation.MyCommand.Definition + "(Elevated)"
        #     $Host.UI.RawUI.BackgroundColor = "DarkRed"
        #     clear-host
        # }
        # else {
        #     $newProcess = new-object System.Diagnostics.ProcessStartInfo "PowerShell"
        #     $newProcess.Arguments = $myInvocation.MyCommand.Definition
        #     $newProcess.Verb = "runas"
        #     [System.Diagnostics.Process]::Start($newProcess)
        #     exit
        # }


        $paths = @(
            "C:\Program Files\New Relic\.Net Agent\",
            "C:\Program Files (x86)\New Relic\.Net Agent\",
            "C:\ProgramData\New Relic\.Net Agent\",
            "HKLM:\SOFTWARE\New Relic\.NET Agent"
        )

        foreach ($path in $paths) {
            If (Test-Path "$path") {
                Remove-Item -Force -Recurse -Path "$path"
            }
        }

        Write-Host "Done"

        Write-Host -NoNewline "Removing Profiler... "
        Remove-ItemProperty -LiteralPath HKLM:SYSTEM\CurrentControlSet\Services\W3SVC -Name Environment
        Remove-ItemProperty -LiteralPath HKLM:SYSTEM\CurrentControlSet\Services\WAS -Name Environment
        Write-Host "Done"

        Write-Host -NoNewline "Removing Install/Uninstall Keys... "
        $keyBases = @(
            "HKLM:SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            "HKLM:SOFTWARE\Classes\Installer\Products"
        )

        foreach ($keyBase in $keyBases) {
            $keyPaths = @()
            Get-ChildItem -Path $keyBase | ForEach-Object -Process {
                $leaf = $_ | Split-Path -Leaf
                $data = Get-ItemProperty -LiteralPath "$keyBase\$leaf"
                if ($data.DisplayName -like "New Relic .NET Agent*" -Or $data.ProductName -like "New Relic .NET Agent*") {
                    $keyPaths += "$keyBase\$leaf"
                }
            }

            if ($keyPaths.Count -gt 0) {
                Write-Host "Found Agent install/uninstall keys and removing them."
                foreach ($keyPath in $keyPaths) {
                    Remove-Item -Force -Recurse -LiteralPath "$keyPath"
                }
            }
        }

        Write-Host "Done"

        Write-Host -NoNewline "Removing System Environment Variables... "
        [Environment]::SetEnvironmentVariable("COR_PROFILER_PATH", "", "Machine")
        [Environment]::SetEnvironmentVariable("COR_ENABLE_PROFILING", "", "Machine")
        [Environment]::SetEnvironmentVariable("COR_PROFILER", "", "Machine")
        [Environment]::SetEnvironmentVariable("NEWRELIC_INSTALL_PATH", "", "Machine")
        Write-Host "Done"

        Write-Host "Removal Completed"
    }   
}


function ExerciseApp() {
    Write-Host "Excercising App"
    Invoke-WebRequest -Uri "http://$serverName/$appName" -UseBasicParsing
}

function GetConnectionStringFromLog() {
    [string]$logFileContents = RunRemoteCommand {
        Get-Content -Path $args[0] | Select-String -Pattern $args[1] | Select-Object -ExpandProperty line
    } ($appLogFile, $connectLinePattern)

    $connectInfo = $logFileContents.Substring($logFileContents.IndexOf($connectLinePattern) + $connectLinePattern.Length + 1)
    return $connectInfo
}

function ValidateConnectData($connectData) {
    $data = ConvertFrom-Json $connectData

    Write-Host $data.utilization.vendors.location
    Write-Host $data.utilization.

    if(($data.utilization.vendors.azure.location -ne $null) -and ($data.utilization.vendors.azure.name -ne $null) -and ($data.utilization.vendors.azure.vmid -ne $null) -and ($data.utilization.vendors.azure.vmsize -ne $null)) {
        return $true
    }
    return $false
}

# Setup
RemoveDirectory $testDirectory
CreateDirectory $testDirectory
CopyInstaller

# Install Agent
StopIIS
InstallAgent
StartIIS

# Exercise App and Get Utilization Data
ExerciseApp

# Validate Utilization Data
$connectString = GetConnectionStringFromLog
$valid = ValidateConnectData($connectString)

# Teardown
StopIIS
RemoveAgent
RemoveDirectory $testDirectory
StartIIS

Remove-PSSession $session

if ($valid -eq $true) {
    $scriptExitCode = 0;
    Write-Host "Success"

}
else {
    Write-Host "Failure"
}

Write-Host "Script Exit Code: $scriptExitCode"
$LastExitCode = $scriptExitCode