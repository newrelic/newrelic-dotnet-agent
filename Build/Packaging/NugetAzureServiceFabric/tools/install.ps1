param($installPath, $toolsPath, $package, $project)

Import-Module (Join-Path $toolsPath NewRelicHelperDialog.psm1)
Import-Module (Join-Path $toolsPath NewRelicHelperInstallProjectConfig.psm1)
Import-Module (Join-Path $toolsPath NewRelicHelperInstallProjectItems.psm1)
Import-Module (Join-Path $toolsPath NewRelicHelperInstallServiceManifest.psm1)

Write-Host "***Updating New Relic project items to be marked as Content and copy if newer***"
Update-NewRelicProjectItems $project

Write-Host "***Updating the ServiceManifest.xml files with the Agent environment variables***"
Update-ServiceManifest $project

Write-Host "***Updating the projects [web|app].config file with the NewRelic.AgentEnabled***"
Update-ProjectConfigFile $project

Write-Host "***Package install is complete***"
