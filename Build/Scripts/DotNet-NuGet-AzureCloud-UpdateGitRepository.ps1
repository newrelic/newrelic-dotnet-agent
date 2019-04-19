$packageName = "NewRelicWindowsAzure"

git checkout master
git rm content/NewRelicAgent_x64_*
git rm content/NewRelicServerMonitor_x64_*

Copy-Item -Path .\CopiedArtifacts\* -Destination $env:WORKSPACE -Recurse -Force
$agentInstall = Get-ChildItem content\NewRelicAgent_x64_*.msi -Name
$agentVersion = $agentInstall.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$agentVersion" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

git status
git stage content/ lib/ tools/ nuget-test/tests/package_content.Tests.ps1 NewRelicWindowsAzure.*
git commit -m "Updates $packageName to version $agentVersion"
git fetch
git pull origin master
git push origin master