param($installPath, $toolsPath, $package, $project)

Import-Module (Join-Path $toolsPath NewRelicHelper.psm1)

Write-Host "***Cleaning up the Windows Azure ServiceConfiguration.*.cscfg files ***"
cleanup_azure_service_configs $project

Write-Host "***Cleaning up the Windows Azure ServiceDefinition.csdef ***"
cleanup_azure_service_definition $project

Write-Host "***Cleaning up the project's .config file ***"
cleanup_project_config $project