param($installPath, $toolsPath, $package, $project)

Import-Module (Join-Path $toolsPath NewRelicHelper.psm1)

$newRelicAgentMsiFileName = "AGENT_INSTALLER"
$newRelicServerMonitorMsiFileName = "SERVERMONITOR_INSTALLER"

Write-Host "***Updating project items newrelic.cmd, $newRelicAgentMsiFileName, and $newRelicServerMonitorMsiFileName***"
update_newrelic_project_items $project $newRelicAgentMsiFileName $newRelicServerMonitorMsiFileName

Write-Host "***Updating the Windows Azure ServiceDefinition.csdef with the newrelic.cmd Startup task***"
update_azure_service_definition $project

Write-Host "***Updating the Windows Azure ServiceConfiguration.*.cscfg files with the license key***"
update_azure_service_configs $project

Write-Host "***Updating the projects .config file with the NewRelic.AppName***"
update_project_config $project

Write-Host "***Package install is complete***"





