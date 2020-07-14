param($installPath, $toolsPath, $package, $project)

$extensionFile = $project.ProjectItems.Item("newrelic").ProjectItems.Item("extensions").ProjectItems.Item("extension.xsd")
if($extensionFile -ne $null){
	$extensionFile.Properties.Item("BuildAction").Value = 2
}

Write-Host "***Package install is complete***" -ForegroundColor DarkGreen

Write-Host "Please make sure to go add the following configurations to your Azure website prior to deploying to Azure." -ForegroundColor DarkGreen
Write-Host "1. Go to manage.windowsazure.com, log in, navigate to your Web App and choose 'configure'"  -ForegroundColor DarkGreen

Write-Host "2. Navigate to the 'Developer Analytics' section and enable 'Performance Monitoring' by either:" -ForegroundColor DarkGreen
Write-Host "	a. choosing 'ADD-ON' and selecting an existing New Relic add-on" -ForegroundColor DarkGreen
Write-Host "	b. choosing 'CUSTOM', choose New Relic as the 'PROVIDER' and add a license key" -ForegroundColor DarkGreen
Write-Host " "
Write-Host "OR (Instead of the above) : Add the following as 'app settings' "

#Write-Host $appSettings | Format-Table @{Expression={$_.Key};Label="Key";width=25},Value
Write-Host "Key					Value"
Write-Host "---------------------------------------"
Write-Host "COR_ENABLE_PROFILING	1"
Write-Host "COR_PROFILER			{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
Write-Host "COR_PROFILER_PATH		D:\Home\site\wwwroot\newrelic\NewRelic.Profiler.dll"
Write-Host "NEWRELIC_HOME			D:\Home\site\wwwroot\newrelic"
Write-Host "NEWRELIC_LICENSEKEY		[REPLACE WITH YOUR LICENSE KEY]"
