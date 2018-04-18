param($installPath, $toolsPath, $package, $project)

Import-Module (Join-Path $toolsPath NewRelicHelperUninstall.psm1)

Write-Host "***Cleaning up the project's [web|app].config file ***"
Remove-ProjectConfig $project

Write-Host "***Cleaning up ServiceManifest.xml***"
Remove-ServiceManifestConfig $project
