# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

$ErrorActionPreference = "SilentlyContinue"

# Annotate the build with the NuGet package version
[Xml]$xml = Get-Content .\NewRelicAzureCloudCI\NewRelicAzureCloudCI\packages.config
$version = $xml.packages.SelectSingleNode("//package[@id='NewRelicWindowsAzure']").version
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$version" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

# Deploy the cloud service to Azure
$appPublishDir = '.\NewRelicAzureCloudCI\NewRelicAzureCloudCI.Azure\bin\Release\app.publish'

Import-Module 'C:\Program Files (x86)\Microsoft SDKs\Azure\PowerShell\ServiceManagement\Azure\Azure.psd1'
Import-AzurePublishSettingsFile -PublishSettingsFile C:\Azure.publishsettings
Set-AzureSubscription -CurrentStorageAccount crossprocessstorage -SubscriptionName Pay-As-You-Go
Select-AzureSubscription -SubscriptionName Pay-As-You-Go

Set-AzureDeployment -Upgrade -Slot Production -Package "$appPublishDir\NewRelicAzureCloudCI.Azure.cspkg" -Configuration "$appPublishDir\ServiceConfiguration.Cloud.cscfg" -label Jenkins -ServiceName NewRelicAzureCloudCI -Force | Out-Null

$deployment = Get-AzureDeployment -Slot Production -ServiceName NewRelicAzureCloudCI
$deploymentUrl = $deployment.Url

Write-Output "$(Get-Date –f $timeStampFormat) - Created Cloud Service with URL $deploymentUrl."
Write-Output "$(Get-Date –f $timeStampFormat) - Azure Cloud Service deploy script finished."