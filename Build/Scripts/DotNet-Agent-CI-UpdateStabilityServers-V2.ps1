$ErrorActionPreference = "Stop"

if( -Not (Test-Path env:PSPASS) -Or -Not (Test-Path env:PSUSER)) { exit 1 }
$secpasswd = ConvertTo-SecureString "$env:PSPASS" -AsPlainText -Force
$credentials = New-Object System.Management.Automation.PSCredential ("$env:PSUSER", $secpasswd)

Function uninstallAgentOnRemoteServer([string] $server) {
    $app = Get-WmiObject -Class Win32_Product -ComputerName $server -Credential $credentials | Where-Object { 
        $_.Name -match "New Relic .NET Agent" 
    }

    if ($app) {
        Write-Host "New Relic .NET Agent found, uninstalling..."
        $result = $app.Uninstall()
        if ($result.ReturnValue) {
            Write-Host "There was an error attempting to uninstall the .NET Agent."
            exit 1
        }
    }
    else {
        Write-Host "New Relic .NET Agent was not detected, moving on..."
    }
}

Function installAgentOnRemoteServer([string] $server, [string] $install)
{
    # Install the new version of the .NET Agent
    $version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

    Write-Host "Attempting to install version $version of the .NET Agent"
    $product = (Get-WmiObject -ComputerName $server -Credential $credentials -List | Where-Object -FilterScript {$_.Name -eq "Win32_Product"})
    $result = $product.Install("C:\$install")

    if ($result.ReturnValue) {
        Write-Host "The was an error attempting to install the .NET Agent."
        exit 1
    }

    # Verify the version of the installed agent
    $appInstalled = Get-WmiObject -Class Win32_Product -ComputerName $server -Credential $credentials | Where-Object { 
        $_.Name -match "New Relic .NET Agent" 
    }

    if ($appInstalled.Version -eq $version) {
        Write-Host "Version $version of the .NET Agent was successfully installed!!"
    }
    else {
        Write-Host "Unexpected Agent version detected!! Expecting: $version, Found: $appInstalled.Version"
        exit 1
    }
}

Function GetServerType([string] $server)
{
    $mvcServers = @(
        "172.31.3.73",
        "172.31.7.129",
        "172.31.4.129",
        "172.31.4.175",
        "172.31.4.21",
        "172.31.4.193"
    )

    $epiServers = @(
        "172.31.13.83",
        "172.31.5.112",
        "172.31.5.187",
        "172.31.6.58",
        "172.31.5.198",
        "172.31.15.110"
    )

    $segServers = @(
        "172.31.14.154",
        "172.31.14.34",
        "172.31.9.66",
        "172.31.11.143",
        "172.31.15.229",
        "172.31.0.4"
    )

    if($mvcServers.Contains($server)){ return "DotNet-Stab-MVC4" }
    if($epiServers.Contains($server)){ return "DotNet-Stab-EPiServer" }
    if($segServers.Contains($server)){ return "DotNet-Stab-Segments" }
}

$phase1 = {
    # Stop IIS
    Invoke-Command -ScriptBlock {iisreset /stop}

    # Purge the W3SVC log files
    Get-ChildItem "C:\inetpub\logs\LogFiles\W3SVC*\*.log" | Foreach { Write-Host "Deleting $_";Remove-Item -Path $_ -Force -ErrorAction SilentlyContinue }
}

$phase2 = {
    param($Rserver)
    # Remove the existing msi files
    Write-Host "Removing previous msi files from the test VM"
    Remove-Item -Path "C:\NewRelicAgent_*"

    # Remove the New Relic 'Logs' folder from 'ProgramData'
    if (Test-Path("C:\ProgramData\New Relic\.NET Agent\Logs"))
    {
        Remove-Item -Recurse -Force -Path "C:\ProgramData\New Relic\.NET Agent\Logs"
    }
    
    Write-Host "Clearing the 'Application' and 'System' event logs on $Rserver"
    Clear-EventLog -LogName Application
    Clear-EventLog -LogName System
}

$phase3 = {
    param($Rversion,$target,$Rtype)
    # Update the 'newrelic.config' or 'newrelic.xml' file
    if ($Rversion -ne "2.8.1.0")
    {
        $configPath = "C:\ProgramData\New Relic\.NET Agent\newrelic.config"
    }
    else
    {
        $configPath = "C:\ProgramData\New Relic\.NET Agent\newrelic.xml"
    }

    Write-Host "Updating the newrelic.config"
    [Xml]$config = Get-Content $configPath
    $ns = New-Object Xml.XmlNamespaceManager $config.NameTable
    $ns.AddNamespace("x", "urn:newrelic-config")
    
    # Set the 'licenseKey' and 'host' attributes on the 'service' element
    $config.configuration.service.SetAttribute("licenseKey", "b25fd3ca20fe323a9a7c4a092e48d62dc64cc61d")
    $config.configuration.service.SetAttribute("host", "staging-collector.newrelic.com")
    
    # Set the default application name
    $config.configuration.application.name = "$Rtype"
    $config.Save($configPath)
    
    # Restart IIS
    Invoke-Command -ScriptBlock {iisreset}
    
    
    Write-Host "Updating the Performance Monitor config"
    $configPathService = "C:\NewRelic.Analytics.PerformanceMonitor.Service\bin\Release\NewRelic.Analytics.PerformanceMonitor.Service.exe.config"
    [Xml]$configService = Get-Content $configPathService
    
    if ($target -in "B1")
    {
        ($configService.configuration.newrelicPerformanceMonitorSection.eventMetaData.add | where {$_.name -eq "AgentVersion"}).value = "$target - $env:BValue"
    }
    else
    {
        ($configService.configuration.newrelicPerformanceMonitorSection.eventMetaData.add | where {$_.name -eq "AgentVersion"}).value = $Rversion
    }
    $configService.Save($configPathService)
    
    # Restart the service
    Write-Host "Restarting NewRelicInsightsPerformanceMonitor"
    $service = Get-Service | Where {$_.Name -eq "NewRelicInsightsPerformanceMonitor"}
    Restart-Service $service -ErrorAction SilentlyContinue
    $service.Status
}

$serversEst = @(
    "172.31.4.21",
    "172.31.5.198",
    "172.31.15.229"
)

$serversMstr = @(
    "172.31.4.175",
    "172.31.6.58",
    "172.31.11.143"
)

$serversStage = @(
    "172.31.4.193",
    "172.31.15.110",
    "172.31.0.4",
    "172.16.128.249"
)

$serversB1 = @(
    "172.31.7.129",
    "172.31.5.112",
    "172.31.14.34"
)

$serversDev = @(
    "172.31.4.129",
    "172.31.5.187",
    "172.31.9.66"
)

$serversAll = $serversEst + $serversMstr + $serversStage + $serversB1 + $serversDev

if($env:IsRelease -eq "true")
{
    $env:Target = "Stage"
}

switch ($env:Target) 
{
    "Stage" { $servers = $serversStage }
    "Master" { $servers = $serversMstr }
    "Established" { $servers = $serversEst }
    "B1" { $servers = $serversB1 }
    "Dev" { $servers = $serversDev }
    default { "The target could not be determined." }
}

$install = Get-ChildItem $env:WORKSPACE\Agent\_build\x64-Release\Installer\NewRelicAgent_x64_*.msi -Name
$version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

foreach ($serverAll in $serversAll)
{
    Enter-PSSession -ComputerName $serverAll -Credential $credentials
    Get-ChildItem "C:\inetpub\logs\LogFiles\W3SVC*\*.log" | Foreach { Write-Host "Deleting '$_' on '$serverAll'";Remove-Item -Path $_ -Force -ErrorAction SilentlyContinue }
    Exit-PSSession
}

foreach ($server in $servers)
{
    # Setup server session
    $serverType = GetServerType $server

    Write-Host "Configuring $server"
    $session = New-PSSession -ComputerName $server -Credential $credentials

    # PHASE 1
    Invoke-Command -Session $session -ScriptBlock $phase1

    # Uninstall the current version of the .NET Agent if present
    uninstallAgentOnRemoteServer $server $install

    # PHASE 2
    Invoke-Command -Session $session -ScriptBlock $phase2 -ArgumentList $server

    Write-Host "Copying new version of the .NET Agent out to the test VM"
    Copy-Item -Path $env:WORKSPACE\Agent\_build\x64-Release\Installer\$install -Destination "C:\" -ToSession $session
    
    # Install the new version of the .NET Agent
    installAgentOnRemoteServer $server $install

    # PHASE 3
    Invoke-Command -Session $session -ScriptBlock $phase3 -ArgumentList $version,$env:Target,$serverType

    Remove-PSSession -Session $session
}