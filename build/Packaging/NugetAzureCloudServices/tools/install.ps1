############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

param($installPath, $toolsPath, $package, $project)

Import-Module (Join-Path $toolsPath NewRelicHelper.psm1)

$newRelicAgentMsiFileName = "AGENT_INSTALLER"

Write-Host "***Updating project items newrelic.cmd, $newRelicAgentMsiFileName***"
update_newrelic_project_items $project $newRelicAgentMsiFileName

Write-Host "***Updating the Windows Azure ServiceDefinition.csdef with the newrelic.cmd Startup task***"
update_azure_service_definition $project

Write-Host "***Updating the Windows Azure ServiceConfiguration.*.cscfg files with the license key***"
update_azure_service_configs $project

Write-Host "***Updating the projects .config file with the NewRelic.AppName***"
update_project_config $project

Write-Host "***Package install is complete***"





