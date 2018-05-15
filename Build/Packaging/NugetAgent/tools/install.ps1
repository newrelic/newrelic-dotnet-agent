param($installPath, $toolsPath, $package, $project)

Import-Module (Join-Path $toolsPath NewRelicHelper.psm1)

Write-Host "***Setting New Relic project item properties***"
Set-NewRelicProjectItemProperties $project

Write-Host "***Package install is complete***"
