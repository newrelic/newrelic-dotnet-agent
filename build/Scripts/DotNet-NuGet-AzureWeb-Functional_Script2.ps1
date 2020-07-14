# Start the website
Select-AzureSubscription -SubscriptionName Pay-As-You-Go
$websiteName = "NuGetAzureWebSitesTest"
Start-AzureWebsite -Name $websiteName
Get-AzureWebsite -Name $websiteName | Select-Object State

# Annotate the build
[Xml]$xml = Get-Content "NewRelicAzureWebCI\NewRelicAzureWebCI\packages.config"
$version = $xml.packages.SelectSingleNode("//package[@id='NewRelic.Azure.WebSites']").version
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$version" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

$install = Get-ChildItem $env:WORKSPACE\Agent\_build\x86-Release\Installer\NewRelicAgent_x86_*.msi -Name
$version = $install.TrimStart('NewRelicAgent_x').TrimStart('{64,86}').TrimStart('_').TrimEnd('.msi')

$appConfigPath = "$env:WORKSPACE\FunctionalTests\bin\Release\net45\FunctionalTests.dll.config"
[Xml]$appConfig = Get-Content $appConfigPath
$appConfig.SelectSingleNode("/configuration/appSettings/add[@key='AgentVersion']").Value = $version
$appConfig.Save($appConfigPath)

# Execute the functional tests
Write-Host "Executing the functional regression tests"

# & "$env:WORKSPACE\FunctionalTests\packages\NUnit.Console.3.0.0\tools\nunit3-console.exe" "$env:WORKSPACE\FunctionalTests\bin\Release\FunctionalTests.dll" --test=FunctionalTests.NuGet_AzureWebSites --workers=1 "--result=TestResult_NUnit2.xml;format=nunit2" "--result=TestResult_NUnit3.xml;format=nunit3"

& "C:\Program Files (x86)\NUnit.org\nunit-console\nunit3-console.exe" "$env:WORKSPACE\FunctionalTests\bin\Release\net45\FunctionalTests.dll" --test=FunctionalTests.NuGet_AzureWebSites --workers=1 "--result=TestResult_NUnit2.xml;format=nunit2" "--result=TestResult_NUnit3.xml;format=nunit3"
