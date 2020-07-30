# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

$ErrorActionPreference = "Stop"
. .\agent-build-scripts\windows\common\powershell\nuGet.ps1

# Modify 'newrelic.config'
$configPath = "$env:WORKSPACE\NewRelicAzureWebCI\NewRelicAzureWebCI\newrelic\newrelic.config"
[Xml]$config = Get-Content -Path $configPath
$ns = New-Object Xml.XmlNamespaceManager $config.NameTable
$ns.AddNamespace("x", "urn:newrelic-config")

$hostAttribute = $config.CreateAttribute("host")
$config.configuration.service.Attributes.Append($hostAttribute)
$config.configuration.service.SetAttribute("host", "staging-collector.newrelic.com")
$config.configuration.transactionTracer.SetAttribute("stackTraceThreshold", "1")
$config.configuration.transactionTracer.SetAttribute("explainThreshold", "1")
$config.Save($configPath)

# Import the publish settings file, select the subscription
Import-AzurePublishSettingsFile -PublishSettingsFile C:\Azure.publishsettings
Select-AzureSubscription -SubscriptionName Pay-As-You-Go

Start-Sleep -s 10

# Stop the website
$websiteName = "NuGetAzureWebSitesTest"
Stop-AzureWebsite -Name $websiteName
Get-AzureWebsite -Name $websiteName | Select-Object State

RestoreNuGetPackages NewRelicAzureWebCI\NewRelicAzureWebCI.sln "https://www.nuget.org/api/v2"