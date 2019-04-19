$ErrorActionPreference = "SilentlyContinue"

# Make request to the controller action to self-modify the config
Invoke-RestMethod -Uri "http://newrelicazurecloudci.cloudapp.net/Home/ModifyConfig" -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue | Out-Null
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
Start-Sleep -Seconds 75
Write-Host "Executing the functional regression tests"

# & "$env:WORKSPACE\FunctionalTests\packages\NUnit.Console.3.0.0\tools\nunit3-console.exe" "$env:WORKSPACE\FunctionalTests\bin\Release\FunctionalTests.dll" --test=FunctionalTests.NuGet_AzureCloudServices --workers=1 "--result=TestResult_NUnit2.xml;format=nunit2" "--result=TestResult_NUnit3.xml;format=nunit3"

& "C:\Program Files (x86)\NUnit.org\nunit-console\nunit3-console.exe" "$env:WORKSPACE\FunctionalTests\bin\Release\net45\FunctionalTests.dll" --test=FunctionalTests.NuGet_AzureCloudServices --workers=1 "--result=TestResult_NUnit2.xml;format=nunit2" "--result=TestResult_NUnit3.xml;format=nunit3"