if ($env:Repository -eq "nuget-azure-web-sites.git")
{
    $packageName = "NewRelic.Azure.WebSites"
}
else
{
    $packageName = "NewRelic.Azure.WebSites.x64"
}

$agentVersion = [Reflection.AssemblyName]::GetAssemblyName("$env:WORKSPACE\lib\NewRelic.Api.Agent.dll").Version.ToString()

$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$agentVersion" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

git checkout master
git status
git add -u content/
git stage content/ lib/ nuget.test/tests/package_content.Tests.ps1 NewRelic.Azure.WebSites.*
git commit -m "Updates $packageName to version $agentVersion"
git fetch
git pull origin master
git push origin master