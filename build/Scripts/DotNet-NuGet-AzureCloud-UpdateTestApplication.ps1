# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

$ErrorActionPreference = "Stop"

New-Item -ItemType File -Path "C:\Program Files (x86)\MSBuild\Microsoft\VisualStudio\v11.0\WebApplications\Microsoft.WebApplication.targets" -Force
Copy-Item "C:\Program Files (x86)\MSBuild\Microsoft\VisualStudio\v14.0\WebApplications\Microsoft.WebApplication.targets" "C:\Program Files (x86)\MSBuild\Microsoft\VisualStudio\v11.0\WebApplications\Microsoft.WebApplication.targets" -Force

$packageId = "NewRelicWindowsAzure"
$packagesConfigPath = "$env:WORKSPACE\NewRelicAzureCloudCI\NewRelicAzureCloudCI\packages.config"
$solutionDir = "NewRelicAzureCloudCI/"
$solutionPath = "NewRelicAzureCloudCI\NewRelicAzureCloudCI.sln"

[Xml]$packages = Get-Content $packagesConfigPath
$oldVersion = $packages.packages.SelectSingleNode("package[@id='$packageId']").version

& C:\NuGet.exe restore $solutionPath -NoCache -Source "https://www.nuget.org/api/v2"
& C:\NuGet.exe update $solutionPath -NonInteractive -Id $packageId -Source "https://www.nuget.org/api/v2"

# Annotate the build
[Xml]$packages = Get-Content $packagesConfigPath
$newVersion = $packages.packages.SelectSingleNode("package[@id='$packageId']").version
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$newVersion" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

# Update the paths
$agent = (Get-ChildItem -Path .\NewRelicAzureCloudCI\NewRelicAzureCloudCI\NewRelicAgent_x64_* | select -last 1).Name
$agent
$wsm = (Get-ChildItem -Path .\NewRelicAzureCloudCI\NewRelicAzureCloudCI\NewRelicServerMonitor_x64_*).Name
$csprojPath = "$env:WORKSPACE\NewRelicAzureCloudCI\NewRelicAzureCloudCI\NewRelicAzureCloudCI.csproj"
[Xml]$csproj = Get-Content $csprojPath
($csproj.Project.ItemGroup.Content | Where {$_.Include -like "NewRelicAgent*"}).Include = $agent
($csproj.Project.ItemGroup.Content | Where {$_.Include -like "NewRelicServerMonitor*"}).Include = $wsm
$csproj.Save($csprojPath)

# Build the solution
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe .\NewRelicAzureCloudCI\NewRelicAzureCloudCI.sln "/p:Configuration=Release,VisualStudioVersion=12.0"

# Git
git checkout master
git fetch
git pull origin master

git add --all $solutionDir
#git rm "NewRelicAzureCloudCI/NewRelicAzureCloudCI/NewRelicAgent_x64_$oldVersion.msi"
git status
git commit -m "Updates $packageId test application from package version $oldVersion to $newVersion"
git push origin master