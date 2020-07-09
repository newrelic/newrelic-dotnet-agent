$packageName = "NewRelic.Agent.Api"
$packageContentTestsPath = "nuget-test\tests\package_content.Tests.ps1"
$packageLibTestsPath = "nuget-test\tests\package_lib.Tests.ps1"
$agentVersion = [Reflection.AssemblyName]::GetAssemblyName("$env:WORKSPACE\lib\NewRelic.Api.Agent.dll").Version.ToString()
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$agentVersion" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

git checkout master
git status
git stage lib/ $packageContentTestsPath $packageLibTestsPath NewRelic.Agent.Api.*
git commit -m "Updates $packageName to version $agentVersion"
git fetch
git pull origin master
git push origin master