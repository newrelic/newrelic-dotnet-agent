# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

$ErrorActionPreference = 'Stop'

$architectures = @("x64", "x86")
foreach ($architecture in $architectures)
{
    $destinationFolder = "$env:WORKSPACE\CopiedArtifacts"

    $success = $false
    $attempts = 0
    while ($success -eq $false)
    {
        $attempts = $attempts + 1
        try{
            $response = Invoke-WebRequest -Uri "https://download.newrelic.com/windows_server_monitor/release/$architecture" -Headers @{"user-agent"="Mozilla/5.0 (Windows NT 6.1; WOW64; rv:7.0.1) Gecko/20100101 Firefox/7.0.1"}
            $success = $true
        }
        catch
        {
            Write-Host "Caught an error attempting to download the $architecture instance of WSM"
            if ($attempts -ge 10)
            {
                Write-Host "Exceeded number of allowed attempts."
                exit 1
            }
        }
    }
    
    $filename = "NewRelicServerMonitor_$architecture.msi"
    $filepath = [System.IO.Path]::Combine($destinationFolder, $filename)

    try
    {
        $filestream = [System.IO.File]::Create($filepath)
        $response.RawContentStream.WriteTo($filestream)
        $filestream.Close()
    }
    finally
    {
        if ($filestream)
        {
            $filestream.Dispose();
        }
    }
}

$installer = New-Object -ComObject WindowsInstaller.Installer

$database = $installer.GetType().InvokeMember("OpenDatabase", [System.Reflection.BindingFlags]::InvokeMethod, $null, $installer, ($filepath, 0))
$view = $database.GetType().InvokeMember("OpenView", [System.Reflection.BindingFlags]::InvokeMethod, $null, $database, "SELECT `Value` FROM `Property` WHERE `Property` = 'ProductVersion'")
$view.GetType().InvokeMember("Execute", [System.Reflection.BindingFlags]::InvokeMethod, $null, $view, $null)

$record = $view.GetType().InvokeMember("Fetch", [System.Reflection.BindingFlags]::InvokeMethod, $null, $view, $null)
$productVersion = $record.GetType().InvokeMember("StringData", [System.Reflection.BindingFlags]::GetProperty, $null, $record, 1)
$view.GetType().InvokeMember("Close", [System.Reflection.BindingFlags]::InvokeMethod, $null, $view, $null)

Remove-Variable -Name view, record, database, installer

New-Item -Path Version.txt -Value $productVersion -ItemType file -Force