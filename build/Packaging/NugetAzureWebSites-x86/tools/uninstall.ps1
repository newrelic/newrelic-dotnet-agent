############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

param($installPath, $toolsPath, $package, $project)

Write-Host "***Cleaning up the newrelic.config file ***"  -ForegroundColor DarkGreen
# manually remove newrelic.config since the Nuget uninstaller won't due to it being "modified"
Try{
	$scripts = $project.ProjectItems | Where-Object { $_.Name -eq "newrelic.config" }

	if ($scripts) {
		$scripts.ProjectItems | ForEach-Object { $_.Delete() }
	}
}Catch{
	#Swallow - file has been removed
}
