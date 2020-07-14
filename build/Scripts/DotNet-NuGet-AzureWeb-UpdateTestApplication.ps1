$ErrorActionPreference = "Stop"

New-Item -ItemType File -Path "C:\Program Files (x86)\MSBuild\Microsoft\VisualStudio\v11.0\WebApplications\Microsoft.WebApplication.targets" -Force
Copy-Item "C:\Program Files (x86)\MSBuild\Microsoft\VisualStudio\v14.0\WebApplications\Microsoft.WebApplication.targets" "C:\Program Files (x86)\MSBuild\Microsoft\VisualStudio\v11.0\WebApplications\Microsoft.WebApplication.targets" -Force

$packageId = "NewRelic.Azure.WebSites"
$packagesConfigPath = "$env:WORKSPACE\NewRelicAzureWebCI\NewRelicAzureWebCI\packages.config"
$solutionDir = "NewRelicAzureWebCI/"
$solutionPath = "NewRelicAzureWebCI\NewRelicAzureWebCI.sln"

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

# Git
git checkout master
git fetch
git pull origin master

git stage $solutionDir
git status
git commit -m "Updates $packageId test application from package version $oldVersion to $newVersion"
git push origin master

